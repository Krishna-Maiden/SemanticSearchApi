// Services/OpenAiSqlGenerator.cs
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SemanticSearchApi.Interfaces;
using System.Data;
using System.Data.SqlClient;

namespace SemanticSearchApi.Services
{
    public class OpenAiSqlGenerator : IOpenAiSqlGenerator
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OpenAiSqlGenerator> _logger;
        private readonly string _apiKey;
        private readonly string _model;
        private readonly string _connectionString;
        private string _schemaContext;
        private DateTime _schemaLastUpdated;
        private readonly TimeSpan _schemaCacheDuration = TimeSpan.FromHours(1);

        public OpenAiSqlGenerator(HttpClient httpClient, ILogger<OpenAiSqlGenerator> logger, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            _apiKey = configuration["OpenAI:ApiKey"];
            _model = configuration["OpenAI:Model"] ?? "gpt-4";
            _connectionString = configuration.GetConnectionString("SqlServerConnection");
        }

        public async Task<string> GenerateSqlAsync(string naturalLanguageQuery)
        {
            // Get or refresh the schema context
            var schemaContext = await GetSchemaContextAsync();

            var systemPrompt = $@"You are an expert SQL query generator. Convert natural language queries to SQL based on the following database schema and examples.

{schemaContext}

Important rules:
1. Only use tables and columns that exist in the schema above
2. Use appropriate SQL Server syntax (TOP instead of LIMIT, etc.)
3. Handle aggregations intelligently (COUNT, AVG, SUM, etc.)
4. When counting distinct entities, use COUNT(DISTINCT column)
5. Include appropriate GROUP BY clauses for aggregations
6. Add ORDER BY for better result organization
7. Return ONLY the SQL query, no explanations
8. Handle synonyms and variations intelligently
9. For queries asking about specific values that don't exist, the query should still be valid and return empty results";

            var userPrompt = $@"Generate SQL for: {naturalLanguageQuery}";

            var requestBody = new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.1, // Lower temperature for more consistent SQL
                max_tokens = 500
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

            _logger.LogInformation("Generated SQL: {Sql}", sql);
            return sql ?? "";
        }

        private async Task<string> GetSchemaContextAsync()
        {
            // Cache the schema for performance
            if (_schemaContext != null && DateTime.UtcNow - _schemaLastUpdated < _schemaCacheDuration)
            {
                return _schemaContext;
            }

            var schemaBuilder = new StringBuilder();

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // Get all tables and their columns with data types
            var schemaQuery = @"
                SELECT 
                    t.TABLE_NAME,
                    c.COLUMN_NAME,
                    c.DATA_TYPE,
                    c.CHARACTER_MAXIMUM_LENGTH,
                    c.IS_NULLABLE,
                    c.COLUMN_DEFAULT
                FROM INFORMATION_SCHEMA.TABLES t
                JOIN INFORMATION_SCHEMA.COLUMNS c ON t.TABLE_NAME = c.TABLE_NAME
                WHERE t.TABLE_TYPE = 'BASE TABLE' and t.Table_Name like 'Student%'
                ORDER BY t.TABLE_NAME, c.ORDINAL_POSITION";

            using var command = new SqlCommand(schemaQuery, connection);
            using var reader = await command.ExecuteReaderAsync();

            var currentTable = "";
            var tableColumns = new List<string>();

            while (await reader.ReadAsync())
            {
                var tableName = reader.GetString(0);
                var columnName = reader.GetString(1);
                var dataType = reader.GetString(2);
                var maxLength = reader.IsDBNull(3) ? null : reader.GetInt32(3).ToString();
                var isNullable = reader.GetString(4);

                if (currentTable != tableName)
                {
                    if (!string.IsNullOrEmpty(currentTable))
                    {
                        schemaBuilder.AppendLine($"Table: {currentTable}");
                        schemaBuilder.AppendLine($"Columns: {string.Join(", ", tableColumns)}");
                        schemaBuilder.AppendLine();
                    }
                    currentTable = tableName;
                    tableColumns.Clear();
                }

                var columnDef = $"{columnName} ({dataType}{(maxLength != null ? $"({maxLength})" : "")})";
                tableColumns.Add(columnDef);
            }

            // Add the last table
            if (!string.IsNullOrEmpty(currentTable))
            {
                schemaBuilder.AppendLine($"Table: {currentTable}");
                schemaBuilder.AppendLine($"Columns: {string.Join(", ", tableColumns)}");
                schemaBuilder.AppendLine();
            }

            // Get sample data and value distributions
            schemaBuilder.AppendLine("Sample Data and Patterns:");
            await AddSampleDataAsync(connection, schemaBuilder);

            // Get relationships if any
            await AddRelationshipsAsync(connection, schemaBuilder);

            _schemaContext = schemaBuilder.ToString();
            _schemaLastUpdated = DateTime.UtcNow;

            return _schemaContext;
        }

        private async Task AddSampleDataAsync(SqlConnection connection, StringBuilder schemaBuilder)
        {
            // Get list of tables
            var tablesQuery = @"
                SELECT TABLE_NAME 
                FROM INFORMATION_SCHEMA.TABLES 
                WHERE TABLE_TYPE = 'BASE TABLE'";

            var tables = new List<string>();
            using (var cmd = new SqlCommand(tablesQuery, connection))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    tables.Add(reader.GetString(0));
                }
            }

            foreach (var table in tables)
            {
                try
                {
                    // Get distinct values for key columns (limit to prevent overwhelming the prompt)
                    var sampleQuery = $@"
                        SELECT TOP 5 * 
                        FROM {table}
                        ORDER BY 1";

                    using var cmd = new SqlCommand(sampleQuery, connection);
                    using var reader = await cmd.ExecuteReaderAsync();

                    if (reader.HasRows)
                    {
                        schemaBuilder.AppendLine($"\nSample data from {table}:");

                        var fieldCount = reader.FieldCount;
                        var columnNames = new List<string>();

                        for (int i = 0; i < fieldCount; i++)
                        {
                            columnNames.Add(reader.GetName(i));
                        }

                        var sampleRows = new List<string>();
                        while (await reader.ReadAsync() && sampleRows.Count < 3)
                        {
                            var values = new List<string>();
                            for (int i = 0; i < fieldCount; i++)
                            {
                                var value = reader.IsDBNull(i) ? "NULL" : reader.GetValue(i).ToString();
                                values.Add($"{columnNames[i]}: {value}");
                            }
                            sampleRows.Add($"  - {string.Join(", ", values)}");
                        }

                        schemaBuilder.AppendLine(string.Join("\n", sampleRows));
                    }

                    // Get distinct values for categorical columns
                    await AddDistinctValuesAsync(connection, table, schemaBuilder);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Error getting sample data for table {table}: {ex.Message}");
                }
            }
        }

        private async Task AddDistinctValuesAsync(SqlConnection connection, string tableName, StringBuilder schemaBuilder)
        {
            // Get columns that likely contain categorical data
            var columnsQuery = $@"
                SELECT COLUMN_NAME, DATA_TYPE
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = '{tableName}'
                AND DATA_TYPE IN ('varchar', 'nvarchar', 'char', 'nchar')
                AND CHARACTER_MAXIMUM_LENGTH <= 100";

            using var cmd = new SqlCommand(columnsQuery, connection);
            using var reader = await cmd.ExecuteReaderAsync();

            var categoricalColumns = new List<string>();
            while (await reader.ReadAsync())
            {
                categoricalColumns.Add(reader.GetString(0));
            }

            foreach (var column in categoricalColumns)
            {
                try
                {
                    var distinctQuery = $@"
                        SELECT DISTINCT TOP 10 {column}, COUNT(*) as Count
                        FROM {tableName}
                        WHERE {column} IS NOT NULL
                        GROUP BY {column}
                        ORDER BY COUNT(*) DESC";

                    using var distinctCmd = new SqlCommand(distinctQuery, connection);
                    using var distinctReader = await distinctCmd.ExecuteReaderAsync();

                    var values = new List<string>();
                    while (await distinctReader.ReadAsync())
                    {
                        values.Add(distinctReader.GetString(0));
                    }

                    if (values.Any())
                    {
                        schemaBuilder.AppendLine($"\nDistinct values in {tableName}.{column}: {string.Join(", ", values)}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug($"Error getting distinct values for {tableName}.{column}: {ex.Message}");
                }
            }
        }

        private async Task AddRelationshipsAsync(SqlConnection connection, StringBuilder schemaBuilder)
        {
            var relationshipQuery = @"
                SELECT 
                    fk.name AS FK_Name,
                    tp.name AS Parent_Table,
                    cp.name AS Parent_Column,
                    tr.name AS Child_Table,
                    cr.name AS Child_Column
                FROM sys.foreign_keys fk
                INNER JOIN sys.tables tp ON fk.parent_object_id = tp.object_id
                INNER JOIN sys.tables tr ON fk.referenced_object_id = tr.object_id
                INNER JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
                INNER JOIN sys.columns cp ON fkc.parent_column_id = cp.column_id AND fkc.parent_object_id = cp.object_id
                INNER JOIN sys.columns cr ON fkc.referenced_column_id = cr.column_id AND fkc.referenced_object_id = cr.object_id";

            using var cmd = new SqlCommand(relationshipQuery, connection);
            using var reader = await cmd.ExecuteReaderAsync();

            if (reader.HasRows)
            {
                schemaBuilder.AppendLine("\nRelationships:");
                while (await reader.ReadAsync())
                {
                    var parentTable = reader.GetString(1);
                    var parentColumn = reader.GetString(2);
                    var childTable = reader.GetString(3);
                    var childColumn = reader.GetString(4);

                    schemaBuilder.AppendLine($"- {parentTable}.{parentColumn} -> {childTable}.{childColumn}");
                }
            }
        }
    }
}