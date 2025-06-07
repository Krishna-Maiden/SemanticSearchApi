// Services/OpenAiSqlGenerator.cs (Completely generic version)
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SemanticSearchApi.Interfaces;
using System.Data;
using System.Data.SqlClient;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

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
            var schemaContext = await GetSchemaContextAsync();

            var systemPrompt = $@"You are an expert SQL query generator. Convert natural language queries to SQL based on the following database schema discovered from the actual database.

{schemaContext}

CRITICAL UNDERSTANDING RULES:
1. Use ONLY the tables and columns shown in the schema above
2. Pay close attention to the 'Distinct values' shown for each column - these are the ACTUAL data values in the database
3. Use the exact values shown in the distinct values lists when filtering data
4. When users mention specific values, match them exactly to what exists in the distinct values

QUERY CONSTRUCTION RULES:
1. Use proper JOINs when connecting related tables (look for foreign key relationships)
2. Use appropriate SQL Server syntax (TOP instead of LIMIT, etc.)
3. Handle aggregations intelligently (COUNT, AVG, SUM, etc.)
4. When counting distinct entities, use COUNT(DISTINCT column)
5. Include appropriate GROUP BY clauses for aggregations
6. Add ORDER BY for better result organization when appropriate
7. Return ONLY the SQL query, no explanations or comments
8. Use proper data type casting when needed (e.g., CAST(column as FLOAT) for averages)
9. Always use square brackets around table and column names: [TableName].[ColumnName]

QUERY OPTIMIZATION:
- Use EXISTS over IN for better performance when appropriate
- Use appropriate WHERE clauses to filter data
- Consider using table aliases for readability
- Leverage primary key and foreign key relationships shown in the schema

Remember: Work ONLY with the actual schema and data values provided. The distinct values show exactly what data exists in each column.";

            var userPrompt = $@"Generate SQL for: {naturalLanguageQuery}

Look at the distinct values in the schema to understand what data exists in the database.";

            var requestBody = new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.1,
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
            if (_schemaContext != null && DateTime.UtcNow - _schemaLastUpdated < _schemaCacheDuration)
            {
                return _schemaContext;
            }

            var schemaBuilder = new StringBuilder();
            schemaBuilder.AppendLine("DATABASE SCHEMA (Discovered Dynamically):");
            schemaBuilder.AppendLine("==========================================");

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // Get all tables and their columns
            await AddTableSchemaAsync(connection, schemaBuilder);

            // Get foreign key relationships
            await AddRelationshipsAsync(connection, schemaBuilder);

            // Get sample data to help AI understand the data patterns
            await AddSampleDataAsync(connection, schemaBuilder);

            _schemaContext = schemaBuilder.ToString();
            _schemaLastUpdated = DateTime.UtcNow;

            return _schemaContext;
        }

        private async Task AddTableSchemaAsync(SqlConnection connection, StringBuilder schemaBuilder)
        {
            var tableSchemaQuery = @"
                SELECT 
                    t.TABLE_NAME,
                    c.COLUMN_NAME,
                    c.DATA_TYPE,
                    c.CHARACTER_MAXIMUM_LENGTH,
                    c.IS_NULLABLE,
                    c.COLUMN_DEFAULT,
                    CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 'YES' ELSE 'NO' END as IS_PRIMARY_KEY,
                    CASE WHEN fk.COLUMN_NAME IS NOT NULL THEN 'YES' ELSE 'NO' END as IS_FOREIGN_KEY,
                    fk.REFERENCED_TABLE_NAME,
                    fk.REFERENCED_COLUMN_NAME
                FROM INFORMATION_SCHEMA.TABLES t
                JOIN INFORMATION_SCHEMA.COLUMNS c ON t.TABLE_NAME = c.TABLE_NAME and t.TABLE_NAME in ('Student', 'Subject', 'StudentGradeSubject')
                LEFT JOIN (
                    SELECT ku.TABLE_NAME, ku.COLUMN_NAME
                    FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                    JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                    WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                ) pk ON c.TABLE_NAME = pk.TABLE_NAME AND c.COLUMN_NAME = pk.COLUMN_NAME
                LEFT JOIN (
                    SELECT 
                        ku.TABLE_NAME, 
                        ku.COLUMN_NAME,
                        ccu.TABLE_NAME AS REFERENCED_TABLE_NAME,
                        ccu.COLUMN_NAME AS REFERENCED_COLUMN_NAME
                    FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                    JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                    JOIN INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE ccu ON tc.CONSTRAINT_NAME = ccu.CONSTRAINT_NAME
                    WHERE tc.CONSTRAINT_TYPE = 'FOREIGN KEY'
                ) fk ON c.TABLE_NAME = fk.TABLE_NAME AND c.COLUMN_NAME = fk.COLUMN_NAME
                WHERE t.TABLE_TYPE = 'BASE TABLE'
                ORDER BY t.TABLE_NAME, c.ORDINAL_POSITION";

            using var cmd = new SqlCommand(tableSchemaQuery, connection);
            using var reader = await cmd.ExecuteReaderAsync();

            var currentTable = "";
            var tableRecordCounts = new Dictionary<string, int>();

            while (await reader.ReadAsync())
            {
                var tableName = reader.GetString("TABLE_NAME");

                if (currentTable != tableName)
                {
                    if (!string.IsNullOrEmpty(currentTable))
                    {
                        schemaBuilder.AppendLine();
                    }

                    currentTable = tableName;
                    schemaBuilder.AppendLine($"Table: [{tableName}]");
                }

                var columnName = reader.GetString("COLUMN_NAME");
                var dataType = reader.GetString("DATA_TYPE");
                var maxLength = reader.IsDBNull("CHARACTER_MAXIMUM_LENGTH") ? null : reader.GetInt32("CHARACTER_MAXIMUM_LENGTH").ToString();
                var isNullable = reader.GetString("IS_NULLABLE") == "YES";
                var isPrimaryKey = reader.GetString("IS_PRIMARY_KEY") == "YES";
                var isForeignKey = reader.GetString("IS_FOREIGN_KEY") == "YES";
                var referencedTable = reader.IsDBNull("REFERENCED_TABLE_NAME") ? null : reader.GetString("REFERENCED_TABLE_NAME");
                var referencedColumn = reader.IsDBNull("REFERENCED_COLUMN_NAME") ? null : reader.GetString("REFERENCED_COLUMN_NAME");

                var columnDescription = $"  - [{columnName}]: {dataType}";
                if (maxLength != null) columnDescription += $"({maxLength})";
                if (!isNullable) columnDescription += " NOT NULL";
                if (isPrimaryKey) columnDescription += " [PRIMARY KEY]";
                if (isForeignKey && referencedTable != null)
                    columnDescription += $" [FOREIGN KEY -> {referencedTable}.{referencedColumn}]";

                schemaBuilder.AppendLine(columnDescription);
            }
        }

        private async Task AddRelationshipsAsync(SqlConnection connection, StringBuilder schemaBuilder)
        {
            var relationshipQuery = @"
                SELECT 
                    tp.name AS ParentTable,
                    cp.name AS ParentColumn,
                    tr.name AS ChildTable,
                    cr.name AS ChildColumn
                FROM sys.foreign_keys fk
                INNER JOIN sys.tables tp ON fk.referenced_object_id = tp.object_id and tp.name in ('Student', 'Subject', 'StudentGradeSubject')
                INNER JOIN sys.tables tr ON fk.parent_object_id = tr.object_id
                INNER JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
                INNER JOIN sys.columns cp ON fkc.referenced_column_id = cp.column_id AND fkc.referenced_object_id = cp.object_id
                INNER JOIN sys.columns cr ON fkc.parent_column_id = cr.column_id AND fkc.parent_object_id = cr.object_id";

            using var cmd = new SqlCommand(relationshipQuery, connection);
            using var reader = await cmd.ExecuteReaderAsync();

            var hasRelationships = false;
            var relationships = new List<string>();

            while (await reader.ReadAsync())
            {
                if (!hasRelationships)
                {
                    hasRelationships = true;
                    schemaBuilder.AppendLine("\nRELATIONSHIPS:");
                    schemaBuilder.AppendLine("==============");
                }

                var parentTable = reader.GetString("ParentTable");
                var parentColumn = reader.GetString("ParentColumn");
                var childTable = reader.GetString("ChildTable");
                var childColumn = reader.GetString("ChildColumn");

                var relationship = $"[{parentTable}].[{parentColumn}] <- [1:N] -> [{childTable}].[{childColumn}]";
                schemaBuilder.AppendLine(relationship);
            }
        }

        private async Task AddSampleDataAsync(SqlConnection connection, StringBuilder schemaBuilder)
        {
            // Get all table names first
            var tablesQuery = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' and TABLE_NAME in ('Student', 'Subject', 'StudentGradeSubject')";
            var tables = new List<string>();

            using (var cmd = new SqlCommand(tablesQuery, connection))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    tables.Add(reader.GetString("TABLE_NAME"));
                }
            }

            schemaBuilder.AppendLine("\nDATA PATTERNS AND VALUES (Critical for AI Understanding):");
            schemaBuilder.AppendLine("=========================================================");

            foreach (var table in tables)
            {
                try
                {
                    // Get record count
                    var countQuery = $"SELECT COUNT(*) FROM [{table}]";
                    int recordCount = 0;

                    using (var cmd = new SqlCommand(countQuery, connection))
                    {
                        recordCount = (int)await cmd.ExecuteScalarAsync();
                    }

                    schemaBuilder.AppendLine($"\nTable [{table}]: {recordCount:N0} records");

                    if (recordCount > 0)
                    {
                        // Get sample records
                        var sampleQuery = $"SELECT TOP 5 * FROM [{table}]";
                        using var cmd = new SqlCommand(sampleQuery, connection);
                        using var reader = await cmd.ExecuteReaderAsync();

                        var columnNames = new List<string>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            columnNames.Add(reader.GetName(i));
                        }

                        var sampleCount = 0;
                        while (await reader.ReadAsync() && sampleCount < 5)
                        {
                            var values = new List<string>();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                var value = reader.IsDBNull(i) ? "NULL" : reader.GetValue(i).ToString();
                                values.Add($"{columnNames[i]}={value}");
                            }
                            schemaBuilder.AppendLine($"  Sample {sampleCount + 1}: {string.Join(", ", values)}");
                            sampleCount++;
                        }

                        // Get distinct values for key columns to help AI understand data patterns
                        await AddDistinctValuesForTableAsync(connection, table, schemaBuilder);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug($"Error getting sample data for table {table}: {ex.Message}");
                    schemaBuilder.AppendLine($"Table [{table}]: Unable to read sample data");
                }
            }
        }

        private async Task AddDistinctValuesForTableAsync(SqlConnection connection, string tableName, StringBuilder schemaBuilder)
        {
            try
            {
                // Get columns for this table
                var columnsQuery = $@"
                    SELECT COLUMN_NAME, DATA_TYPE 
                    FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_NAME = '{tableName}'
                    ORDER BY ORDINAL_POSITION";

                var columns = new List<(string name, string type)>();
                using (var cmd = new SqlCommand(columnsQuery, connection))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        columns.Add((reader.GetString(0), reader.GetString(1)));
                    }
                }

                // Show distinct values for each column (especially important for lookup/reference data)
                foreach (var (columnName, dataType) in columns)
                {
                    try
                    {
                        var distinctQuery = $@"
                            SELECT DISTINCT TOP 10 [{columnName}] 
                            FROM [{tableName}] 
                            WHERE [{columnName}] IS NOT NULL 
                            ORDER BY [{columnName}]";

                        using var distinctCmd = new SqlCommand(distinctQuery, connection);
                        using var distinctReader = await distinctCmd.ExecuteReaderAsync();

                        var values = new List<string>();
                        while (await distinctReader.ReadAsync())
                        {
                            values.Add(distinctReader.GetValue(0).ToString());
                        }

                        if (values.Any())
                        {
                            schemaBuilder.AppendLine($"  Distinct values in [{columnName}]: {string.Join(", ", values)}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug($"Error getting distinct values for {tableName}.{columnName}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"Error analyzing distinct values for table {tableName}: {ex.Message}");
            }
        }
    }
}