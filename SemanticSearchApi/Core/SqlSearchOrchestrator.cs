// Core/SqlSearchOrchestrator.cs (Updated with correction handling)
using System.Text.Json;
using SemanticSearchApi.Agents;
using SemanticSearchApi.Interfaces;
using SemanticSearchApi.Models;

namespace SemanticSearchApi.Core
{
    public class SqlSearchOrchestrator
    {
        private readonly IIntentAgent _intentAgent;
        private readonly ISqlQueryPlanner _queryPlanner;
        private readonly ISqlQueryExecutor _executor;
        private readonly SqlAnswerSynthesizer _synthesizer;
        private readonly IChatMemory _memory;
        private readonly ILogger<SqlSearchOrchestrator> _logger;

        public SqlSearchOrchestrator(
            IIntentAgent intentAgent,
            ISqlQueryPlanner queryPlanner,
            ISqlQueryExecutor executor,
            SqlAnswerSynthesizer synthesizer,
            IChatMemory memory,
            ILogger<SqlSearchOrchestrator> logger)
        {
            _intentAgent = intentAgent;
            _queryPlanner = queryPlanner;
            _executor = executor;
            _synthesizer = synthesizer;
            _memory = memory;
            _logger = logger;
        }

        public async Task<QueryResponse> HandleUserQueryAsync(string userInput, string sessionId)
        {
            var response = new QueryResponse();

            try
            {
                // Load session memory
                _memory.Load(sessionId);
                var context = _memory.GetContext();

                // Step 1: Extract intent from user input
                var intent = await _intentAgent.InterpretAsync(userInput, context);
                response.Intent = intent;
                _logger.LogInformation($"Extracted intent: {JsonSerializer.Serialize(intent)}");

                // Step 2: Generate SQL query from intent
                var sqlQuery = await _queryPlanner.PlanSqlAsync(intent);
                response.GeneratedQuery = sqlQuery;
                _logger.LogInformation($"Generated SQL: {sqlQuery}");

                // Step 3: Execute SQL query
                var result = await _executor.ExecuteAsync(sqlQuery);
                response.RawResults = result;
                response.Success = result.Success;

                // Step 4: Extract any corrections made during query generation
                if (result.Corrections.Any())
                {
                    response.Corrections = result.Corrections;
                    _logger.LogInformation($"Corrections applied: {string.Join("; ", result.Corrections)}");
                }

                if (!result.Success)
                {
                    response.ErrorMessage = result.Error;
                    response.Summary = $"Error executing query: {result.Error}";
                }
                else
                {
                    // Step 5: Generate human-readable response using OpenAI
                    response.Summary = await _synthesizer.SummarizeSqlResultsAsync(result, intent);

                    // Step 6: Save interaction to memory
                    _memory.UpdateContext(userInput, response.Summary);
                    _memory.Save(sessionId);
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SQL search orchestrator");
                response.Success = false;
                response.ErrorMessage = ex.Message;
                response.Summary = $"An error occurred while processing your request: {ex.Message}";
                return response;
            }
        }
    }
}