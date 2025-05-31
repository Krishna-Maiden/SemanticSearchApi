// Services/OpenAiSqlGenerator.cs
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SemanticSearchApi.Interfaces;

namespace SemanticSearchApi.Services
{
    public class OpenAiSqlGenerator : IOpenAiSqlGenerator
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OpenAiSqlGenerator> _logger;
        private readonly string _apiKey;
        private readonly string _model;

        public OpenAiSqlGenerator(HttpClient httpClient, ILogger<OpenAiSqlGenerator> logger, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            _apiKey = configuration["OpenAI:ApiKey"];
            _model = configuration["OpenAI:Model"] ?? "gpt-4";
        }

        public async Task<string> GenerateSqlAsync(string naturalLanguageQuery)
        {
            var systemPrompt = @"
You are a SQL query generator for a table named 'Student' with the following schema:
- Name (string)
- Subject (string)
- Grade (int)

Only return valid, executable SQL queries in plain text. Do not include explanations or formatting.
";

            var requestBody = new
            {
                model = _model,
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = naturalLanguageQuery }
                },
                temperature = 0.2
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

            var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
            response.EnsureSuccessStatusCode();

            using var responseStream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(responseStream);

            var sql = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString()
                ?.Trim();

            _logger.LogInformation("OpenAI SQL generated: {Sql}", sql);
            return sql ?? "";
        }
    }
}
