using System.Data;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

public class QueryInterpretationService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _dbType;
    private readonly IConfiguration _config;

    public QueryInterpretationService(IConfiguration config)
    {
        _config = config;
        _apiKey = config["OpenAI:ApiKey"];
        _dbType = config["Database:Type"]?.ToLower();
        _httpClient = new HttpClient();
    }

    public async Task<(string? Sql, string? ChartType)> GetSQLAndChartType(string userQuery, int topN = 0)
    {
        string prompt;

        if (_dbType == "elasticsearch")
        {
            prompt = $@"
You are an assistant that converts natural language into Elasticsearch DSL (Query DSL).
Schema: importers/exporters(date, countryId, parentGlobalExporterId, parentGlobalImporterId, productDesc, productDescEnglish, productDescription, unitRateUsd)
Rules:
- Do not apply sort unless explicitly asked.
- If the query asks for specific fields (e.g., unitRateUsd), include a ""_source"" block listing only those fields.
- Avoid returning all fields unless the user asks for full details.
- Do not include a 'body' wrapper. Output only the raw query structure expected by the Elasticsearch _search endpoint.
Synonyms and Matching:
- Map any synonyms or variations of known products to their official names mentioned in this list Mehandi, Coffee.
- The query string should be the inner JSON, e.g.:
- {{ ""query"": {{ ""match"": {{ ""productDescEnglish"": ""Mehandi"" }} }}, ""sort"": [ ... ], ""size"": 5 }}
- Not wrapped in {{ ""index"": ..., ""body"": ... }}

Respond in JSON:
{{
 ""dsl"": {{ body: {{...}} }},
  ""chart"": ""...""
}}

    Query:
""{userQuery}""";
        }
        else // Default: PostgreSQL
        {
            prompt = $@"
You are an assistant that converts natural language to SQL.
Schema: documents(transaction_id, exporter_name, product_name, price_in_inr, product_type)

Known product names: Lemon Soda, Blueberry Soda, Mehandi, Green Tea, Red Label Tea, Coffee
Known product types: Cool Drink, Powder, Hot Drink

Synonyms and Matching:
- Map any synonyms or variations of known products to their official name.
- Only use the listed product names and types.
- If the query references unlisted values, return an empty SQL string.
- Use ILIKE for filtering. Use COUNT(*) for 'how many' questions. Use LIMIT for 'top/few'.

Respond in JSON:
{{
 ""sql"": ""..."",
  ""chart"": ""...""
}}

Query:
""{userQuery}""";
        }

        var body = new
        {
            model = "gpt-4",
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        var json = JsonSerializer.Serialize(body);
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        var responseString = await response.Content.ReadAsStringAsync();

        var doc = JsonDocument.Parse(responseString);
        var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

        try
        {
            var resultJson = JsonDocument.Parse(content);
            if (_dbType == "elasticsearch")
            {
                var dsl = resultJson.RootElement.GetProperty("dsl").GetRawText();
                var chart = resultJson.RootElement.TryGetProperty("chart", out var chartProp) ? chartProp.GetString() : null;
                return (dsl, chart?.Trim());
            }
            else
            {
                var sql = resultJson.RootElement.GetProperty("sql").GetString();
                var chart = resultJson.RootElement.TryGetProperty("chart", out var chartProp) ? chartProp.GetString() : null;
                return (sql?.Trim(), chart?.Trim());
            }
        }
        catch
        {
            return (content?.Trim(), null);
        }
    }
}
