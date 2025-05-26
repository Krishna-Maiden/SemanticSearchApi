using System.Text.Json;
using SemanticSearchApi.Tools.Base;

namespace SemanticSearchApi.Tools
{
    public class ElasticsearchTool : SemanticSearchTool
    {
        private readonly IElasticQueryExecutor _executor;
        
        public override string ToolType => "search";

        public ElasticsearchTool(IElasticQueryExecutor executor) 
            : base("elasticsearch_search", "Executes Elasticsearch queries and returns results")
        {
            _executor = executor;
        }

        protected override async Task<object> ExecuteAsync(string input)
        {
            try
            {
                var result = await _executor.ExecuteAsync(input);
                return new
                {
                    success = true,
                    hits = result.GetProperty("hits").GetProperty("total").GetProperty("value").GetInt32(),
                    results = result
                };
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
