// Tools/SqlPlannerTool.cs
using System.Text.Json;
using SemanticSearchApi.Tools.Base;
using SemanticSearchApi.Interfaces;

namespace SemanticSearchApi.Tools
{
    public class SqlPlannerTool : SemanticSearchTool
    {
        private readonly ISqlQueryPlanner _planner;
        
        public override string ToolType => "sql_planner";

        public SqlPlannerTool(ISqlQueryPlanner planner) 
            : base("sql_planner", "Plans SQL queries from user intent for Student data")
        {
            _planner = planner;
        }

        protected override async Task<object> ExecuteAsync(string input)
        {
            try
            {
                // Parse input as UserIntent
                var intent = JsonSerializer.Deserialize<UserIntent>(input);
                var sql = await _planner.PlanSqlAsync(intent);
                
                return new
                {
                    intent = intent,
                    generatedSql = sql,
                    success = true
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