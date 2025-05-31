// Agents/SqlAnswerSynthesizer.cs
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SemanticSearchApi.Interfaces;
using SemanticSearchApi.Models;

namespace SemanticSearchApi.Agents
{
    public class SqlAnswerSynthesizer
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<SqlAnswerSynthesizer> _logger;
        private readonly string _apiKey;

        public SqlAnswerSynthesizer(IConfiguration configuration, ILogger<SqlAnswerSynthesizer> logger)
        {
            _logger = logger;
            _apiKey = configuration["OpenAI:ApiKey"];
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        public async Task<string> SummarizeSqlResultsAsync(SqlQueryResult result, UserIntent intent)
        {
            if (!result.Success)
            {
                return $"Query failed: {result.Error}";
            }

            if (result.RowCount == 0)
            {
                return "No results found for your query.";
            }

            try
            {
                // Use OpenAI to generate a natural language summary
                var summary = await GenerateOpenAISummary(result, intent);
                return summary;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating OpenAI summary, falling back to basic summary");
                return GenerateBasicSummary(result, intent);
            }
        }

        private async Task<string> GenerateOpenAISummary(SqlQueryResult result, UserIntent intent)
        {
            // Prepare the data for OpenAI
            var prompt = BuildPrompt(result, intent);
            
            var request = new
            {
                model = "gpt-4",
                messages = new[]
                {
                    new { role = "system", content = "You are a helpful assistant that summarizes SQL query results in a clear, concise, and human-friendly way. Focus on answering the user's original question directly." },
                    new { role = "user", content = prompt }
                },
                temperature = 0.7,
                max_tokens = 500
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError($"OpenAI API error: {error}");
                throw new Exception($"OpenAI API error: {response.StatusCode}");
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var responseDoc = JsonDocument.Parse(responseJson);
            var summary = responseDoc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return summary;
        }

        private string BuildPrompt(SqlQueryResult result, UserIntent intent)
        {
            var prompt = new StringBuilder();
            
            prompt.AppendLine($"User's question: {intent.RawQuery}");
            prompt.AppendLine($"\nSQL query returned {result.RowCount} results.");
            
            // Include sample of the data
            if (result.Rows.Any())
            {
                prompt.AppendLine("\nHere's the data returned:");
                
                // For large result sets, include a representative sample
                var samplesToInclude = Math.Min(result.RowCount, 20);
                var sampleRows = result.Rows.Take(samplesToInclude);
                
                // Convert to a more readable format
                var columns = result.Rows.First().Keys.ToList();
                prompt.AppendLine($"Columns: {string.Join(", ", columns)}");
                prompt.AppendLine("\nData:");
                
                foreach (var row in sampleRows)
                {
                    var values = columns.Select(col => $"{col}: {row[col]}");
                    prompt.AppendLine($"- {string.Join(", ", values)}");
                }
                
                if (result.RowCount > samplesToInclude)
                {
                    prompt.AppendLine($"\n(Showing first {samplesToInclude} of {result.RowCount} total results)");
                }
            }
            
            prompt.AppendLine("\nPlease provide a clear, concise summary that directly answers the user's question.");
            prompt.AppendLine("If there are patterns or insights in the data, highlight them.");
            prompt.AppendLine("Format numbers nicely and use bullet points or structure where appropriate.");
            
            return prompt.ToString();
        }

        private string GenerateBasicSummary(SqlQueryResult result, UserIntent intent)
        {
            var summary = new StringBuilder();
            summary.AppendLine($"Found {result.RowCount} results for your query.");
            
            if (result.Rows.Any())
            {
                summary.AppendLine("\nSample results:");
                foreach (var row in result.Rows.Take(5))
                {
                    var rowDesc = string.Join(", ", row.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
                    summary.AppendLine($"- {rowDesc}");
                }
                
                if (result.RowCount > 5)
                {
                    summary.AppendLine($"\n... and {result.RowCount - 5} more results.");
                }
            }
            
            return summary.ToString();
        }
    }
}