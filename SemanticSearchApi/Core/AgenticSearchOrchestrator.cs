using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public class AgenticSearchOrchestrator
{
    private readonly IIntentAgent _intentAgent;
    private readonly ICompanyResolverAgent _companyAgent;
    private readonly IQueryPlanner _queryPlanner;
    private readonly IElasticQueryExecutor _executor;
    private readonly IAnswerSynthesizer _synthesizer;
    private readonly IChatMemory _memory;

    public AgenticSearchOrchestrator(
        IIntentAgent intentAgent,
        ICompanyResolverAgent companyAgent,
        IQueryPlanner queryPlanner,
        IElasticQueryExecutor executor,
        IAnswerSynthesizer synthesizer,
        IChatMemory memory)
    {
        _intentAgent = intentAgent;
        _companyAgent = companyAgent;
        _queryPlanner = queryPlanner;
        _executor = executor;
        _synthesizer = synthesizer;
        _memory = memory;
    }

    public async Task<string> HandleUserQueryAsync(string userInput, string sessionId)
    {
        _memory.Load(sessionId);
        var context = _memory.GetContext();

        // Step 1: Extract intent from user input
        var intent = await _intentAgent.InterpretAsync(userInput, context);

        // Step 2: Generate Elasticsearch DSL from structured intent
        // Temporarily pass empty company map to planner
        var emptyMap = new Dictionary<string, List<int>>();
        var plannedQuery = await _queryPlanner.PlanAsync(intent, emptyMap);

        // Step 3: Rewrite DSL by replacing company names with resolved company IDs
        Func<string, Task<List<int>>> resolver = async (string name) =>
        {
            var result = await _companyAgent.ResolveCompaniesAsync(name);
            return result.ContainsKey(name) ? result[name] : new List<int>();
        };
        plannedQuery = await CompanyNameRewriter.ReplaceCompanyNamesWithIdsAsync(plannedQuery, resolver);

        // Step 4: Execute the rewritten DSL against Elasticsearch
        var result = await _executor.ExecuteAsync(plannedQuery);

        // Step 5: Generate a human-readable response
        var response = _synthesizer.Summarize(result, intent);

        // Step 6: Save the interaction to memory
        _memory.UpdateContext(userInput, response);
        _memory.Save(sessionId);

        return response;
    }
}
