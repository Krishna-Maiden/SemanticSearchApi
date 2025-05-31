// Agents/SqlQueryPlanner.cs
using SemanticSearchApi.Interfaces;
using SemanticSearchApi.Models;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SemanticSearchApi.Agents
{
    public class SqlQueryPlanner : ISqlQueryPlanner
    {
        private readonly ILogger<SqlQueryPlanner> _logger;
        private readonly IOpenAiSqlGenerator _sqlGenerator;

        public SqlQueryPlanner(ILogger<SqlQueryPlanner> logger, IOpenAiSqlGenerator sqlGenerator)
        {
            _logger = logger;
            _sqlGenerator = sqlGenerator;
        }

        public async Task<string> PlanSqlAsync(UserIntent intent)
        {
            // Use OpenAI to generate SQL from the raw query
            var sql = await _sqlGenerator.GenerateSqlAsync(intent.RawQuery);

            _logger.LogInformation($"Generated SQL from OpenAI: {sql}");

            return sql;
        }
    }
}
