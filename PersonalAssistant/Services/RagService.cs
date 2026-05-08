using System.Text;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PersonalAssistant.Data;
using PersonalAssistant.Models;

namespace PersonalAssistant.Services;

public class RagService(
    AppDbContext db,
    IEmbeddingService embeddingService,
    IOptions<RagOptions> options) : IRagService
{
    public async Task<string> GetFromRAG(string query)
    {
        var queryEmbedding = await embeddingService.GenerateEmbeddingAsync(query);
        var sqlVector = new SqlVector<float>(queryEmbedding);
        var topK = options.Value.TopK;

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
