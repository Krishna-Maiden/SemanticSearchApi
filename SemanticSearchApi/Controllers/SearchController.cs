using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading.Tasks;
using Pgvector;
using Microsoft.Extensions.Configuration;
using ClosedXML.Excel;
using System.IO;
using Nest;
using System.Text.Json;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json.Linq;
using System.Text.Json.Serialization;
using DocumentFormat.OpenXml.Spreadsheet;
using Newtonsoft.Json;
using DocumentFormat.OpenXml.Office2010.Excel;

[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly CsvDataService _csvService;
    private readonly EmbeddingService _embedService;
    private readonly PostgresDocumentRepository _repo;
    private readonly QueryInterpretationService _queryInterpreter;
    private readonly IConfiguration _config;

    public SearchController(CsvDataService csvService, EmbeddingService embedService, PostgresDocumentRepository repo, QueryInterpretationService queryInterpreter, IConfiguration config)
    {
        _csvService = csvService;
        _embedService = embedService;
        _repo = repo;
        _queryInterpreter = queryInterpreter;
        _config = config;
    }

    [HttpGet("init")]
    public async Task<IActionResult> Initialize()
    {
        var docs = _csvService.GetDocuments();

        foreach (var doc in docs)
        {
            var textToEmbed = $"{doc.ProductName} {doc.ProductType} {doc.ExporterName}";
            var embeddingArray = await _embedService.GetEmbeddingAsync(textToEmbed);
            doc.Embedding = new Vector(embeddingArray);

            await _repo.SaveDocumentAsync(doc);
        }

        return Ok("CSV data embedded and saved to PostgreSQL.");
    }

    [HttpPost("query")]
    public async Task<IActionResult> Query([FromBody] QueryRequest request)
    {
        int extractedTopN = request.TopN;

        var match = Regex.Match(request.Query.ToLower(), @"top\s+(\d+)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var n))
        {
            extractedTopN = n;
        }

        var (query, chartType) = await _queryInterpreter.GetSQLAndChartType(request.Query, extractedTopN);
        query = await UpdateMapping(query);

        var dbType = _config["Database:Type"]?.ToLower();

        bool isCountQuery = request.Query.ToLower().Contains("how many");

        if (dbType == "elasticsearch")
        {
            var elasticUri = _config["Elastic:Uri"];
            var url = $"{elasticUri.TrimEnd('/')}/_search";
            var apiKey = _config["Elastic:ApiKey"];

            var testQuery = @"{
  ""match"": {
    ""productDescEnglish"": ""Mehandi""
  }
}";

            testQuery = @"{
      ""size"": 5,
      ""query"": {
        ""match"": {
          ""productDesc"": ""Mehandi""
        }
      },
      ""sort"": [
        { ""unitPrice"": { ""order"": ""desc"" } }
      ]
    }";
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };

            var httpClient = new HttpClient(handler);

            var username = _config["Elastic:username"];
            var password = _config["Elastic:password"];
            string credentials = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{username}:{password}"));
            httpClient.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);

            // httpClient.DefaultRequestHeaders.Add("Authorization", $"ApiKey {apiKey}");
            var content = new StringContent(query.Contains("body") ? JsonDocument.Parse(query).RootElement.GetProperty("body").GetRawText() : query, Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                // Read the entire JSON response as a string
                var jsonResponse = await response.Content.ReadAsStringAsync();
                // jsonResponse contains the full Elasticsearch response, including "hits"
                // You can now parse or log this as needed
                var jObject = JsonConvert.DeserializeObject<dynamic>(jsonResponse);
                var hits = jObject["hits"]["hits"]; // This is a JArray of documents
                return Ok(new { Source = "elasticsearch", DSL = query, Chart = chartType, Hits = JsonConvert.SerializeObject(hits) });
            }
            else
            {
                // Handle error
                var error = await response.Content.ReadAsStringAsync();
                // Log or throw exception as needed
                throw new Exception(error);
            }
        }
        else if (!string.IsNullOrWhiteSpace(query) && query.ToLower().Contains("select"))
        {
            bool sqlIsCount = query != null && (query.ToLower().Contains("count(") || query.ToLower().StartsWith("select count"));

            if (isCountQuery && !sqlIsCount && query != null)
            {
                query = Regex.Replace(query, @"select\s+.+?\s+from", "SELECT COUNT(*) FROM", RegexOptions.IgnoreCase);
            }

            var result = await _repo.ExecuteCustomSQLAsync(query);

            if (result is List<Dictionary<string, object>> table)
                return Ok(new { Source = "sql", SQL = query, Chart = chartType, Rows = table });

            if (result is List<Document> documents)
                return Ok(new { Source = "sql", SQL = query, Chart = chartType, Rows = documents });

            return Ok(new { Source = "sql", SQL = query, Chart = chartType, Count = ((dynamic)result).Scalar });
        }

        var queryEmbedding = await _embedService.GetEmbeddingAsync(request.Query);
        var queryVector = new Vector(queryEmbedding);
        var matches = await _repo.SearchTopNAsync(queryVector, extractedTopN, request.Threshold);

        if (isCountQuery)
        {
            return Ok(new { Source = "embedding", Count = matches.Count });
        }
        else
        {
            return Ok(new { Source = "embedding", Count = matches.Count, Matches = matches.Take(extractedTopN) });
        }
    }

    [HttpPost("import/excel-to-elasticsearch")]
    public async Task<IActionResult> ImportExcelToElasticsearch()
    {
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "storage", "sample_export_import_data.xlsx");

        using var workbook = new XLWorkbook(filePath);
        var worksheet = workbook.Worksheet(1);
        var rows = worksheet.RangeUsed().RowsUsed().Skip(1); // skip header

        var elasticUri = _config["Elastic:Uri"];
        var apiKey = _config["Elastic:ApiKey"];
        var credentials = new Elasticsearch.Net.ApiKeyAuthenticationCredentials(apiKey);
        var settings = new ConnectionSettings(new Uri(elasticUri))
            .DefaultIndex("documents")
            .ApiKeyAuthentication(credentials);
        var client = new ElasticClient(settings);

        var bulkRequest = new BulkDescriptor();
        foreach (var row in rows)
        {
            var doc = new
            {
                DocumentId = row.Cell(1).GetValue<string>(),
                parentGlobalExporterId = row.Cell(2).GetValue<int>(),
                parentGlobalImporterId = row.Cell(3).GetValue<int>(),
                productDesc = row.Cell(4).GetValue<string>(),
                productDescription = row.Cell(5).GetValue<string>(),
                productDescEnglish = row.Cell(6).GetValue<string>(),
                countryId = row.Cell(7).GetValue<int>(),
                unitPrice = row.Cell(8).GetValue<double>()
            };

            bulkRequest.Index<object>(idx => idx.Document(doc));
        }

        var response = await client.BulkAsync(bulkRequest);
        if (response.Errors)
        {
            return StatusCode(500, response.ItemsWithErrors);
        }

        return Ok(new { Count = response.Items.Count });
    }

    private async Task<List<int>> LookupCompanyIdsAsync(string partialName)
    {
        var elasticUri = _config["Elastic:Uri"];

        var query = @"{
            ""query"": {
                ""wildcard"": {
                    ""companyName.keyword"": {
                        ""value"": ""*partialName*""
                    }
                }
            }
        }";
        query = query.Replace("partialName", partialName);

        var dsl = new
        {
            query = new
            {
                wildcard = new Dictionary<string, object>
                {
                    ["companyName.keyword"] = new { value = $"*{partialName}*" }
                }
            },
            size = 1000
        };

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };
        using var httpClient = new HttpClient(handler);

        string username = "murli.krishna";
        string password = "LdzPBqM3qHmhKw9wTp";
        string credentials = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{username}:{password}"));
        httpClient.DefaultRequestHeaders.Authorization =
    new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);


        var content = new StringContent(System.Text.Json.JsonSerializer.Serialize(dsl), System.Text.Encoding.UTF8, "application/json");
        var url = $"{elasticUri.TrimEnd('/')}/globalcompanies/_search";
        var response = await httpClient.PostAsync(url, content);

        var ids = new List<int>();

        if (response.IsSuccessStatusCode)
        {
            using var responseStream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(responseStream);

            var hits = doc.RootElement.GetProperty("hits").GetProperty("hits");
            

            foreach (var hit in hits.EnumerateArray())
            {
                if (hit.TryGetProperty("_source", out var source) && source.TryGetProperty("companyId", out var idElement))
                {
                    if (idElement.TryGetInt32(out var id))
                        ids.Add(id);
                }
            }

        }
            return ids;

            var settings = new ConnectionSettings(new Uri(elasticUri))
                .DefaultIndex("globalcompanies-v2");
            var client = new ElasticClient(settings);

            var searchResponse = await client.SearchAsync<CompanyDoc>(s => s
                .Index("globalcompanies-v2")
                .Query(q => q
                    .Wildcard(m => m
        .Field(f => f.companyName.Suffix("keyword"))
        .Value("*" + partialName.ToLower() + "*")
                ))
                .Size(1000)
            );
        return searchResponse.Documents.Select(d => d.companyId).ToList();
    }

    private async Task<string> UpdateMapping(string query)
    {
        query = Regex.Match(query ?? string.Empty, @"\{[\s\S]*\}").Value;
        using var jsonDoc = JsonDocument.Parse(query);
        var root = jsonDoc.RootElement.Clone();
        var rootStr = root.GetRawText();

        foreach (var field in new[] { "parentGlobalExporterId", "parentGlobalImporterId" })
        {
            var pattern = $"\\\"{field}\\\":\\s*\\\"(.*?)\\\"";
            var match = Regex.Match(rootStr, pattern);
            if (match.Success)
            {
                var name = match.Groups[1].Value;
                var ids = await LookupCompanyIdsAsync(name);
                if (ids.Any())
                {
                    var jsonArray = $"[{string.Join(",", ids)}]";
                    rootStr = Regex.Replace(rootStr, pattern, $"\"{field}\": {jsonArray}");
                    rootStr = Regex.Replace(
    rootStr,
    $"\\{{\\s*\\\"match\\\"\\s*:\\s*\\{{\\s*\\\"{field}\\\"\\s*:\\s*\\[.*?\\]\\s*\\}}\\s*\\}}",
    $"{{\"terms\": {{ \"{field}\": {jsonArray} }} }}"
);

                    //rootStr = rootStr.Replace($"\"match\": {{ \"{field}\": {jsonArray} }}", $"\"terms\": {{ \"{field}\": {jsonArray} }}");
                    // Replace match with terms for array of IDs
                    //rootStr = Regex.Replace(rootStr, $"\"match\"\\s*:\\s*\\{{\\s*\"{field}\"\\s*:\\s*{jsonArray}\\s*\\}}", $"\"terms\": {{ \"{field}\": {jsonArray} }}");

                }
            }
        }
        rootStr = Regex.Replace(
    rootStr,
    "\\{\\s*\"match\"\\s*:\\s*\\{\\s*\"productDescEnglish\"\\s*:\\s*\"(.*?)\"\\s*\\}\\s*\\}",
    m => $"{{ \"match_phrase\": {{ \"productDesc\": \"*{m.Groups[1].Value}*\" }} }}"
);

        rootStr = Regex.Match(rootStr ?? string.Empty, @"\{[\s\S]*\}").Value;
        return rootStr;
    }

    private class CompanyDoc
    {
        public int companyId { get; set; }
        public string companyName { get; set; }
    }


    public class QueryRequest
    {
        public string Query { get; set; }
        public int TopN { get; set; } = 100000000;
        public double Threshold { get; set; } = 0.25;
    }
}
