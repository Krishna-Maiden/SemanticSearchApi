using LangChain.Abstractions.Schema;
using LangChain.Tools;

namespace SemanticSearchApi.Tools.Base
{
    public abstract class SemanticSearchTool : Tool
    {
        public abstract string ToolType { get; }
        
        protected SemanticSearchTool(string name, string description) 
            : base(name, description)
        {
        }

        public override async Task<string> InvokeAsync(string input)
        {
            try
            {
                var result = await ExecuteAsync(input);
                return System.Text.Json.JsonSerializer.Serialize(result);
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        protected abstract Task<object> ExecuteAsync(string input);
    }
}
