// Services/EmbeddingService.cs
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public class EmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public EmbeddingService(IConfiguration config)
    {
        _apiKey = config["OpenAI:ApiKey"];
        _httpClient = new HttpClient();
    }

    public async Task<float[]> GetEmbeddingAsync(string text)
    {
        var json = JsonSerializer.Serialize(new
        {
            input = text,
            model = "text-embedding-ada-002"
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/embeddings")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var embedding = doc.RootElement.GetProperty("data")[0].GetProperty("embedding");

        return embedding.EnumerateArray().Select(x => x.GetSingle()).ToArray();
    }
}
