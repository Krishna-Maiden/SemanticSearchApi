using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Pgvector.Npgsql;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using SemanticSearchApi.Tools;
using SemanticSearchApi.Tools.Base;
using SemanticSearchApi.MCP;
using SemanticSearchApi.LangChain;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSingleton<QueryInterpretationService>();

// Add logging
builder.Services.AddLogging();

// Database Services
builder.Services.AddSingleton(provider =>
{
    var connectionString = builder.Configuration.GetConnectionString("PostgresConnection");
    var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
    dataSourceBuilder.UseVector();
    return dataSourceBuilder.Build();
});

// Core Services
builder.Services.AddSingleton<CsvDataService>();
builder.Services.AddSingleton<EmbeddingService>();
builder.Services.AddSingleton<PostgresDocumentRepository>();

// Original Agent Services (kept for compatibility)
builder.Services.AddSingleton<IIntentAgent, OpenAIIntentAgent>();
builder.Services.AddSingleton<ICompanyResolverAgent, CompanyResolverAgent>();
builder.Services.AddSingleton<IQueryPlanner, SimpleQueryPlanner>();
builder.Services.AddSingleton<IElasticQueryExecutor, ElasticQueryExecutor>();
builder.Services.AddSingleton<IAnswerSynthesizer, OpenAISummarizer>();
builder.Services.AddSingleton<IChatMemory, InMemoryChatMemory>();
builder.Services.AddSingleton<AgenticSearchOrchestrator>();

// LangChain Services - Manual configuration instead of AddLangChain
//builder.Services.AddSingleton<LangChainIntentAgent>();
builder.Services.AddSingleton<IAgenticOrchestrator, LangChainOrchestrator>();

// Tool Services
builder.Services.AddSingleton<CompanyResolverTool>();
builder.Services.AddSingleton<ElasticsearchTool>();
builder.Services.AddSingleton<VectorSearchTool>();
builder.Services.AddSingleton<QueryPlannerTool>();
builder.Services.AddSingleton<ToolRegistry>();

// MCP Services
builder.Services.AddSingleton<MCPToolRegistry>();
builder.Services.AddSingleton<MCPSchemaProvider>();

// NEST Elasticsearch Client
builder.Services.AddSingleton<Nest.IElasticClient>(provider =>
{
    var config = provider.GetRequiredService<IConfiguration>();
    var elasticUri = config["Elastic:Uri"];
    var username = config["Elastic:username"];
    var password = config["Elastic:password"];

    var settings = new Nest.ConnectionSettings(new Uri(elasticUri))
        .DefaultIndex("documents")
        .BasicAuthentication(username, password)
        .ServerCertificateValidationCallback((o, cert, chain, errors) => true);

    return new Nest.ElasticClient(settings);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Map MCP endpoints - these are discovered via .well-known pattern
app.MapControllerRoute(
    name: "mcp",
    pattern: ".well-known/mcp/{action}",
    defaults: new { controller = "MCPServer" });

app.Run();