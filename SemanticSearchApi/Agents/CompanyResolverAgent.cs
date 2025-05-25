using System.Net.Http.Headers;
using System.Text.Json;

public class CompanyResolverAgent : ICompanyResolverAgent
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    public CompanyResolverAgent(IConfiguration config)
    {
        _config = config;

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };

        _httpClient = new HttpClient(handler);
        var username = config["Elastic:username"];
        var password = config["Elastic:password"];
        string credentials = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{username}:{password}"));
        _httpClient.DefaultRequestHeaders.Authorization =
    new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
    }

    public async Task<Dictionary<string, List<int>>> ResolveCompaniesAsync(string input)
    {
        bool splitInput = false;
        var results = new Dictionary<string, List<int>>();

        if (splitInput)
        {
            var terms = input.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);

            foreach (var term in terms)
            {
                var query = new
                {
                    query = new
                    {
                        wildcard = new Dictionary<string, object>
                        {
                            ["companyName.keyword"] = new { value = $"*{term}*" }
                        }
                    },
                    size = 100
                };

                var content = new StringContent(JsonSerializer.Serialize(query));
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                var elasticUri = _config["Elastic:Uri"];
                var url = $"{elasticUri.TrimEnd('/')}/globalcompanies/_search";
                var response = await _httpClient.PostAsync(url, content);
                if (!response.IsSuccessStatusCode) continue;

                var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var hits = doc.RootElement.GetProperty("hits").GetProperty("hits");

                var ids = new List<int>();
                foreach (var hit in hits.EnumerateArray())
                {
                    if (hit.TryGetProperty("_source", out var src) && src.TryGetProperty("companyId", out var idProp) && idProp.TryGetInt32(out int id))
                    {
                        ids.Add(id);
                    }
                }

                if (ids.Count > 0)
                {
                    results[term] = ids;
                }
            }
        }
        else
        {
            var query = new
            {
                query = new
                {
                    wildcard = new Dictionary<string, object>
                    {
                        ["companyName.keyword"] = new { value = $"*{input}*" }
                    }
                },
                size = 1000
            };

            var content = new StringContent(JsonSerializer.Serialize(query));
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            var elasticUri = _config["Elastic:Uri"];
            var url = $"{elasticUri.TrimEnd('/')}/globalcompanies/_search";
            var response = await _httpClient.PostAsync(url, content);
            //if (!response.IsSuccessStatusCode) return;

            var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var hits = doc.RootElement.GetProperty("hits").GetProperty("hits");

            var ids = new List<int>();
            foreach (var hit in hits.EnumerateArray())
            {
                if (hit.TryGetProperty("_source", out var src) && src.TryGetProperty("companyId", out var idProp) && idProp.TryGetInt32(out int id))
                {
                    ids.Add(id);
                }
            }

            if (ids.Count > 0)
            {
                results[input] = ids;
            }
        }


        return results;
    }
}
