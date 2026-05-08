namespace PersonalAssistant.Models;

public class Document
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string Source { get; set; }
    public required string SourceType { get; set; }
    public DateTime IngestedAt { get; set; } = DateTime.UtcNow;
    public ICollection<DocumentChunk> Chunks { get; set; } = [];
}
