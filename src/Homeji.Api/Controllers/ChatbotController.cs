using Homeji.Api.Mappers;
using Homeji.Api.Views.Chatbot;
using Homeji.Application.DTOs.Chatbot;
using Homeji.Application.IServices.Chatbot;
using Microsoft.AspNetCore.Mvc;

namespace Homeji.Api.Controllers;

[ApiController]
[Route("api/chatbot")]
public sealed class ChatbotController : ControllerBase
{
    private readonly IChatbotService _chatbot;

    public ChatbotController(IChatbotService chatbot)
    {
        _chatbot = chatbot;
    }

    [HttpGet("popup-config")]
    [ProducesResponseType<ChatbotPopupConfigDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ChatbotPopupConfigDto>> GetPopupConfig(
        CancellationToken cancellationToken)
    {
        return Ok(await _chatbot.GetPopupConfigAsync(cancellationToken));
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
    [ProducesResponseType<ChatbotReplyDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChatbotReplyDto>> SendMessage(
        [FromBody] SendChatbotMessageViewModel request,
        CancellationToken cancellationToken)
    {
        return Ok(await _chatbot.SendMessageAsync(ChatbotViewMapper.ToDto(request), cancellationToken));
    }
}
