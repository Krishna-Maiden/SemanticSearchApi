using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Pgvector.Npgsql;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSingleton<QueryInterpretationService>();
//builder.Services.AddSwaggerGen();

// Custom Services
builder.Services.AddSingleton(provider =>
{
    var connectionString = builder.Configuration.GetConnectionString("PostgresConnection");
    var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
    dataSourceBuilder.UseVector(); // ✅ This replaces old connection.TypeMapper
    return dataSourceBuilder.Build();
});

builder.Services.AddSingleton<CsvDataService>();
builder.Services.AddSingleton<EmbeddingService>();
builder.Services.AddSingleton<PostgresDocumentRepository>();

builder.Services.AddSingleton<IIntentAgent, OpenAIIntentAgent>();
builder.Services.AddSingleton<ICompanyResolverAgent, CompanyResolverAgent>();
builder.Services.AddSingleton<IQueryPlanner, SimpleQueryPlanner>();
builder.Services.AddSingleton<IElasticQueryExecutor, ElasticQueryExecutor>();
//services.AddSingleton<IAnswerSynthesizer, BasicAnswerSynthesizer>();
builder.Services.AddSingleton<IAnswerSynthesizer, OpenAISummarizer>();
builder.Services.AddSingleton<IChatMemory, InMemoryChatMemory>();
builder.Services.AddSingleton<AgenticSearchOrchestrator>();

var app = builder.Build();

/*
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}*/

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
