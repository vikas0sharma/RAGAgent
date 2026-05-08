using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using PersonalAssistant.Services;

namespace PersonalAssistant.Agents;

public class RagChatAgent(IChatClient chatClient, IRagService ragService)
{
    private const string SystemPrompt = """
        You are a helpful AI assistant. Answer questions based on the provided context
        from ingested documents. If the context doesn't contain relevant information,
        say so clearly. Always cite which document the information comes from when possible.
        """;

    public async Task<string> ChatAsync(string userMessage)
    {

        ChatClientAgent agent = new(
            chatClient,
            name: "RAGAssistant",
            instructions: SystemPrompt, tools: [AIFunctionFactory.Create(ragService.GetFromRAG)]);

        var response = await agent.RunAsync(userMessage);
        return response.ToString();
    }
}
