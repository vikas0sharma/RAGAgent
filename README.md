# Personal Assistant - RAG Chat Agent

A .NET 10 chat agent that ingests URLs and PDFs, creates vector embeddings stored in **SQL Server 2025** using **EF Core 10**'s native vector support, and answers questions using Retrieval Augmented Generation (RAG) via **Microsoft Agent Framework (MAF)** with **Google Gemini**.

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     Chat Agent (MAF)                         │
│              ChatClientAgent + Google Gemini                 │
└──────────────────────────┬──────────────────────────────────┘
                           │
                    ┌──────▼──────┐
                    │  RAG Service │
                    └──────┬──────┘
                           │
          ┌────────────────┼────────────────┐
          │                │                │
  ┌───────▼───────┐ ┌─────▼─────┐ ┌────────▼────────┐
  │ Document      │ │ Embedding │ │ Vector Search   │
  │ Ingestion     │ │ Generator │ │ (SQL Server)    │
  │ (URL/PDF)     │ │ (Gemini)  │ │ EF Core 10      │
  └───────────────┘ └───────────┘ └─────────────────┘
```

## Technology Stack

| Component | Technology | What's New |
|-----------|-----------|------------|
| Framework | .NET 10 / ASP.NET Core Web API | Latest LTS release |
| Agent Framework | [Microsoft Agent Framework (MAF)](https://learn.microsoft.com/en-us/agent-framework/overview/?pivots=programming-language-csharp) — `Microsoft.Agents.AI` | Successor to Semantic Kernel & AutoGen, provides `ChatClientAgent` with built-in tool calling |
| LLM (Chat) | [Google Gemini](https://github.com/googleapis/dotnet-genai) — `gemini-2.5-flash` via `Google.GenAI` | Official .NET SDK with `IChatClient` integration via `Microsoft.Extensions.AI` |
| Embeddings | Google Gemini — `gemini-embedding-2` | 768-dimension embeddings for semantic search |
| Vector Store | [SQL Server 2025](https://learn.microsoft.com/en-us/sql/sql-server/what-s-new-in-sql-server-2025?view=sql-server-ver17) — native `vector` data type | New `vector(768)` column type, `VECTOR_DISTANCE()` function, and vector indexes for similarity search |
| ORM | [EF Core 10](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0/whatsnew) — `SqlVector<float>` | First-class vector search support with `EF.Functions.VectorDistance()` translated to SQL |
| PDF Parsing | `PdfPig` (MIT license) | Extract text from uploaded PDF documents |
| HTML/URL Parsing | `HtmlAgilityPack` | Extract text content from web pages |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [SQL Server 2025](https://www.microsoft.com/sql-server/sql-server-downloads) (with native vector support)
- A Google Gemini API key ([Get one here](https://ai.google.dev/))

## Getting Started

### 1. Clone the repository

```bash
git clone <repo-url>
cd PersonalAssistant
```

### 2. Configure the application

Update `PersonalAssistant/appsettings.json` with your settings:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=PersonalAssistantRAG;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Gemini": {
    "ApiKey": "YOUR_GEMINI_API_KEY",
    "ChatModel": "gemini-2.5-flash",
    "EmbeddingModel": "gemini-embedding-2"
  },
  "Rag": {
    "ChunkSize": 500,
    "ChunkOverlap": 50,
    "TopK": 5,
    "EmbeddingDimension": 768
  }
}
```

### 3. Run the application

```bash
cd PersonalAssistant
dotnet run
```

The application automatically applies EF Core migrations on startup, creating the database and required tables with vector columns.

## API Endpoints

### Chat

```http
POST /api/chat
Content-Type: application/json

{
  "message": "What does the document say about..."
}
```

The chat agent uses RAG to retrieve relevant context from ingested documents before generating a response via Gemini.

### Document Ingestion

**Ingest a URL:**

```http
POST /api/document/ingest/url
Content-Type: application/json

{
  "url": "https://example.com/article"
}
```

**Ingest a PDF:**

```http
POST /api/document/ingest/pdf
Content-Type: multipart/form-data

file: <your-pdf-file>
```

## How It Works

1. **Document Ingestion** — URLs and PDFs are parsed to extract text content.
2. **Text Chunking** — Extracted text is split into overlapping chunks (500 chars with 50 char overlap).
3. **Embedding Generation** — Each chunk is sent to Google Gemini's embedding model (`gemini-embedding-2`) to produce a 768-dimensional vector.
4. **Vector Storage** — Chunks and their embeddings are stored in SQL Server 2025 using EF Core 10's `SqlVector<float>` mapped to the native `vector(768)` column type.
5. **RAG Query** — When a user asks a question:
   - The question is embedded using the same model.
   - `VECTOR_DISTANCE('cosine', ...)` finds the top-K most similar chunks.
   - Relevant context is assembled and passed to the MAF `ChatClientAgent`.
6. **Agent Response** — The `ChatClientAgent` uses Gemini with the RAG context as a tool to generate a grounded answer, citing source documents.

## Project Structure

```
PersonalAssistant/
├── Program.cs                          # App startup, DI configuration
├── appsettings.json                    # Configuration
├── Agents/
│   └── RagChatAgent.cs                 # MAF ChatClientAgent with RAG tool
├── Controllers/
│   ├── ChatController.cs              # POST /api/chat
│   └── DocumentController.cs          # POST /api/document/ingest/*
├── Data/
│   ├── AppDbContext.cs                 # EF Core DbContext with vector support
│   └── Migrations/                     # EF Core migrations
├── Models/
│   ├── Document.cs                     # Document entity (source metadata)
│   ├── DocumentChunk.cs               # Chunk entity with SqlVector<float> embedding
│   └── RagOptions.cs                  # RAG configuration options
└── Services/
    ├── DocumentIngestionService.cs     # Orchestrates ingestion pipeline
    ├── GeminiEmbeddingService.cs       # Gemini-based embedding generation
    ├── PdfExtractor.cs                 # Extracts text from PDF files
    ├── UrlExtractor.cs                 # Extracts text from web pages
    ├── TextChunker.cs                  # Splits text into chunks
    └── RagService.cs                   # Vector similarity search + context building
```

## Key Technology Highlights

### SQL Server 2025 — Native Vector Support

SQL Server 2025 introduces a native `vector` data type optimized for AI workloads. Vectors are stored in an optimized binary format and support distance calculations via `VECTOR_DISTANCE()` using cosine, dot product, or Euclidean metrics — eliminating the need for external vector databases.

### EF Core 10 — SqlVector\<float\>

EF Core 10 provides first-class support for SQL Server 2025's vector type through `SqlVector<float>`. This enables LINQ queries with `EF.Functions.VectorDistance()` that translate directly to efficient SQL:

```csharp
var results = await db.DocumentChunks
    .OrderBy(c => EF.Functions.VectorDistance("cosine", c.Embedding, queryVector))
    .Take(5)
    .ToListAsync();
```

### Microsoft Agent Framework (MAF)

MAF is the successor to Semantic Kernel and AutoGen, combining simple agent abstractions with enterprise features. This project uses `ChatClientAgent` with tool-calling to integrate RAG retrieval as a function the agent can invoke autonomously.

### Google GenAI .NET SDK

The `Google.GenAI` package provides the official .NET SDK for Gemini, with native `IChatClient` integration via `Microsoft.Extensions.AI`. This enables seamless DI registration and compatibility with MAF's agent abstractions.

## References

- [What's New in SQL Server 2025](https://learn.microsoft.com/en-us/sql/sql-server/what-s-new-in-sql-server-2025?view=sql-server-ver17)
- [What's New in EF Core 10](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0/whatsnew)
- [Microsoft Agent Framework Overview](https://learn.microsoft.com/en-us/agent-framework/overview/?pivots=programming-language-csharp)
- [Google GenAI .NET SDK](https://github.com/googleapis/dotnet-genai)
