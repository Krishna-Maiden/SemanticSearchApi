// Agents/SqlAnswerSynthesizer.cs
using System.Data.SqlClient;
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
        private readonly ISqlQueryExecutor _executor;
        private readonly string _connectionString;

        public SqlAnswerSynthesizer(
            IConfiguration configuration,
            ILogger<SqlAnswerSynthesizer> logger,
            ISqlQueryExecutor executor)
        {
            _logger = logger;
            _apiKey = configuration["OpenAI:ApiKey"];
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _apiKey);
            _executor = executor;
            _connectionString = configuration.GetConnectionString("SqlServerConnection");
        }

        public async Task<string> SummarizeSqlResultsAsync(SqlQueryResult result, UserIntent intent)
        {
            if (!result.Success)
            {
                return $"Query failed: {result.Error}";
            }

            if (result.RowCount == 0)
            {
                return await GetIntelligentNoResultsResponse(intent);
            }

            try
            {
                var summary = await GenerateOpenAISummary(result, intent);
                return summary;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating OpenAI summary, falling back to basic summary");
                return GenerateBasicSummary(result, intent);
            }
        }

        private async Task<string> GetIntelligentNoResultsResponse(UserIntent intent)
        {
            try
            {
                // Get schema context to help provide better suggestions
                var schemaInfo = await GetRelevantSchemaInfo(intent.RawQuery);

                var prompt = $@"
The user asked: '{intent.RawQuery}'

The query returned no results. Based on the database schema and available data:

{schemaInfo}

Please provide a helpful response that:
1. Explains why there might be no results
2. Suggests what valid values or queries the user could try instead
3. Be concise and helpful
4. If the query references values that don't exist, mention what values ARE available";

                var request = new
                {
                    model = "gpt-4",
                    messages = new[]
                    {
                        new {
                            role = "system",
                            content = "You are a helpful database assistant that provides guidance when queries return no results."
                        },
                        new { role = "user", content = prompt }
                    },
                    temperature = 0.7,
                    max_tokens = 300
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var responseDoc = JsonDocument.Parse(responseJson);
                    return responseDoc.RootElement
                        .GetProperty("choices")[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating intelligent no-results response");
            }

            return "No results found for your query. Please check your search criteria and try again.";
        }

        private async Task<string> GetRelevantSchemaInfo(string query)
        {
            var schemaBuilder = new StringBuilder();

            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Get all tables mentioned in the query (simple heuristic)
                var tables = await GetAllTablesAsync(connection);

                foreach (var table in tables)
                {
                    // Get distinct values for likely relevant columns
                    var relevantData = await GetTableSummaryAsync(connection, table);
                    if (!string.IsNullOrEmpty(relevantData))
                    {
                        schemaBuilder.AppendLine(relevantData);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting schema info for no-results response");
            }

            return schemaBuilder.ToString();
        }

        private async Task<List<string>> GetAllTablesAsync(SqlConnection connection)
        {
            var tables = new List<string>();
            var query = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'";

            using var cmd = new SqlCommand(query, connection);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }

            return tables;
        }

        private async Task<string> GetTableSummaryAsync(SqlConnection connection, string tableName)
        {
            var summary = new StringBuilder();
            summary.AppendLine($"\nTable: {tableName}");

            try
            {
                // Get row count
                var countQuery = $"SELECT COUNT(*) FROM {tableName}";
                using (var cmd = new SqlCommand(countQuery, connection))
                {
                    var count = await cmd.ExecuteScalarAsync();
                    summary.AppendLine($"Total records: {count}");
                }

                // Get sample of distinct values for text columns
                var columnsQuery = $@"
                    SELECT COLUMN_NAME, DATA_TYPE 
                    FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_NAME = '{tableName}' 
                    AND DATA_TYPE IN ('varchar', 'nvarchar', 'int', 'bigint')";

                using var colCmd = new SqlCommand(columnsQuery, connection);
                using var colReader = await colCmd.ExecuteReaderAsync();

                var columns = new List<(string name, string type)>();
                while (await colReader.ReadAsync())
                {
                    columns.Add((colReader.GetString(0), colReader.GetString(1)));
                }

                foreach (var (columnName, dataType) in columns)
                {
                    try
                    {
                        var distinctQuery = $@"
                            SELECT DISTINCT TOP 10 {columnName} 
                            FROM {tableName} 
                            WHERE {columnName} IS NOT NULL 
                            ORDER BY {columnName}";

                        using var distinctCmd = new SqlCommand(distinctQuery, connection);
                        using var distinctReader = await distinctCmd.ExecuteReaderAsync();

                        var values = new List<string>();
                        while (await distinctReader.ReadAsync() && values.Count < 10)
                        {
                            values.Add(distinctReader.GetValue(0).ToString());
                        }

                        if (values.Any())
                        {
                            summary.AppendLine($"{columnName} values: {string.Join(", ", values)}");
                        }
                    }
                    catch
                    {
                        // Skip columns that cause errors
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"Error getting summary for table {tableName}: {ex.Message}");
            }

            return summary.ToString();
        }

        private async Task<string> GenerateOpenAISummary(SqlQueryResult result, UserIntent intent)
        {
            var prompt = BuildPrompt(result, intent);

            var request = new
            {
                model = "gpt-4",
                messages = new[]
                {
                    new {
                        role = "system",
                        content = "You are a helpful assistant that summarizes SQL query results in a clear, concise, and human-friendly way. Focus on answering the user's original question directly."
                    },
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

            if (result.Rows.Any())
            {
                prompt.AppendLine("\nHere's the data returned:");

                var samplesToInclude = Math.Min(result.RowCount, 20);
                var sampleRows = result.Rows.Take(samplesToInclude);

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