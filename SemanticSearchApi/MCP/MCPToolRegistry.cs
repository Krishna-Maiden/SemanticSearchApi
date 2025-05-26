using System.Text.Json;
using SemanticSearchApi.Tools;

namespace SemanticSearchApi.MCP
{
    public class MCPToolRegistry
    {
        private readonly ToolRegistry _toolRegistry;
        private readonly Dictionary<string, MCPToolDescriptor> _mcpTools;

        public MCPToolRegistry(ToolRegistry toolRegistry)
        {
            _toolRegistry = toolRegistry;
            _mcpTools = InitializeMCPTools();
        }

        private Dictionary<string, MCPToolDescriptor> InitializeMCPTools()
        {
            return new Dictionary<string, MCPToolDescriptor>
            {
                ["company_resolver"] = new MCPToolDescriptor
                {
                    Name = "company_resolver",
                    Description = "Resolves company names to database IDs",
                    Parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            companyName = new { type = "string", description = "Partial or full company name" }
                        },
                        required = new[] { "companyName" }
                    }
                },
                ["elasticsearch_search"] = new MCPToolDescriptor
                {
                    Name = "elasticsearch_search",
                    Description = "Executes Elasticsearch DSL queries",
                    Parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            query = new { type = "string", description = "Elasticsearch DSL query as JSON" }
                        },
                        required = new[] { "query" }
                    }
                },
                ["vector_search"] = new MCPToolDescriptor
                {
                    Name = "vector_search",
                    Description = "Semantic similarity search using embeddings",
                    Parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            query = new { type = "string", description = "Search query text" },
                            topN = new { type = "integer", description = "Number of results", @default = 10 },
                            threshold = new { type = "number", description = "Similarity threshold", @default = 0.25 }
                        },
                        required = new[] { "query" }
                    }
                }
            };
        }

        public async Task<object> GetToolManifest()
        {
            return _mcpTools.Values.Select(tool => new
            {
                name = tool.Name,
                description = tool.Description,
                parameters = tool.Parameters
            });
        }

        public async Task<object> InvokeTool(string toolName, JsonElement request)
        {
            var tool = _toolRegistry.GetTool(toolName);
            
            // Extract input based on tool type
            string input = toolName switch
            {
                "company_resolver" => request.GetProperty("companyName").GetString(),
                "elasticsearch_search" => request.GetProperty("query").GetString(),
                "vector_search" => FormatVectorSearchInput(request),
                _ => request.GetRawText()
            };

            var result = await tool.InvokeAsync(input);
            return JsonSerializer.Deserialize<object>(result);
        }

        private string FormatVectorSearchInput(JsonElement request)
        {
            var query = request.GetProperty("query").GetString();
            var topN = request.TryGetProperty("topN", out var topNElement) ? topNElement.GetInt32() : 10;
            var threshold = request.TryGetProperty("threshold", out var thresholdElement) ? thresholdElement.GetDouble() : 0.25;
            
            return $"{query}|{topN}|{threshold}";
        }
    }

    public class MCPToolDescriptor
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public object Parameters { get; set; }
    }
}