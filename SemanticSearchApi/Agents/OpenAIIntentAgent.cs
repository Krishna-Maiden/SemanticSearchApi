using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Identity.Client;
using Nest;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class OpenAIIntentAgent : IIntentAgent
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public OpenAIIntentAgent(IConfiguration config)
    {
        _httpClient = new HttpClient();
        _apiKey = config["OpenAI:ApiKey"];
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
    }

    public async Task<UserIntent> InterpretAsync(string input, ConversationContext context)
    {
        var prompt = @"You are an AI that extracts structured query intent from user input.
Return a JSON object with:
- RawQuery: original user question
- FocusField: the main field to retrieve (e.g., unitRateUsd)
- CompanyMentions: { Exporter, Importer } — if applicable
- Product: relevant product name (optional)

Example:
User: 'What price is Global Spices selling Henna to Wellness World Trade?'
Output:
{
  \""RawQuery\"": \"":What price is Global Spices selling Henna to Wellness World Trade?\"":,
  \"":FocusField\"":: \"":unitRateUsd\"":,
  \"":CompanyMentions\"":: {
    \"":Exporter\"":: \"":Global Spices\"":,
    \"":Importer\"":: \"":Wellness World Trade\"":
  },
  \"":Product\"":: \"":Henna\"":
}

Now extract from:
" + input;

        var payload = new
        {
            model = "gpt-4",
            messages = new[] { new { role = "user", content = prompt } }
        };

        var response = await _httpClient.PostAsync(
            "https://api.openai.com/v1/chat/completions",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        );

        var json = await response.Content.ReadAsStringAsync();
        var root = JsonDocument.Parse(json);
        var content = root.RootElement.GetProperty("choices")[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString();

        var intent = JsonSerializer.Deserialize<UserIntent>(content ?? "{}");
        if (intent != null)
        {
            intent.RawQuery ??= input;
        }
        else
        {
            intent = new UserIntent { RawQuery = input };
        }

        return intent;
    }
}
