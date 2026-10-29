using System.Text.Json;
using Homeji.Api.Realtime;
using Homeji.Api.RateLimiting;
using Homeji.Application.DTOs.Conversations;
using Homeji.Application.IServices.Conversations;
using Homeji.Application.Abstractions.Authentication;
using Homeji.Application.IRepositories.Conversations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Homeji.Api.Controllers;

[ApiController]
[Route("api/chat")]
[EnableRateLimiting(RateLimitingPolicyNames.CostlyOperations)]
public sealed class ChatMediaController(IConfiguration configuration) : ControllerBase
{
    [HttpGet("voice-config")]
    public async Task<IActionResult> VoiceConfig([FromServices] VoiceIceService iceService, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        _ = Guid.TryParse(User.FindFirst("sub")?.Value, out var userId);
        return Ok(await iceService.CreateAsync(userId, cancellationToken));
    }

    [HttpGet("gif-config")]
    public IActionResult GifConfig()
    {
        Response.Headers.CacheControl = "private, no-store";
        var key = configuration["Giphy:ApiKey"];
        if (string.IsNullOrWhiteSpace(key)) return StatusCode(503, new { detail = "Tìm GIF đang chờ kích hoạt dịch vụ." });
        // GIPHY requires browser-side requests. This is a browser API key,
        // deliberately public to authenticated clients, never the TURN shared secret.
        return Ok(new { apiKey = key });
    }

    [HttpPost("conversations/{conversationId:guid}/gifs/{gifId}")]
    public async Task<IActionResult> SendGif(Guid conversationId, string gifId,
        [FromServices] IPostConversationService conversations,
        [FromServices] IPostConversationRepository repository,
        [FromServices] ICurrentUser currentUser, CancellationToken cancellationToken)
    {
        if (gifId.Length is < 1 or > 80 || !gifId.All(char.IsAsciiLetterOrDigit)) return BadRequest();
        var conversation = await repository.GetByIdAsync(conversationId, cancellationToken);
        if (currentUser.UserId is not { } userId || conversation is null || !conversation.Includes(userId)) return NotFound();
        var key = configuration["Giphy:ApiKey"];
        if (string.IsNullOrWhiteSpace(key)) return StatusCode(503, new { detail = "Tìm GIF đang chờ kích hoạt dịch vụ." });
        var message = await conversations.SendMessageAsync(conversationId,
            new SendPostMessageDto("homeji:gif:v1:" + JsonSerializer.Serialize(new { id = gifId })), cancellationToken);
        return Ok(message);
    }
}
