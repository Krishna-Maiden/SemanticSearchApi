# Migration to Tools Pattern Without LangChain

Since LangChain.NET is causing dependency issues, here's how to implement the Tools pattern and enhanced orchestration without it:

## Step 1: Update Project File

Replace your `SemanticSearchApi.csproj` with this minimal version:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net7.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <!-- Your existing packages -->
    <PackageReference Include="ClosedXML" Version="0.105.0" />
    <PackageReference Include="CsvHelper" Version="33.0.1" />
    <PackageReference Include="Dapper" Version="2.1.66" />
    <PackageReference Include="Microsoft.Data.SqlClient" Version="6.0.2" />
    <PackageReference Include="NEST" Version="7.17.5" />
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
    <PackageReference Include="Npgsql" Version="7.0.10" />
    <PackageReference Include="Pgvector" Version="0.1.1" />
  </ItemGroup>
</Project>
```

## Step 2: Create Tool Interfaces

Create `Tools/Base/ITool.cs`:

```csharp
namespace SemanticSearchApi.Tools.Base
{
    public interface ITool
    {
        string Name { get; }
        string Description { get; }
        Task<string> InvokeAsync(string input);
    }
}
```

## Step 3: Create Base Tool Class

Create `Tools/Base/SemanticSearchTool.cs`:

```csharp
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
```

## Step 4: Update All Tool Implementations

Update each tool to inherit from `SemanticSearchTool` instead of LangChain's Tool class. For example:

```csharp
using SemanticSearchApi.Tools.Base;

namespace SemanticSearchApi.Tools
{
    public class CompanyResolverTool : SemanticSearchTool
    {
        private readonly ICompanyResolverAgent _companyResolver;
        
        public override string ToolType => "resolver";

        public CompanyResolverTool(ICompanyResolverAgent companyResolver) 
            : base("company_resolver", "Resolves company names to their database IDs")
        {
            _companyResolver = companyResolver;
        }

        protected override async Task<object> ExecuteAsync(string input)
        {
            var companyMap = await _companyResolver.ResolveCompaniesAsync(input);
            return new
            {
                query = input,
                resolved = companyMap,
                count = companyMap.Values.Sum(v => v.Count)
            };
        }
    }
}
```

## Step 5: Use Simplified Orchestrator

The simplified `LangChainOrchestrator` I provided above works without LangChain dependencies but still provides the benefits of:
- Tool-based architecture
- Modular design
- Easy extensibility
- Session memory
- Intent-based processing

## Step 6: Update Program.cs

Use the fixed Program.cs that doesn't call `AddLangChain()`.

## Benefits of This Approach

1. **No External Dependencies**: Works with your existing packages
2. **Same Architecture**: Maintains the tool-based design
3. **Easy Migration Path**: When LangChain.NET stabilizes, you can easily migrate
4. **Full Control**: You own all the code and can customize as needed

## Testing

After making these changes:

1. Run `dotnet restore`
2. Run `dotnet build`
3. Test the endpoints:

```bash
# Test tools endpoint
GET https://localhost:7213/api/agentic/tools

# Test query with enhanced orchestrator
POST https://localhost:7213/api/agentic/query
{
  "query": "Show products from Global Spices",
  "sessionId": "test123"
}
```

This approach gives you all the architectural benefits without the dependency issues!