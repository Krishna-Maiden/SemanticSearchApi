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

                // Step 3: Plan query with resolved company IDs
                var queryPlanner = _toolRegistry.GetTool("query_planner") as QueryPlannerTool;

                // Create a modified intent with resolved company IDs
                var modifiedIntent = JsonSerializer.Deserialize<UserIntent>(JsonSerializer.Serialize(intent));

                // Update the intent with resolved company IDs if available
                if (companyMap.ContainsKey("Exporter") && companyMap["Exporter"].Any())
                {
                    // Store the IDs in a way the query planner can use
                    modifiedIntent.CompanyMentions.Exporter = string.Join(",", companyMap["Exporter"]);
                }
                if (companyMap.ContainsKey("Importer") && companyMap["Importer"].Any())
                {
                    modifiedIntent.CompanyMentions.Importer = string.Join(",", companyMap["Importer"]);
                }

                // Pass the modified intent to the query planner
                var queryPlannerClass = _serviceProvider.GetRequiredService<IQueryPlanner>();
                var plannedQuery = await queryPlannerClass.PlanAsync(modifiedIntent, companyMap);

                // Step 4: Rewrite the query to replace company names with IDs
                string finalQuery = plannedQuery;
                if (!string.IsNullOrEmpty(plannedQuery))
                {
                    // Use the CompanyNameRewriter to replace names with IDs
                    finalQuery = await CompanyNameRewriter.ReplaceCompanyNamesWithIdsAsync(
                        plannedQuery,
                        async (name) =>
                        {
                            // Check if we already resolved this company
                            if (companyMap.TryGetValue("Exporter", out var exporterIds) &&
                                intent.CompanyMentions?.Exporter == name)
                            {
                                return exporterIds;
                            }
                            if (companyMap.TryGetValue("Importer", out var importerIds) &&
                                intent.CompanyMentions?.Importer == name)
                            {
                                return importerIds;
                            }

                            // If not already resolved, resolve it now
                            var companyResolver = _toolRegistry.GetTool("company_resolver");
                            var result = await companyResolver.InvokeAsync(name);
                            var resolved = JsonSerializer.Deserialize<CompanyResolverResult>(result);
                            return resolved?.resolved?.Values.FirstOrDefault() ?? new List<int>();
                        }
                    );
                }

                // Step 5: Execute search with the rewritten query
                string searchResult;
                if (!string.IsNullOrEmpty(finalQuery))
                {
                    var elasticsearchTool = _toolRegistry.GetTool("elasticsearch_search");
                    searchResult = await elasticsearchTool.InvokeAsync(finalQuery);
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

        // Add IServiceProvider to constructor
        private readonly IServiceProvider _serviceProvider;

        public LangChainOrchestrator(
            ToolRegistry toolRegistry,
            ILogger<LangChainOrchestrator> logger,
            IIntentAgent intentAgent,
            IChatMemory memory,
            IAnswerSynthesizer synthesizer,
            IServiceProvider serviceProvider)
        {
            _toolRegistry = toolRegistry;
            _logger = logger;
            _intentAgent = intentAgent;
            _memory = memory;
            _synthesizer = synthesizer;
            _serviceProvider = serviceProvider;
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