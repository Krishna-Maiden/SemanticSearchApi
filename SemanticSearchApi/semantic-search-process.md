# Semantic Search API - Process Documentation

## System Overview

The Semantic Search API is a hybrid intelligent search system that implements an **agentic architecture** for processing natural language queries across multiple data sources (PostgreSQL with vector embeddings and Elasticsearch). It leverages AI to understand user intent and provide contextual, conversational search capabilities.

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
- **Libraries**: NEST (Elasticsearch), Npgsql (PostgreSQL), Dapper, CsvHelper

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
- **Purpose**: Conversational agentic search
- **Request Body**:
  ```json
  {
    "query": "string",
    "sessionId": "string"
  }
  ```
- **Response**: Natural language answer with context

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
  }
}
```

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

## Future Enhancements

1. **Multi-language Support**: Extend beyond English queries
2. **Visual Analytics**: Generate charts from query results
3. **Export Capabilities**: Allow data export in various formats
4. **Advanced NLP**: Use more sophisticated intent recognition
5. **Federated Search**: Query multiple data sources simultaneously
6. **Real-time Updates**: Support streaming data ingestion
7. **User Feedback Loop**: Learn from user interactions
8. **Query Suggestions**: Provide autocomplete and suggestions