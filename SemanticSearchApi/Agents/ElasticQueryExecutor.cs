using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class ElasticQueryExecutor : IElasticQueryExecutor
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;

    public ElasticQueryExecutor(IConfiguration config)
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

    public async Task<JsonElement> ExecuteAsync(string queryDsl)
    {
        var elasticUri = _config["Elastic:Uri"];
        var url = $"{elasticUri.TrimEnd('/')}/_search";
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(queryDsl, Encoding.UTF8, "application/json")
        };

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync();
        return (await JsonDocument.ParseAsync(stream)).RootElement;
    }
}
