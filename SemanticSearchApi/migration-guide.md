# Migration Guide: Upgrading to LangChain.NET and MCP

This guide provides step-by-step instructions for migrating your Semantic Search API to use LangChain.NET, Model Context Protocol (MCP), and the Tools pattern.

## Prerequisites

- .NET 7.0 SDK installed
- Access to OpenAI API
- Elasticsearch and PostgreSQL running
- Basic understanding of dependency injection

## Step 1: Update Project Dependencies

1. Open `SemanticSearchApi.csproj`
2. Add the new package references:

```xml
<!-- Add these to your existing PackageReference items -->
<PackageReference Include="LangChain.Core" Version="0.12.3" />
<PackageReference Include="LangChain.Providers.OpenAI" Version="0.12.3" />
<PackageReference Include="LangChain.Memory.InMemory" Version="0.12.3" />
<PackageReference Include="LangChain.Extensions.DependencyInjection" Version="0.12.3" />
<PackageReference Include="Microsoft.SemanticKernel" Version="1.3.0" />
```

3. Run `dotnet restore`

## Step 2: Create New Folder Structure

Create the following new folders in your project:
- `Tools/`
- `Tools/Base/`
- `LangChain/`
- `MCP/`

## Step 3: Implement Tools

1. Create `Tools/Base/SemanticSearchTool.cs` (base class for all tools)
2. Create tool implementations:
   - `Tools/CompanyResolverTool.cs`
   - `Tools/ElasticsearchTool.cs`
   - `Tools/VectorSearchTool.cs`
   - `Tools/QueryPlannerTool.cs`
3. Create `Tools/ToolRegistry.cs`

## Step 4: Implement LangChain Components

1. Create `LangChain/IAgenticOrchestrator.cs` (interface)
2. Create `LangChain/LangChainOrchestrator.cs`
3. Create `LangChain/ReActAgent.cs`
4. Create `LangChain/LangChainIntentAgent.cs`

## Step 5: Implement MCP Components

1. Create `MCP/MCPServer.cs` (controller)
2. Create `MCP/MCPToolRegistry.cs`
3. Create `MCP/MCPSchemaProvider.cs`

## Step 6: Update Program.cs

1. Add new service registrations:

```csharp
// Add after existing service registrations

// LangChain Services
builder.Services.AddSingleton<LangChainIntentAgent>();
builder.Services.AddSingleton<IAgenticOrchestrator, LangChainOrchestrator>();

// Add LangChain
builder.Services.AddLangChain(options =>
{
    options.OpenAiApiKey = builder.Configuration["OpenAI:ApiKey"];
    options.DefaultModelName = "gpt-4";
});

// Tool Services
builder.Services.AddSingleton<CompanyResolverTool>();
builder.Services.AddSingleton<ElasticsearchTool>();
builder.Services.AddSingleton<VectorSearchTool>();
builder.Services.AddSingleton<QueryPlannerTool>();
builder.Services.AddSingleton<ToolRegistry>();

// MCP Services
builder.Services.AddSingleton<MCPToolRegistry>();
builder.Services.AddSingleton<MCPSchemaProvider>();
```

2. Add MCP routing:

```csharp
// Add before app.Run()
app.MapControllerRoute(
    name: "mcp",
    pattern: ".well-known/mcp/{action}",
    defaults: new { controller = "MCPServer" });
```

## Step 7: Update AgenticController

Replace the existing `AgenticController.cs` with the updated version that supports both modes.

## Step 8: Update Configuration

1. Update `appsettings.json` with new configuration sections:

```json
{
  "Features": {
    "UseLangChain": false,  // Start with false for testing
    "EnableMCP": true,
    "EnableTools": true
  },
  "LangChain": {
    "DefaultModel": "gpt-4",
    "Temperature": 0.7,
    "MaxTokens": 2000,
    "VerboseLogging": true
  },
  "MCP": {
    "Enabled": true,
    "ExposeAtWellKnown": true,
    "AllowExternalAccess": false
  },
  "Tools": {
    "CompanyResolver": {
      "MaxResults": 100,
      "FuzzyMatchThreshold": 0.7
    },
    "VectorSearch": {
      "DefaultTopN": 10,
      "DefaultThreshold": 0.25
    },
    "Elasticsearch": {
      "DefaultSize": 10,
      "Timeout": "30s"
    }
  }
}