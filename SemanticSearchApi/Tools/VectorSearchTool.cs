using Pgvector;
using SemanticSearchApi.Tools.Base;

namespace SemanticSearchApi.Tools
{
    public class VectorSearchTool : SemanticSearchTool
    {
        private readonly PostgresDocumentRepository _repository;
        private readonly EmbeddingService _embeddingService;
        
        public override string ToolType => "search";

        public VectorSearchTool(PostgresDocumentRepository repository, EmbeddingService embeddingService) 
            : base("vector_search", "Performs semantic similarity search using vector embeddings")
        {
            _repository = repository;
            _embeddingService = embeddingService;
        }

        protected override async Task<object> ExecuteAsync(string input)
        {
            var queryParts = input.Split('|');
            var query = queryParts[0];
            var topN = queryParts.Length > 1 ? int.Parse(queryParts[1]) : 10;
            var threshold = queryParts.Length > 2 ? double.Parse(queryParts[2]) : 0.25;

            var embedding = await _embeddingService.GetEmbeddingAsync(query);
            var vector = new Vector(embedding);
            var results = await _repository.SearchTopNAsync(vector, topN, threshold);

            return new
            {
                query = query,
                matches = results.Count,
                results = results
            };
        }
    }
}
