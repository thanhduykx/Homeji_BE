using Homeji.Api.Mappers;
using Homeji.Api.Security;
using Homeji.Api.Views.Chatbot;
using Homeji.Application.DTOs.Chatbot;
using Homeji.Application.IServices.Chatbot;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Homeji.Api.RateLimiting;

namespace Homeji.Api.Controllers;

[ApiController]
[Route("api/chatbot")]
public sealed class ChatbotController : ControllerBase
{
    private readonly IChatbotService _chatbot;
    private readonly IChatbotPostingAgent _posting;

    private readonly ChatbotRecaptcha _recaptcha;

    public ChatbotController(IChatbotService chatbot, IChatbotPostingAgent posting, ChatbotRecaptcha recaptcha)
    {
        _chatbot = chatbot;
        _posting = posting;
        _recaptcha = recaptcha;
    }

    [HttpGet("popup-config")]
    [ProducesResponseType<ChatbotPopupConfigDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ChatbotPopupConfigDto>> GetPopupConfig(
        CancellationToken cancellationToken)
    {
        var config = await _chatbot.GetPopupConfigAsync(cancellationToken);
        return Ok(config with { RecaptchaSiteKey = _recaptcha.SiteKey });
    }

    [HttpGet("conversations")]
    [ProducesResponseType<IReadOnlyList<ChatbotConversationDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<ChatbotConversationDto>>> GetMyConversations(
        CancellationToken cancellationToken)
    {
        return Ok(await _chatbot.GetMyConversationsAsync(cancellationToken));
    }

    [HttpGet("conversations/{conversationId:guid}/messages")]
    [ProducesResponseType<IReadOnlyList<ChatbotMessageDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ChatbotMessageDto>>> GetMessages(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        return Ok(await _chatbot.GetMessagesAsync(conversationId, cancellationToken));
    }

    [HttpPost("messages")]
    [RequestSizeLimit(12 * 1024 * 1024)]
    [EnableRateLimiting(RateLimitingPolicyNames.CostlyOperations)]
    [ProducesResponseType<ChatbotReplyDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChatbotReplyDto>> SendMessage(
        [FromBody] SendChatbotMessageViewModel request,
        CancellationToken cancellationToken)
    {
        var problem = await CheckRecaptchaAsync(ChatbotRecaptcha.MessageAction, cancellationToken);
        if (problem is not null) return problem;

        return Ok(await _chatbot.SendMessageAsync(ChatbotViewMapper.ToDto(request), cancellationToken));
    }

    [HttpPost("transcriptions")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [EnableRateLimiting(RateLimitingPolicyNames.CostlyOperations)]
    public async Task<IActionResult> Transcribe([FromBody] ChatbotTranscriptionDto request, CancellationToken cancellationToken)
    {
        var problem = await CheckRecaptchaAsync(ChatbotRecaptcha.TranscriptionAction, cancellationToken);
        if (problem is not null) return problem;

        return Ok(new { text = await _chatbot.TranscribeAsync(request, cancellationToken) });
    }

    private async Task<ObjectResult?> CheckRecaptchaAsync(string action, CancellationToken cancellationToken)
    {
        var result = await _recaptcha.VerifyAsync(Request.Headers[ChatbotRecaptcha.TokenHeader].ToString(), action, cancellationToken);
        return result switch
        {
            ChatbotRecaptchaResult.Accepted => null,
            ChatbotRecaptchaResult.Rejected => Problem(
                statusCode: StatusCodes.Status403Forbidden,
                detail: "Không thể xác minh reCAPTCHA. Vui lòng thử lại."),
            _ => Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                detail: "Dịch vụ xác minh reCAPTCHA tạm thời không khả dụng. Vui lòng thử lại."),
        };
    }

    [HttpPost("agent-drafts/{draftId:guid}/confirm")]
    [EnableRateLimiting(RateLimitingPolicyNames.CostlyOperations)]
    public async Task<IActionResult> ConfirmDraft(Guid draftId, [FromBody] AgentPostDetails details, CancellationToken cancellationToken)
        => Ok(new { postId = await _posting.ConfirmAsync(draftId, details, cancellationToken) });

    [HttpDelete("agent-drafts/{draftId:guid}")]
    [EnableRateLimiting(RateLimitingPolicyNames.CostlyOperations)]
    public async Task<IActionResult> CancelDraft(Guid draftId, CancellationToken cancellationToken)
    {
        await _posting.CancelAsync(draftId, cancellationToken);
        return NoContent();
    }

    [HttpDelete("conversations/{conversationId:guid}")]
    [EnableRateLimiting(RateLimitingPolicyNames.CostlyOperations)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteConversation(Guid conversationId, CancellationToken cancellationToken)
    {
        await _chatbot.DeleteConversationAsync(conversationId, cancellationToken);
        return NoContent();
    }
}
