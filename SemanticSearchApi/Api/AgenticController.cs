using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using SemanticSearchApi.LangChain;
using SemanticSearchApi.Tools;

namespace SemanticSearchApi.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class AgenticController : ControllerBase
    {
        private readonly AgenticSearchOrchestrator _originalOrchestrator;
        private readonly IAgenticOrchestrator _langChainOrchestrator;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AgenticController> _logger;

        public AgenticController(
            AgenticSearchOrchestrator originalOrchestrator,
            IAgenticOrchestrator langChainOrchestrator,
            IConfiguration configuration,
            ILogger<AgenticController> logger)
        {
            _originalOrchestrator = originalOrchestrator;
            _langChainOrchestrator = langChainOrchestrator;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost("query")]
        public async Task<IActionResult> Query([FromBody] QueryRequest request)
        {
            try
            {
                // Check if LangChain mode is enabled
                var useLangChain = _configuration.GetValue<bool>("Features:UseLangChain", false);

                string result;
                if (useLangChain)
                {
                    _logger.LogInformation("Using LangChain orchestrator for query");
                    result = await _langChainOrchestrator.HandleUserQueryAsync(
                        request.Query,
                        request.SessionId);
                }
                else
                {
                    _logger.LogInformation("Using original orchestrator for query");
                    result = await _originalOrchestrator.HandleUserQueryAsync(
                        request.Query,
                        request.SessionId);
                }

                return Ok(new
                {
                    Response = result,
                    Mode = useLangChain ? "LangChain" : "Original"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing query");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [HttpPost("query/langchain")]
        public async Task<IActionResult> QueryWithLangChain([FromBody] QueryRequest request)
        {
            try
            {
                var result = await _langChainOrchestrator.HandleUserQueryAsync(
                    request.Query,
                    request.SessionId);

                return Ok(new { Response = result, Mode = "LangChain" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing LangChain query");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [HttpPost("query/original")]
        public async Task<IActionResult> QueryWithOriginal([FromBody] QueryRequest request)
        {
            try
            {
                var result = await _originalOrchestrator.HandleUserQueryAsync(
                    request.Query,
                    request.SessionId);

                return Ok(new { Response = result, Mode = "Original" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing original query");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [HttpGet("tools")]
        public IActionResult GetAvailableTools([FromServices] ToolRegistry toolRegistry)
        {
            var tools = toolRegistry.GetToolNames();
            return Ok(new { Tools = tools });
        }

        public class QueryRequest
        {
            public string Query { get; set; }
            public string SessionId { get; set; }
        }
    }
}