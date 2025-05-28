// Api/SqlSearchController.cs
using Microsoft.AspNetCore.Mvc;
using SemanticSearchApi.Core;
using SemanticSearchApi.Interfaces;

namespace SemanticSearchApi.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class SqlSearchController : ControllerBase
    {
        private readonly SqlSearchOrchestrator _orchestrator;
        private readonly ISqlQueryExecutor _executor;
        private readonly ILogger<SqlSearchController> _logger;

        public SqlSearchController(
            SqlSearchOrchestrator orchestrator,
            ISqlQueryExecutor executor,
            ILogger<SqlSearchController> logger)
        {
            _orchestrator = orchestrator;
            _executor = executor;
            _logger = logger;
        }

        [HttpPost("query")]
        public async Task<IActionResult> Query([FromBody] QueryRequest request)
        {
            try
            {
                var result = await _orchestrator.HandleUserQueryAsync(request.Query, request.SessionId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing SQL query");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [HttpPost("direct")]
        public async Task<IActionResult> DirectSqlQuery([FromBody] DirectSqlRequest request)
        {
            try
            {
                // For testing - execute SQL directly
                var result = await _executor.ExecuteAsync(request.Sql);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing direct SQL query");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [HttpGet("samples")]
        public IActionResult GetSampleQueries()
        {
            var samples = new[]
            {
                "Show all students who study Maths",
                "What is the average grade by subject?",
                "How many students are there?",
                "Show top 5 students with highest grades",
                "Find students with grade above 4",
                "Show Emma Johnson's grades",
                "Which students are struggling with grade below 2?",
                "Count students by grade level",
                "Show all subjects for students with perfect grades",
                "What is the grade distribution?",
                "Find students who take all three subjects",
                "Show average grade for each student",
                "List students who excel in Science",
                "How many students failed (grade 1)?",
                "Show students studying English with grade 5"
            };

            return Ok(new { SampleQueries = samples });
        }

        public class QueryRequest
        {
            public string Query { get; set; }
            public string SessionId { get; set; }
        }

        public class DirectSqlRequest
        {
            public string Sql { get; set; }
        }
    }
}