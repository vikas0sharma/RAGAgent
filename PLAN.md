# RAG Chat Agent - Implementation Plan

## Overview
Build a .NET chat agent that ingests URLs/PDFs, creates vector embeddings stored in SQL Server 2025 using EF Core 10's native vector support, and answers questions using Retrieval Augmented Generation (RAG) via Microsoft Agent Framework (MAF) with Ollama or Gemini.

---

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     Chat Agent (MAF)                         │
│              ChatClientAgent + Ollama/Gemini                 │
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
  │ (URL/PDF)     │ │ (Ollama)  │ │ EF Core 10      │
  └───────────────┘ └───────────┘ └─────────────────┘
```

---

## Technology Stack

| Component | Technology |
|-----------|-----------|
| Framework | .NET 10 / ASP.NET Core Web API |
| Agent Framework | Microsoft Agent Framework (MAF) - `Microsoft.Agents.AI` |
| LLM (Chat) | Ollama (`llama3.2` or `phi4`) / Google Gemini (`gemini-2.5-flash`) |
| Embeddings | Ollama (`nomic-embed-text` or `all-minilm`) / Gemini embedding |
| ORM | EF Core 10 with `SqlVector<float>` support |
| Database | SQL Server 2025 (native `vector` data type + `VECTOR_DISTANCE()`) |
| PDF Parsing | `UglyToad.PdfPig` (MIT license) |
| HTML/URL Parsing | `HtmlAgilityPack` |

---

## Project Structure

```
PersonalAssistant/
├── Program.cs                          # App startup, DI configuration
├── appsettings.json                    # Config (connection strings, model settings)
├── Data/
│   ├── AppDbContext.cs                 # EF Core DbContext with vector support
│   └── Migrations/                     # EF Core migrations
├── Models/
│   ├── Document.cs                     # Document entity (source metadata)
│   └── DocumentChunk.cs               # Chunk entity with vector embedding
├── Services/
│   ├── IDocumentIngestionService.cs    # Interface for ingestion
│   ├── DocumentIngestionService.cs     # Orchestrates ingestion pipeline
│   ├── PdfExtractor.cs                # Extracts text from PDF files
│   ├── UrlExtractor.cs                # Extracts text from web pages
│   ├── TextChunker.cs                 # Splits text into chunks
│   ├── IEmbeddingService.cs           # Interface for embedding generation
│   ├── OllamaEmbeddingService.cs      # Ollama-based embeddings
│   ├── IRagService.cs                 # Interface for RAG retrieval
│   └── RagService.cs                  # Vector search + context building
├── Agents/
│   └── RagChatAgent.cs                # MAF agent with RAG-augmented responses
└── Controllers/
    ├── ChatController.cs              # Chat endpoint
    └── DocumentController.cs          # Document ingestion endpoint
```

---

## Phase 1: Database & EF Core Setup

### Step 1.1 - Install NuGet Packages
```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="10.0.0" />
```

### Step 1.2 - Define Entity Models

**Document.cs** - Stores document metadata:
```csharp
public class Document
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string Source { get; set; }         // URL or file path
    public required string SourceType { get; set; }     // "url" or "pdf"
    public DateTime IngestedAt { get; set; } = DateTime.UtcNow;
    public ICollection<DocumentChunk> Chunks { get; set; } = [];
}
```

**DocumentChunk.cs** - Stores text chunks with vector embeddings:
```csharp
using Microsoft.Data.SqlClient;

public class DocumentChunk
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public required string Content { get; set; }
    public int ChunkIndex { get; set; }

    [Column(TypeName = "vector(768)")]  // 768 for nomic-embed-text, 1536 for others
    public SqlVector<float> Embedding { get; set; }

    public Document Document { get; set; } = null!;
}
```

### Step 1.3 - Configure DbContext

```csharp
public class AppDbContext : DbContext
{
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DocumentChunk>(entity =>
        {
            entity.HasIndex(e => e.DocumentId);
            entity.HasOne(e => e.Document)
                  .WithMany(d => d.Chunks)
                  .HasForeignKey(e => e.DocumentId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
```

### Step 1.4 - Connection String (appsettings.json)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=PersonalAssistantRAG;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "ChatModel": "llama3.2",
    "EmbeddingModel": "nomic-embed-text"
  },
  "Gemini": {
    "ApiKey": "",
    "Model": "gemini-2.5-flash"
  },
  "Rag": {
    "ChunkSize": 500,
    "ChunkOverlap": 50,
    "TopK": 5,
    "EmbeddingDimension": 768
  }
}
```

---

## Phase 2: Document Ingestion Pipeline

### Step 2.1 - Install Parsing Packages
```xml
<PackageReference Include="UglyToad.PdfPig" Version="0.1.9" />
<PackageReference Include="HtmlAgilityPack" Version="1.11.72" />
```

### Step 2.2 - PDF Extractor
```csharp
public class PdfExtractor
{
    public string ExtractText(Stream pdfStream)
    {
        using var document = UglyToad.PdfPig.PdfDocument.Open(pdfStream);
        var sb = new StringBuilder();
        foreach (var page in document.GetPages())
        {
            sb.AppendLine(page.Text);
        }
        return sb.ToString();
    }
}
```

### Step 2.3 - URL Extractor
```csharp
public class UrlExtractor(HttpClient httpClient)
{
    public async Task<string> ExtractTextAsync(string url)
    {
        var html = await httpClient.GetStringAsync(url);
        var doc = new HtmlAgilityPack.HtmlDocument();
        doc.LoadHtml(html);

        // Remove script and style nodes
        doc.DocumentNode.SelectNodes("//script|//style")?
            .ToList().ForEach(n => n.Remove());

        return doc.DocumentNode.InnerText
            .Replace("\t", " ")
            .Replace("\r", "")
            .Trim();
    }
}
```

### Step 2.4 - Text Chunker
```csharp
public class TextChunker(IOptions<RagOptions> options)
{
    public List<string> ChunkText(string text)
    {
        var chunkSize = options.Value.ChunkSize;
        var overlap = options.Value.ChunkOverlap;
        var chunks = new List<string>();

        // Split by sentences/paragraphs, then assemble into chunks
        var sentences = text.Split(['.', '!', '?', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var current = new StringBuilder();

        foreach (var sentence in sentences)
        {
            if (current.Length + sentence.Length > chunkSize && current.Length > 0)
            {
                chunks.Add(current.ToString().Trim());
                // Keep overlap
                var overlapText = current.ToString();
                current.Clear();
                if (overlapText.Length > overlap)
                    current.Append(overlapText[^overlap..]);
            }
            current.Append(sentence.Trim()).Append(". ");
        }

        if (current.Length > 0)
            chunks.Add(current.ToString().Trim());

        return chunks;
    }
}
```

---

## Phase 3: Embedding Generation

### Step 3.1 - Install Ollama AI Package
```xml
<PackageReference Include="Microsoft.Extensions.AI.Ollama" Version="10.0.0-preview.*" />
<PackageReference Include="Microsoft.Extensions.AI" Version="10.0.0-preview.*" />
```

### Step 3.2 - Embedding Service (Ollama)
```csharp
public class OllamaEmbeddingService(
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator) : IEmbeddingService
{
    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        var embedding = await embeddingGenerator.GenerateAsync([text]);
        return embedding[0].Vector.ToArray();
    }

    public async Task<IList<float[]>> GenerateEmbeddingsAsync(IList<string> texts)
    {
        var embeddings = await embeddingGenerator.GenerateAsync(texts);
        return embeddings.Select(e => e.Vector.ToArray()).ToList();
    }
}
```

### Step 3.3 - Register Embedding Generator in DI
```csharp
// In Program.cs
builder.Services.AddEmbeddingGenerator(b =>
    b.Use(new OllamaEmbeddingGenerator(
        new Uri(builder.Configuration["Ollama:BaseUrl"]!),
        modelId: builder.Configuration["Ollama:EmbeddingModel"]!)));
```

---

## Phase 4: Document Ingestion Orchestration

### Step 4.1 - Ingestion Service
```csharp
public class DocumentIngestionService(
    AppDbContext db,
    IEmbeddingService embeddingService,
    PdfExtractor pdfExtractor,
    UrlExtractor urlExtractor,
    TextChunker textChunker) : IDocumentIngestionService
{
    public async Task<Document> IngestUrlAsync(string url)
    {
        var text = await urlExtractor.ExtractTextAsync(url);
        return await IngestTextAsync(text, url, "url", title: url);
    }

    public async Task<Document> IngestPdfAsync(Stream pdfStream, string fileName)
    {
        var text = pdfExtractor.ExtractText(pdfStream);
        return await IngestTextAsync(text, fileName, "pdf", title: fileName);
    }

    private async Task<Document> IngestTextAsync(string text, string source, string sourceType, string title)
    {
        var document = new Document
        {
            Title = title,
            Source = source,
            SourceType = sourceType
        };

        var chunks = textChunker.ChunkText(text);
        var embeddings = await embeddingService.GenerateEmbeddingsAsync(chunks);

        for (int i = 0; i < chunks.Count; i++)
        {
            document.Chunks.Add(new DocumentChunk
            {
                Content = chunks[i],
                ChunkIndex = i,
                Embedding = new SqlVector<float>(embeddings[i])
            });
        }

        db.Documents.Add(document);
        await db.SaveChangesAsync();
        return document;
    }
}
```

---

## Phase 5: RAG Retrieval Service

### Step 5.1 - Vector Similarity Search with EF Core 10
```csharp
public class RagService(
    AppDbContext db,
    IEmbeddingService embeddingService,
    IOptions<RagOptions> options) : IRagService
{
    public async Task<string> GetRelevantContextAsync(string query)
    {
        var queryEmbedding = await embeddingService.GenerateEmbeddingAsync(query);
        var sqlVector = new SqlVector<float>(queryEmbedding);
        var topK = options.Value.TopK;

        // Use EF Core 10 VectorDistance for cosine similarity search
        var relevantChunks = await db.DocumentChunks
            .OrderBy(c => EF.Functions.VectorDistance("cosine", c.Embedding, sqlVector))
            .Take(topK)
            .Select(c => new { c.Content, c.Document.Title })
            .ToListAsync();

        if (relevantChunks.Count == 0)
            return "No relevant documents found.";

        var contextBuilder = new StringBuilder();
        contextBuilder.AppendLine("Relevant context from ingested documents:");
        contextBuilder.AppendLine("---");
        foreach (var chunk in relevantChunks)
        {
            contextBuilder.AppendLine($"[Source: {chunk.Title}]");
            contextBuilder.AppendLine(chunk.Content);
            contextBuilder.AppendLine("---");
        }

        return contextBuilder.ToString();
    }
}
```

---

## Phase 6: Chat Agent with MAF

### Step 6.1 - Install MAF Package
```xml
<PackageReference Include="Microsoft.Agents.AI" Version="1.0.0-*" />
<PackageReference Include="Microsoft.Extensions.AI.Ollama" Version="10.0.0-preview.*" />
<!-- OR for Gemini: -->
<PackageReference Include="Google.GenAI" Version="1.0.0-*" />
```

### Step 6.2 - RAG Chat Agent
```csharp
public class RagChatAgent(
    IChatClient chatClient,
    IRagService ragService)
{
    private const string SystemPrompt = """
        You are a helpful AI assistant. Answer questions based on the provided context 
        from ingested documents. If the context doesn't contain relevant information, 
        say so clearly. Always cite which document the information comes from.
        """;

    public async Task<string> ChatAsync(string userMessage)
    {
        // Step 1: Retrieve relevant context via vector search
        var context = await ragService.GetRelevantContextAsync(userMessage);

        // Step 2: Build augmented prompt
        var augmentedPrompt = $"""
            {SystemPrompt}

            {context}

            User Question: {userMessage}
            """;

        // Step 3: Use MAF ChatClientAgent for response
        ChatClientAgent agent = new(
            chatClient,
            name: "RAGAssistant",
            instructions: augmentedPrompt);

        var response = await agent.RunAsync(userMessage);
        return response.ToString();
    }
}
```

### Step 6.3 - DI Registration (Program.cs)
```csharp
var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Ollama Chat Client
builder.Services.AddSingleton<IChatClient>(sp =>
    new OllamaChatClient(
        new Uri(builder.Configuration["Ollama:BaseUrl"]!),
        modelId: builder.Configuration["Ollama:ChatModel"]!));

// Embedding Generator
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
    new OllamaEmbeddingGenerator(
        new Uri(builder.Configuration["Ollama:BaseUrl"]!),
        modelId: builder.Configuration["Ollama:EmbeddingModel"]!));

// Services
builder.Services.AddHttpClient<UrlExtractor>();
builder.Services.AddSingleton<PdfExtractor>();
builder.Services.AddSingleton<TextChunker>();
builder.Services.AddScoped<IEmbeddingService, OllamaEmbeddingService>();
builder.Services.AddScoped<IRagService, RagService>();
builder.Services.AddScoped<IDocumentIngestionService, DocumentIngestionService>();
builder.Services.AddScoped<RagChatAgent>();

builder.Services.Configure<RagOptions>(builder.Configuration.GetSection("Rag"));

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();
app.MapControllers();
app.MapOpenApi();
app.Run();
```

---

## Phase 7: API Endpoints

### Step 7.1 - Document Ingestion Controller
```csharp
[ApiController]
[Route("api/[controller]")]
public class DocumentController(IDocumentIngestionService ingestionService) : ControllerBase
{
    [HttpPost("ingest/url")]
    public async Task<IActionResult> IngestUrl([FromBody] IngestUrlRequest request)
    {
        var doc = await ingestionService.IngestUrlAsync(request.Url);
        return Ok(new { doc.Id, doc.Title, ChunkCount = doc.Chunks.Count });
    }

    [HttpPost("ingest/pdf")]
    public async Task<IActionResult> IngestPdf(IFormFile file)
    {
        using var stream = file.OpenReadStream();
        var doc = await ingestionService.IngestPdfAsync(stream, file.FileName);
        return Ok(new { doc.Id, doc.Title, ChunkCount = doc.Chunks.Count });
    }
}

public record IngestUrlRequest(string Url);
```

### Step 7.2 - Chat Controller
```csharp
[ApiController]
[Route("api/[controller]")]
public class ChatController(RagChatAgent agent) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        var response = await agent.ChatAsync(request.Message);
        return Ok(new { Response = response });
    }
}

public record ChatRequest(string Message);
```

---

## Phase 8: Database Migration

```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

This creates the `Documents` and `DocumentChunks` tables with the native SQL Server 2025 `vector(768)` column type.

---

## Phase 9: Prerequisites & Setup

### Local Ollama Setup
```bash
# Install Ollama, then pull required models:
ollama pull llama3.2          # Chat model
ollama pull nomic-embed-text  # Embedding model (768 dimensions)
```

### SQL Server 2025
- Install SQL Server 2025 (Developer edition is free)
- Ensure `vector` data type is available (SQL Server 2025+)

### Alternative: Google Gemini Setup
```bash
# Set environment variable
$env:GOOGLE_GENAI_API_KEY="your-api-key"
```

For Gemini, swap the `IChatClient` registration:
```csharp
builder.Services.AddSingleton<IChatClient>(sp =>
    new Google.GenAI.Client(vertexAI: false, apiKey: config["Gemini:ApiKey"])
        .AsIChatClient(config["Gemini:Model"]!));
```

---

## Implementation Order (Tasks)

| # | Task | Effort |
|---|------|--------|
| 1 | Add NuGet packages (EF Core 10, MAF, Ollama, PdfPig, HtmlAgilityPack) | Small |
| 2 | Create entity models (`Document`, `DocumentChunk` with `SqlVector<float>`) | Small |
| 3 | Create `AppDbContext` and configure vector column | Small |
| 4 | Run EF Core migration to create SQL Server tables | Small |
| 5 | Implement `PdfExtractor` and `UrlExtractor` | Medium |
| 6 | Implement `TextChunker` | Small |
| 7 | Implement `OllamaEmbeddingService` | Small |
| 8 | Implement `DocumentIngestionService` (orchestrator) | Medium |
| 9 | Implement `RagService` (vector search with `VectorDistance`) | Medium |
| 10 | Implement `RagChatAgent` using MAF `ChatClientAgent` | Medium |
| 11 | Create API controllers (`DocumentController`, `ChatController`) | Small |
| 12 | Configure DI in `Program.cs` | Small |
| 13 | Test end-to-end: ingest a URL → ask a question | Medium |

---

## Key EF Core 10 Features Used

1. **`SqlVector<float>`** - Native .NET type for SQL Server 2025 vector columns
2. **`EF.Functions.VectorDistance("cosine", ...)`** - LINQ-translated cosine similarity search
3. **`[Column(TypeName = "vector(768)")]`** - Maps to SQL Server's native `vector` data type
4. **JSON type support** - For storing chunk metadata if needed

## Key SQL Server 2025 Features Used

1. **`vector` data type** - Native storage for embedding vectors
2. **`VECTOR_DISTANCE()`** - Built-in function for similarity computation
3. **Vector index** (optional, for large-scale) - `CREATE VECTOR INDEX` for approximate nearest neighbor search

## Key MAF Features Used

1. **`ChatClientAgent`** - Wraps any `IChatClient` into an agent
2. **`IChatClient` abstraction** - Swap Ollama/Gemini without code changes
3. **`IEmbeddingGenerator<string, Embedding<float>>`** - Standard embedding interface

---

## Optional Enhancements (Future)

- [ ] Conversation history / memory (store chat turns in DB)
- [ ] Streaming responses via SSE
- [ ] Document management UI (Blazor/React)
- [ ] Hybrid search (full-text + vector via SQL Server 2025)
- [ ] Vector index creation for performance at scale
- [ ] Multi-model support (switch between Ollama and Gemini at runtime)
- [ ] Chunking strategies (semantic chunking with sentence boundaries)
- [ ] File upload support for DOCX, TXT, Markdown
