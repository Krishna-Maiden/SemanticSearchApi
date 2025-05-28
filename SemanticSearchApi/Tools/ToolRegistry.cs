// Tools/ToolRegistry.cs
using Microsoft.Extensions.DependencyInjection;
using SemanticSearchApi.Tools.Base;

namespace SemanticSearchApi.Tools
{
    public class ToolRegistry
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<string, Type> _toolTypes;

        public ToolRegistry(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _toolTypes = new Dictionary<string, Type>
            {
                // Elasticsearch tools
                ["company_resolver"] = typeof(CompanyResolverTool),
                ["elasticsearch_search"] = typeof(ElasticsearchTool),
                ["vector_search"] = typeof(VectorSearchTool),
                ["query_planner"] = typeof(QueryPlannerTool),
                
                // SQL Server tools (NEW)
                ["sql_query"] = typeof(SqlQueryTool),
                ["sql_planner"] = typeof(SqlPlannerTool)
            };
        }

        public ITool GetTool(string toolName)
        {
            if (_toolTypes.TryGetValue(toolName, out var toolType))
            {
                return (ITool)_serviceProvider.GetRequiredService(toolType);
            }
            throw new ArgumentException($"Tool '{toolName}' not found");
        }

        public IEnumerable<ITool> GetAllTools()
        {
            return _toolTypes.Values.Select(type => (ITool)_serviceProvider.GetRequiredService(type));
        }

        public IEnumerable<string> GetToolNames()
        {
            return _toolTypes.Keys;
        }
        
        public IEnumerable<ITool> GetToolsByType(string toolType)
        {
            return GetAllTools().Where(tool => 
            {
                if (tool is SemanticSearchTool sst)
                {
                    return sst.ToolType == toolType;
                }
                return false;
            });
        }
    }
}