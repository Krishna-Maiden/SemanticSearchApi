namespace SemanticSearchApi.Tools.Base
{
    public abstract class SemanticSearchTool : ITool
    {
        public string Name { get; }
        public string Description { get; }
        public abstract string ToolType { get; }

        protected SemanticSearchTool(string name, string description)
        {
            Name = name;
            Description = description;
        }

        public async Task<string> InvokeAsync(string input)
        {
            try
            {
                var result = await ExecuteAsync(input);
                return System.Text.Json.JsonSerializer.Serialize(result);
            }
            catch (Exception ex)
            {
                return System.Text.Json.JsonSerializer.Serialize(new { error = ex.Message });
            }
        }

        protected abstract Task<object> ExecuteAsync(string input);
    }
}