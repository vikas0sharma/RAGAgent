namespace PersonalAssistant.Services;

public interface IEmbeddingService
{
    Task<float[]> GenerateEmbeddingAsync(string text);
    Task<IList<float[]>> GenerateEmbeddingsAsync(IList<string> texts);
}
