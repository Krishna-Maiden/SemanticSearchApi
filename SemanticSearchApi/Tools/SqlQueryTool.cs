// Tools/SqlQueryTool.cs
using System.Text.Json;
using SemanticSearchApi.Tools.Base;
using SemanticSearchApi.Interfaces;

namespace SemanticSearchApi.Tools
{
    public class SqlQueryTool : SemanticSearchTool
    {
        private readonly ISqlQueryExecutor _executor;
        
        public override string ToolType => "sql_search";

        public SqlQueryTool(ISqlQueryExecutor executor) 
            : base("sql_query", "Executes SQL queries against Student database")
        {
            _executor = executor;
        }

        protected override async Task<object> ExecuteAsync(string input)
        {
            try
            {
                var result = await _executor.ExecuteAsync(input);
                return result;
            }
            catch (Exception ex)
            {
                return new
                {
                    success = false,
                    error = ex.Message
                };
            }
        }
    }
}