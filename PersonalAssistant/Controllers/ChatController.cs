using Microsoft.AspNetCore.Mvc;
using PersonalAssistant.Agents;

namespace PersonalAssistant.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController(RagChatAgent agent) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest("Message is required.");

        var response = await agent.ChatAsync(request.Message);
        return Ok(new { Response = response });
    }
}

public record ChatRequest(string Message);
