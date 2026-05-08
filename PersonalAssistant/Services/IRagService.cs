namespace PersonalAssistant.Services;

public interface IRagService
{
    Task<string> GetFromRAG(string query);
}
