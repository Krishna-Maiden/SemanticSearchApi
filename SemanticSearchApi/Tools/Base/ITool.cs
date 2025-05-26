namespace SemanticSearchApi.Tools.Base
{
    public interface ITool
    {
        string Name { get; }
        string Description { get; }
        Task<string> InvokeAsync(string input);
    }
}