// Agents/SqlQueryExecutor.cs
using System.Data;
using System.Data.SqlClient;
using SemanticSearchApi.Interfaces;

namespace SemanticSearchApi.Agents
{
    public class SqlQueryExecutor : ISqlQueryExecutor
    {
        private readonly string _connectionString;
        private readonly ILogger<SqlQueryExecutor> _logger;

        public SqlQueryExecutor(IConfiguration configuration, ILogger<SqlQueryExecutor> logger)
        {
            _connectionString = configuration.GetConnectionString("SqlServerConnection");
            _logger = logger;
        }

        public async Task<SqlQueryResult> ExecuteAsync(string query)
        {
            var result = new SqlQueryResult { Success = true };

            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                using var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                // Get column names
                var columns = new List<string>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    columns.Add(reader.GetName(i));
                }

                // Read rows
                while (await reader.ReadAsync())
                {
                    var row = new Dictionary<string, object>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        row[columns[i]] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    }
                    result.Rows.Add(row);
                }

                _logger.LogInformation($"SQL query executed successfully. Rows returned: {result.RowCount}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing SQL query");
                result.Success = false;
                result.Error = ex.Message;
            }

            return result;
        }
    }
}