// LangChain/LangChainOrchestrator.cs
using SemanticSearchApi.Tools;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SemanticSearchApi.LangChain
{
    /// <summary>
    /// Simplified orchestrator that uses the Tools pattern without full LangChain dependency
    /// This provides a stepping stone toward full LangChain integration
    /// </summary>
    public class LangChainOrchestrator : IAgenticOrchestrator
    {
        private readonly ToolRegistry _toolRegistry;
        private readonly ILogger<LangChainOrchestrator> _logger;
        private readonly IIntentAgent _intentAgent;
        private readonly IChatMemory _memory;
        private readonly IAnswerSynthesizer _synthesizer;

        public LangChainOrchestrator(
            ToolRegistry toolRegistry,
            ILogger<LangChainOrchestrator> logger,
            IIntentAgent intentAgent,
            IChatMemory memory,
            IAnswerSynthesizer synthesizer)
        {
            _toolRegistry = toolRegistry;
            _logger = logger;
            _intentAgent = intentAgent;
            _memory = memory;
            _synthesizer = synthesizer;
        }

        public async Task<string> HandleUserQueryAsync(string userInput, string sessionId)
        {
            try
            {
                _logger.LogInformation($"Processing query with enhanced orchestrator: {userInput}");

                // Load session memory
                _memory.Load(sessionId);
                var context = _memory.GetContext();

                // Step 1: Extract intent
                var intent = await _intentAgent.InterpretAsync(userInput, context);
                _logger.LogDebug($"Extracted intent: {JsonSerializer.Serialize(intent)}");

                // Step 2: Resolve company names if mentioned
                Dictionary<string, List<int>> companyMap = new();
                if (intent.CompanyMentions != null)
                {
                    var companyResolver = _toolRegistry.GetTool("company_resolver");

                    if (!string.IsNullOrEmpty(intent.CompanyMentions.Exporter))
                    {
                        var result = await companyResolver.InvokeAsync(intent.CompanyMentions.Exporter);
                        var resolved = JsonSerializer.Deserialize<CompanyResolverResult>(result);
                        if (resolved?.resolved != null && resolved.resolved.ContainsKey(intent.CompanyMentions.Exporter))
                        {
                            companyMap["Exporter"] = resolved.resolved[intent.CompanyMentions.Exporter];
                        }
                        else
                        {
                            companyMap["Exporter"] = new List<int>();
                        }
                    }

                    if (!string.IsNullOrEmpty(intent.CompanyMentions.Importer))
                    {
                        var result = await companyResolver.InvokeAsync(intent.CompanyMentions.Importer);
                        var resolved = JsonSerializer.Deserialize<CompanyResolverResult>(result);
                        if (resolved?.resolved != null && resolved.resolved.ContainsKey(intent.CompanyMentions.Importer))
                        {
                            companyMap["Importer"] = resolved.resolved[intent.CompanyMentions.Importer];
                        }
                        else
                        {
                            companyMap["Importer"] = new List<int>();
                        }
                    }
                }

                // Step 3: Plan query
                var queryPlanner = _toolRegistry.GetTool("query_planner") as QueryPlannerTool;
                var plannerInput = JsonSerializer.Serialize(intent);
                var plannedQueryJson = await queryPlanner.InvokeAsync(plannerInput);
                var plannedQuery = JsonSerializer.Deserialize<QueryPlanResult>(plannedQueryJson);

                // Step 4: Execute search
                string searchResult;
                if (!string.IsNullOrEmpty(plannedQuery?.generatedDsl))
                {
                    var elasticsearchTool = _toolRegistry.GetTool("elasticsearch_search");
                    searchResult = await elasticsearchTool.InvokeAsync(plannedQuery.generatedDsl);
                }
                else
                {
                    // Fallback to vector search
                    var vectorSearchTool = _toolRegistry.GetTool("vector_search");
                    searchResult = await vectorSearchTool.InvokeAsync($"{userInput}|10|0.25");
                }

                // Step 5: Parse results
                var searchResultObj = JsonSerializer.Deserialize<JsonElement>(searchResult);

                // Step 6: Synthesize response
                var response = _synthesizer.Summarize(searchResultObj, intent);

                // Step 7: Update memory
                _memory.UpdateContext(userInput, response);
                _memory.Save(sessionId);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in enhanced orchestrator");
                return $"I encountered an error while processing your request: {ex.Message}";
            }
        }

        // Helper classes for deserialization
        private class CompanyResolverResult
        {
            public string query { get; set; }
            public Dictionary<string, List<int>> resolved { get; set; }
            public int count { get; set; }
        }

        private class QueryPlanResult
        {
            public UserIntent intent { get; set; }
            public string generatedDsl { get; set; }
        }
    }
}