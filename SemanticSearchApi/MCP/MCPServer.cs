using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace SemanticSearchApi.MCP
{
    [ApiController]
    [Route(".well-known/mcp")]
    public class MCPServer : ControllerBase
    {
        private readonly MCPToolRegistry _toolRegistry;
        private readonly MCPSchemaProvider _schemaProvider;

        public MCPServer(MCPToolRegistry toolRegistry, MCPSchemaProvider schemaProvider)
        {
            _toolRegistry = toolRegistry;
            _schemaProvider = schemaProvider;
        }

        [HttpGet("manifest")]
        public async Task<IActionResult> GetManifest()
        {
            var manifest = new
            {
                name = "SemanticSearchAPI",
                version = "1.0.0",
                description = "Semantic search with Elasticsearch and PostgreSQL vector search",
                tools = await _toolRegistry.GetToolManifest(),
                resources = await _schemaProvider.GetResourceManifest()
            };

            return Ok(manifest);
        }

        [HttpPost("invoke/{toolName}")]
        public async Task<IActionResult> InvokeTool(string toolName, [FromBody] JsonElement request)
        {
            try
            {
                var result = await _toolRegistry.InvokeTool(toolName, request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("resources/{resourceName}")]
        public async Task<IActionResult> GetResource(string resourceName)
        {
            var resource = await _schemaProvider.GetResource(resourceName);
            if (resource == null)
                return NotFound();
            
            return Ok(resource);
        }
    }
}