// Agents/SqlQueryExecutor.cs (Updated with correction handling)
using System.Data;
using System.Data.SqlClient;
using SemanticSearchApi.Interfaces;
using SemanticSearchApi.Models;
using System.Text.RegularExpressions;

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
                // Extract corrections from SQL comments before execution
                result.Corrections = ExtractCorrectionsFromSql(query);

                // Clean the query (remove comments for execution)
                var cleanQuery = CleanSqlForExecution(query);

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                using var command = new SqlCommand(cleanQuery, connection);
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

                // Log corrections if any were made
                if (result.Corrections.Any())
                {
                    _logger.LogInformation($"Corrections made: {string.Join("; ", result.Corrections)}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing SQL query");
                result.Success = false;
                result.Error = ex.Message;
            }

            return result;
        }

        private List<string> ExtractCorrectionsFromSql(string sql)
        {
            var corrections = new List<string>();

            if (string.IsNullOrEmpty(sql))
                return corrections;

            // Look for correction comments in various formats
            var correctionPatterns = new[]
            {
                @"--\s*Note:\s*Corrected\s+'([^']+)'\s*to\s+'([^']+)'",
                @"--\s*Corrected\s+'([^']+)'\s*to\s+'([^']+)'",
                @"--\s*Fixed\s+spelling:\s+'([^']+)'\s*→\s*'([^']+)'",
                @"--\s*Error:\s*(.+)",
                @"/\*\s*Corrected\s+'([^']+)'\s*to\s+'([^']+)'\s*\*/"
            };

            foreach (var pattern in correctionPatterns)
            {
                var matches = Regex.Matches(sql, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                foreach (Match match in matches)
                {
                    if (match.Groups.Count >= 3)
                    {
                        // Correction format: "Corrected 'X' to 'Y'"
                        var original = match.Groups[1].Value;
                        var corrected = match.Groups[2].Value;
                        corrections.Add($"'{original}' was corrected to '{corrected}'");
                    }
                    else if (match.Groups.Count >= 2)
                    {
                        // Error format: "Error: message"
                        corrections.Add(match.Groups[1].Value);
                    }
                }
            }

            return corrections;
        }

        private string CleanSqlForExecution(string sql)
        {
            if (string.IsNullOrEmpty(sql))
                return sql;

            // Remove single-line comments (-- comments)
            var lines = sql.Split('\n');
            var cleanLines = lines.Where(line => !line.TrimStart().StartsWith("--")).ToList();

            // Remove multi-line comments (/* comments */)
            var cleanSql = string.Join('\n', cleanLines);
            cleanSql = Regex.Replace(cleanSql, @"/\*.*?\*/", "", RegexOptions.Singleline);

            return cleanSql.Trim();
        }
    }
}