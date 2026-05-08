using Microsoft.Data.SqlTypes;
using PersonalAssistant.Data;
using PersonalAssistant.Models;

namespace PersonalAssistant.Services;

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
        return await IngestTextAsync(text, url, "url", title: new Uri(url).Host);
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
