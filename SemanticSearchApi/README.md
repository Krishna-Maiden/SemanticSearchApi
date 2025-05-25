# Agentic Console System

This solution demonstrates a fully modular, agentic search system designed to handle natural language queries over Elasticsearch using OpenAI.

## 📂 Project Structure

- `Agents/` — Implementations of modular services (Intent detection, planning, execution, etc.)
- `Interfaces/` — Core interfaces each agent implements
- `Core/` — The orchestrator coordinating agent interactions
- `Memory/` — Conversation memory handling
- `Api/` — ASP.NET Controller for HTTP interface
- `ConsoleApp/` — Program entry point + project file

## ✅ Features

- Conversational context (memory)
- Company name to ID resolution via Elastic
- DSL planning and execution
- Response summarization
- OpenAI-powered interpretation agent

## 🚀 How to Run

```bash
cd ConsoleApp
dotnet build
dotnet run
```

Or call the `AgenticController` from Postman using:
```
POST /api/agentic/query
{
  "query": "Show unit price for Global Spices selling Henna",
  "sessionId": "user123"
}
```
