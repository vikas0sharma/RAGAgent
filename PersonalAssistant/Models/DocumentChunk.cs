using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.Data.SqlTypes;

namespace PersonalAssistant.Models;

public class DocumentChunk
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public required string Content { get; set; }
    public int ChunkIndex { get; set; }

    [Column(TypeName = "vector(768)")]
    public SqlVector<float> Embedding { get; set; }

    public Document Document { get; set; } = null!;
}
