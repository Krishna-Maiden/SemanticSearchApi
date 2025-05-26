# Semantic Search API - Process Documentation (v2.0 with LangChain & MCP)

## System Overview

The Semantic Search API is a hybrid intelligent search system that implements an **agentic architecture** for processing natural language queries across multiple data sources (PostgreSQL with vector embeddings and Elasticsearch). It leverages AI to understand user intent and provide contextual, conversational search capabilities.

**Version 2.0 Updates:**
- Integrated LangChain.NET for standardized agent orchestration
- Added Model Context Protocol (MCP) for tool discovery
- Implemented Tools pattern for modular functionality
- Dual-mode operation (Original vs LangChain)

## Architecture Overview

### System Design
- **Hybrid Search System**: Supports both vector similarity search (PostgreSQL + pgvector) and traditional search (Elasticsearch)
- **Agentic Design**: Modular agents handle different aspects of query processing
- **AI-Powered**: Uses OpenAI for query interpretation, intent extraction, and response synthesis
- **Conversational**: Maintains context across user sessions for coherent multi-turn interactions

### Technology Stack
- **Backend**: ASP.NET Core 7.0
- **Databases**: PostgreSQL (with pgvector extension), Elasticsearch
- **AI/ML**: OpenAI API (embeddings, GPT-4)
- **Libraries**: 
  - NEST (Elasticsearch)
  - Npgsql (PostgreSQL)
  - Dapper
  - CsvHelper
  - **LangChain.NET** (v0.12.3) - Agent orchestration
  - **Microsoft.SemanticKernel** - AI orchestration support

## New Architecture Components (v2.0)

### 1. LangChain Integration

#### LangChainOrchestrator
- **Location**: `LangChain/LangChainOrchestrator.cs`
- **Purpose**: Replaces original orchestrator with LangChain's agent system
- **Features**:
  - ReAct agent pattern for reasoning
  - Automatic tool selection
  - Built-in conversation memory
  - Verbose logging for debugging

#### LangChainIntentAgent
- **Location**: `LangChain/LangChainIntentAgent.cs`
- **Purpose**: Intent extraction using LangChain's LLMChain
- **Advantages**: 
  - Structured prompt templates
  - Automatic retry logic
  - Better error handling

### 2. Tools Framework

#### Tool Implementations
All tools inherit from `SemanticSearchTool` base class:

1. **CompanyResolverTool** (`Tools/CompanyResolverTool.cs`)
   - Resolves company names to database IDs
   - Returns mapping with counts

2. **ElasticsearchTool** (`Tools/ElasticsearchTool.cs`)
   - Executes Elasticsearch DSL queries
   - Returns hit count and results

3. **VectorSearchTool** (`Tools/VectorSearchTool.cs`)
   - Semantic similarity search
   - Configurable top-N and threshold

4. **QueryPlannerTool** (`Tools/QueryPlannerTool.cs`)
   - Generates Elasticsearch DSL from intent
   - Handles complex query construction

#### ToolRegistry
- **Location**: `Tools/ToolRegistry.cs`
- **Purpose**: Service locator for tools
- **Methods**:
  - `GetTool(name)`: Retrieve specific tool
  - `GetAllTools()`: Get all registered tools
  - `GetToolNames()`: List available tools

### 3. Model Context Protocol (MCP)

#### MCPServer
- **Location**: `MCP/MCPServer.cs`
- **Endpoints**:
  - `GET /.well-known/mcp/manifest` - Tool and resource discovery
  - `POST /.well-known/mcp/invoke/{toolName}` - Tool invocation
  - `GET /.well-known/mcp/resources/{resourceName}` - Schema/data access

#### MCPToolRegistry
- **Location**: `MCP/MCPToolRegistry.cs`
- **Purpose**: MCP-compliant tool descriptions
- **Features**:
  - JSON Schema parameter definitions
  - Tool manifest generation
  - Input transformation for tools

#### MCPSchemaProvider
- **Location**: `MCP/MCPSchemaProvider.cs`
- **Resources Exposed**:
  - `elasticsearch_indices`: Index mappings
  - `postgresql_tables`: Table schemas
  - `company_mappings`: Name to ID mappings
  - `product_catalog`: Product variations

## Core Components

### 1. Agents

#### IntentAgent (OpenAIIntentAgent)
- **Purpose**: Extracts structured intent from natural language queries
- **Input**: User query + conversation context
- **Output**: `UserIntent` object containing:
  - Raw query
  - Focus field (what to return)
  - Chart type (if visualization needed)
  - Time filter
  - Product mentions
  - Company mentions (exporter/importer)

#### CompanyResolverAgent
- **Purpose**: Resolves partial company names to their database IDs
- **Input**: Company name string
- **Output**: Dictionary mapping company names to lists of matching IDs
- **Method**: Wildcard search in Elasticsearch `globalcompanies` index

#### QueryPlanner (SimpleQueryPlanner)
- **Purpose**: Generates Elasticsearch DSL queries based on structured intent
- **Input**: UserIntent + resolved company IDs
- **Output**: Elasticsearch query DSL as JSON string
- **Features**:
  - Handles company ID filtering
  - Product description matching
  - Dynamic field selection

#### ElasticQueryExecutor
- **Purpose**: Executes queries against Elasticsearch
- **Input**: Query DSL string
- **Output**: Raw Elasticsearch response as JsonElement
- **Authentication**: Basic auth with credentials from config

#### AnswerSynthesizer
- **Two Implementations**:
  1. **BasicAnswerSynthesizer**: Simple extraction and formatting
  2. **OpenAISummarizer**: Uses GPT-4 to generate natural language summaries
- **Purpose**: Converts raw search results into human-readable responses
- **Input**: Elasticsearch results + user intent
- **Output**: Formatted answer string

#### ChatMemory (InMemoryChatMemory)
- **Purpose**: Maintains conversation history per session
- **Storage**: In-memory dictionary (can be extended to Redis)
- **Features**:
  - Stores user-bot message pairs
  - Provides context for multi-turn conversations

### 2. Core Services

#### AgenticSearchOrchestrator
- **Purpose**: Coordinates all agents in the search pipeline
- **Process Flow**:
  1. Load session memory
  2. Extract intent from user input
  3. Plan initial query
  4. Resolve company names to IDs
  5. Rewrite query with resolved IDs
  6. Execute query
  7. Synthesize response
  8. Update memory

#### QueryInterpretationService
- **Purpose**: Converts natural language to SQL or Elasticsearch DSL
- **Features**:
  - Database-aware (PostgreSQL vs Elasticsearch)
  - Synonym handling
  - Chart type detection
  - Product/company name mapping

#### EmbeddingService
- **Purpose**: Generates vector embeddings using OpenAI
- **Model**: text-embedding-ada-002
- **Used for**: Semantic similarity search in PostgreSQL

## Data Flow

```
1. User Query Reception
   ↓
2. Session Context Loading
   ↓
3. Intent Extraction (OpenAI)
   ↓
4. Query Planning
   ↓
5. Company Name Resolution
   ↓
6. Query Rewriting
   ↓
7. Query Execution
   ↓
8. Response Synthesis
   ↓
9. Memory Update
   ↓
10. Response Delivery
```

## Data Models

### Document Schema

#### PostgreSQL (documents table)
```
- id: UUID
- transaction_id: string
- exporter_name: string
- product_name: string
- price_in_inr: float
- product_type: string
- embedding: vector(1536)
```

#### Elasticsearch (documents index)
```
- documentId: GUID
- parentGlobalExporterId: int
- parentGlobalImporterId: int
- productDesc: string
- productDescription: string
- productDescEnglish: string
- countryId: int
- unitPrice: double
```

### Company Mappings

#### Exporters
- 1: Oceanic Tea House Pvt. Ltd.
- 3: Saffron Valley Traders
- 5: Trident Agro Exports
- 7: Evergreen Beverages Co
- 9: Global Spices Ltd

#### Importers
- 2: Wellness World Trade Co
- 4: Leaf & Bean Imports
- 6: SavorLine Global Imports
- 8: United Natural Goods Inc
- 10: EuroFlora Essentials
- 12: Bluewave Imports Ltd
- 14: FreshMart International

### Product Catalog
- Lemon Soda
- Blueberry Soda
- Mehandi (Henna)
- Green Tea
- Red Label Tea
- Coffee

## API Endpoints

### 1. Search Endpoints

#### POST /api/search/query
- **Purpose**: Main search endpoint supporting multiple backends
- **Request Body**:
  ```json
  {
    "query": "string",
    "topN": 100000000,
    "threshold": 0.25
  }
  ```
- **Response**: Results from SQL, Elasticsearch, or vector search

#### POST /api/agentic/query
- **Purpose**: Conversational agentic search (supports both modes)
- **Request Body**:
  ```json
  {
    "query": "string",
    "sessionId": "string"
  }
  ```
- **Response**: 
  ```json
  {
    "response": "Natural language answer",
    "mode": "LangChain" | "Original"
  }
  ```

#### POST /api/agentic/query/langchain
- **Purpose**: Force LangChain mode
- **Same request/response as above**

#### POST /api/agentic/query/original
- **Purpose**: Force original orchestrator mode
- **Same request/response as above**

#### GET /api/agentic/tools
- **Purpose**: List available tools
- **Response**: 
  ```json
  {
    "tools": ["company_resolver", "elasticsearch_search", "vector_search", "query_planner"]
  }
  ```

### 2. MCP Endpoints

#### GET /.well-known/mcp/manifest
- **Purpose**: MCP discovery endpoint
- **Response**:
  ```json
  {
    "name": "SemanticSearchAPI",
    "version": "1.0.0",
    "description": "...",
    "tools": [...],
    "resources": [...]
  }
  ```

#### POST /.well-known/mcp/invoke/{toolName}
- **Purpose**: Invoke tool via MCP
- **Request**: Tool-specific parameters
- **Response**: Tool execution result

#### GET /.well-known/mcp/resources/{resourceName}
- **Purpose**: Get schema or data resources
- **Resources**: 
  - `elasticsearch_indices`
  - `postgresql_tables`
  - `company_mappings`
  - `product_catalog`

### 2. Data Management

#### GET /api/search/init
- **Purpose**: Initialize vector embeddings for CSV data
- **Process**: Reads CSV, generates embeddings, stores in PostgreSQL

#### POST /api/search/import/excel-to-elasticsearch
- **Purpose**: Import Excel data to Elasticsearch
- **File**: `sample_export_import_data.xlsx`
- **Bulk imports**: Documents with all fields

## Query Processing Examples

### Example 1: Product Search
**Query**: "Show unit price for Global Spices selling Henna"
**Process**:
1. Intent: Focus on unitPrice, company="Global Spices", product="Henna"
2. Resolve: "Global Spices" → ID: 9, "Henna" → "Mehandi"
3. Generate DSL: Filter by parentGlobalExporterId=9 and productDesc="Mehandi"
4. Return: Unit prices from matching documents

### Example 2: Top N Query
**Query**: "Top 5 documents for Mehandi"
**Process**:
1. Intent: Limit=5, product="Mehandi"
2. Generate DSL: Match productDesc="Mehandi", size=5
3. Return: First 5 matching documents

### Example 3: Aggregation
**Query**: "How many cool drink transactions"
**Process**:
1. Intent: Count query, product_type="Cool Drink"
2. Generate SQL: `SELECT COUNT(*) FROM documents WHERE product_type='Cool Drink'`
3. Return: Numeric count

## Known Issues & TODOs

### Current Issues
1. **Elasticsearch Query Parsing**: DSL format needs fixing for proper query structure
2. **Company Name Resolution**: Integration with query rewriting needs improvement
3. **Sample Data**: Need to create Excel file with 10k rows of sample data

### Enhancement Opportunities
1. **Persistent Memory**: Move from in-memory to Redis for chat history
2. **Query Optimization**: Cache frequently resolved company names
3. **Error Handling**: Improve fallback strategies for failed queries
4. **Multi-Index Search**: Support searching across multiple Elasticsearch indices
5. **Advanced Aggregations**: Support complex analytics queries

## Configuration

### appsettings.json Structure
```json
{
  "ConnectionStrings": {
    "PostgresConnection": "Host=localhost;Database=semantic_search;..."
  },
  "OpenAI": {
    "ApiKey": "sk-..."
  },
  "Elastic": {
    "Uri": "https://localhost:9200",
    "ApiKey": "...",
    "username": "...",
    "password": "..."
  },
  "Database": {
    "Type": "elasticsearch" // or "postgresql"
  },
  "Features": {
    "UseLangChain": true,      // Enable LangChain mode
    "EnableMCP": true,         // Enable MCP endpoints
    "EnableTools": true        // Enable tools framework
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
```

## Migration Guide

### Phase 1: Parallel Operation (Weeks 1-2)
1. Deploy with both orchestrators active
2. Use feature flag to switch between modes
3. Compare results and performance

### Phase 2: Tool Migration (Weeks 3-4)
1. Migrate agents to tool pattern
2. Test individual tools via MCP
3. Update client code to use tools

### Phase 3: Full LangChain Adoption (Weeks 5-6)
1. Default to LangChain mode
2. Deprecate original orchestrator
3. Remove legacy code paths

### Phase 4: Advanced Features (Weeks 7-8)
1. Add more sophisticated agents
2. Implement tool chaining
3. Add external tool support

## Security Considerations

1. **API Keys**: Store securely, never commit to source control
2. **SQL Injection**: Using parameterized queries
3. **Elasticsearch**: Using authentication and SSL
4. **Input Validation**: Sanitize user queries before processing

## Performance Optimization

1. **Caching**: Consider caching company name resolutions
2. **Bulk Operations**: Use bulk API for Elasticsearch imports
3. **Connection Pooling**: Reuse database connections
4. **Async Operations**: All I/O operations are async
5. **Vector Indexing**: Ensure proper indexing on embedding columns

## Monitoring & Logging

### Key Metrics to Track
1. Query response times
2. Agent execution times
3. API success/failure rates
4. Memory usage per session
5. OpenAI API usage and costs

### Recommended Logging
1. All user queries and intents
2. Elasticsearch DSL queries generated
3. Company name resolution results
4. Error scenarios and fallbacks
5. Session lifecycle events

## Deployment Considerations

1. **Environment Variables**: Use for sensitive configuration
2. **Health Checks**: Implement for all external dependencies
3. **Scaling**: Stateless design allows horizontal scaling
4. **Database Migrations**: Version control schema changes
5. **API Versioning**: Plan for backward compatibility

## Testing the New Features

### 1. Test LangChain Mode
```bash
curl -X POST https://localhost:7213/api/agentic/query/langchain \
  -H "Content-Type: application/json" \
  -d '{
    "query": "Show unit price for Global Spices selling Henna",
    "sessionId": "test-123"
  }'
```

### 2. Test MCP Discovery
```bash
curl https://localhost:7213/.well-known/mcp/manifest
```

### 3. Test Tool Invocation via MCP
```bash
curl -X POST https://localhost:7213/.well-known/mcp/invoke/company_resolver \
  -H "Content-Type: application/json" \
  -d '{
    "companyName": "Global Spices"
  }'
```

### 4. Test Resource Access
```bash
curl https://localhost:7213/.well-known/mcp/resources/company_mappings
```

## Benefits of the New Architecture

### 1. **Standardization**
- Common patterns across all agents via Tools
- LangChain provides industry-standard agent patterns
- MCP enables cross-system interoperability

### 2. **Flexibility**
- Easy to add new tools without changing core logic
- Switch between orchestrators with feature flags
- External systems can discover and use your tools

### 3. **Observability**
- LangChain's built-in logging and tracing
- Tool execution metrics
- Session-based conversation tracking

### 4. **Composability**
- Chain multiple tools together
- Create complex workflows
- Reuse tools in different contexts

### 5. **Future-Proofing**
- Ready for LangChain ecosystem growth
- MCP adoption by major AI providers
- Easy integration with other AI systems

## Troubleshooting

### Common Issues

1. **"Tool not found" errors**
   - Ensure tool is registered in `Program.cs`
   - Check tool name matches exactly
   - Verify DI container registration

2. **LangChain mode not working**
   - Check `Features:UseLangChain` is `true`
   - Verify OpenAI API key is valid
   - Check logs for detailed errors

3. **MCP endpoints returning 404**
   - Ensure MCP routing is configured
   - Check `MCP:Enabled` is `true`
   - Verify URL format: `/.well-known/mcp/...`

4. **Memory not persisting**
   - Current implementation is in-memory
   - Implement Redis backend for persistence
   - Check session ID consistency

## Next Steps

1. **Production Readiness**
   - Add Redis for distributed memory
   - Implement rate limiting
   - Add comprehensive error handling

2. **Enhanced Tools**
   - SQL generation tool
   - Data visualization tool
   - Export/report generation tool

3. **Advanced LangChain Features**
   - Custom agent types
   - Tool validation
   - Fallback strategies

4. **MCP Extensions**
   - Authentication for external access
   - Tool versioning
   - Resource caching

This updated architecture provides a modern, extensible foundation for semantic search that can grow with your needs and integrate with the broader AI ecosystem.