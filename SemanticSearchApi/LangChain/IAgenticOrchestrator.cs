namespace SemanticSearchApi.LangChain
{
    public interface IAgenticOrchestrator
    {
        Task<string> HandleUserQueryAsync(string userInput, string sessionId);
    }
}
