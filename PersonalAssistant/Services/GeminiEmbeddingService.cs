using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.Options;
using PersonalAssistant.Models;

namespace PersonalAssistant.Services;

public class GeminiEmbeddingOptions
{
    public string Model { get; set; } = "gemini-embedding-2";
}

public class GeminiEmbeddingService(Client genAiClient, IOptions<GeminiEmbeddingOptions> options, IOptions<RagOptions> ragOptions) : IEmbeddingService
{
    private readonly string _model = options.Value.Model;
    private readonly int _outputDimensionality = ragOptions.Value.EmbeddingDimension;

    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        var response = await genAiClient.Models.EmbedContentAsync(
            model: _model,
            contents: text,
            config: new EmbedContentConfig { OutputDimensionality = _outputDimensionality });
        return response.Embeddings![0].Values!.Select(v => (float)v).ToArray();
    }

    public async Task<IList<float[]>> GenerateEmbeddingsAsync(IList<string> texts)
    {
        var results = new List<float[]>();
        foreach (var text in texts)
        {
            results.Add(await GenerateEmbeddingAsync(text));
        }
        return results;
    }
}
