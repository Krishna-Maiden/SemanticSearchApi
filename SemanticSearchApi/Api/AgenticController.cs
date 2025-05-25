using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

[ApiController]
[Route("api/[controller]")]
public class AgenticController : ControllerBase
{
    private readonly AgenticSearchOrchestrator _orchestrator;

    public AgenticController(AgenticSearchOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [HttpPost("query")]
    public async Task<IActionResult> Query([FromBody] QueryRequest request)
    {
        var result = await _orchestrator.HandleUserQueryAsync(request.Query, request.SessionId);
        return Ok(new { Response = result });
    }

    public class QueryRequest
    {
        public string Query { get; set; }
        public string SessionId { get; set; }
    }
}
