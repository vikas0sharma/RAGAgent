using PersonalAssistant.Models;

namespace PersonalAssistant.Services;

public interface IDocumentIngestionService
{
    Task<Document> IngestUrlAsync(string url);
    Task<Document> IngestPdfAsync(Stream pdfStream, string fileName);
}
