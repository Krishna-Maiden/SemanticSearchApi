using System.Text.Json;
using SemanticSearchApi.Tools.Base;

namespace SemanticSearchApi.Tools
{
    public class QueryPlannerTool : SemanticSearchTool
    {
        private readonly IQueryPlanner _queryPlanner;
        
        public override string ToolType => "planner";

        public QueryPlannerTool(IQueryPlanner queryPlanner) 
            : base("query_planner", "Plans and generates Elasticsearch DSL queries from user intent")
        {
            _queryPlanner = queryPlanner;
        }

        protected override async Task<object> ExecuteAsync(string input)
        {
            // Parse input as UserIntent JSON
            var intent = JsonSerializer.Deserialize<UserIntent>(input);
            var dsl = await _queryPlanner.PlanAsync(intent, new Dictionary<string, List<int>>());
            
            return new
            {
                intent = intent,
                generatedDsl = dsl
            };
        }
    }
}
