using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class OpenAISummarizer : IAnswerSynthesizer
{
    private readonly HttpClient _httpClient;

    public OpenAISummarizer()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "sk-xxx");
    }

    public string Summarize(JsonElement results, UserIntent intent)
    {
        var hits = results.GetProperty("hits").GetProperty("hits");
        if (hits.GetArrayLength() == 0)
            return "No results found.";

        var lines = new List<string>();
        foreach (var hit in hits.EnumerateArray().Take(5))
        {
            if (hit.TryGetProperty("_source", out var src) && src.TryGetProperty(intent.FocusField, out var val))
            {
                lines.Add(val.ToString());
            }
        }

        var prompt = $"Summarize the following {intent.FocusField} values:\n" + string.Join("\n", lines);
        var request = new
        {
            model = "gpt-4",
            messages = new[] { new { role = "user", content = prompt } }
        };

        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        var response = _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content).Result;
        var json = response.Content.ReadAsStringAsync().Result;
        var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
    }
}
