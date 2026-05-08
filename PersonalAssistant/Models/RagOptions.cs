namespace PersonalAssistant.Models;

public class RagOptions
{
    public int ChunkSize { get; set; } = 500;
    public int ChunkOverlap { get; set; } = 50;
    public int TopK { get; set; } = 5;
    public int EmbeddingDimension { get; set; } = 768;
}
