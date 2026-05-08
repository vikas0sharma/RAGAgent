using Microsoft.AspNetCore.Mvc;
using PersonalAssistant.Services;

namespace PersonalAssistant.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentController(IDocumentIngestionService ingestionService) : ControllerBase
{
    [HttpPost("ingest/url")]
    public async Task<IActionResult> IngestUrl([FromBody] IngestUrlRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Url) || !Uri.TryCreate(request.Url, UriKind.Absolute, out _))
            return BadRequest("A valid URL is required.");

        var doc = await ingestionService.IngestUrlAsync(request.Url);
        return Ok(new { doc.Id, doc.Title, ChunkCount = doc.Chunks.Count });
    }

    [HttpPost("ingest/pdf")]
    public async Task<IActionResult> IngestPdf(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest("A PDF file is required.");

        if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Only PDF files are supported.");

        using var stream = file.OpenReadStream();
        var doc = await ingestionService.IngestPdfAsync(stream, file.FileName);
        return Ok(new { doc.Id, doc.Title, ChunkCount = doc.Chunks.Count });
    }
}

public record IngestUrlRequest(string Url);
