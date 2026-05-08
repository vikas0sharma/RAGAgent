using System.Text;
using Microsoft.Extensions.Options;
using PersonalAssistant.Models;

namespace PersonalAssistant.Services;

public class TextChunker(IOptions<RagOptions> options)
{
    public List<string> ChunkText(string text)
    {
        var chunkSize = options.Value.ChunkSize;
        var overlap = options.Value.ChunkOverlap;
        var chunks = new List<string>();

        var sentences = text.Split(['.', '!', '?', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var current = new StringBuilder();

        foreach (var sentence in sentences)
        {
            if (current.Length + sentence.Length > chunkSize && current.Length > 0)
            {
                chunks.Add(current.ToString().Trim());
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
