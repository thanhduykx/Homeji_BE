using Homeji.Application.DTOs.RoommateChats;
using Homeji.Application.IServices.RoommateChats;
using Microsoft.AspNetCore.Mvc;

namespace Homeji.Api.Controllers;

[ApiController]
[Route("api/roommate-chats")]
public sealed class RoommateChatsController : ControllerBase
{
    private readonly IRoommateChatService _chatService;

    public RoommateChatsController(IRoommateChatService chatService)
    {
        _chatService = chatService;
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RoommateConversationDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RoommateConversationDto>>> GetMine(
        CancellationToken cancellationToken)
    {
        return Ok(await _chatService.GetMineAsync(cancellationToken));
    }

    [HttpGet("{conversationId:guid}/messages")]
    [ProducesResponseType<IReadOnlyList<RoommateMessageDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RoommateMessageDto>>> GetMessages(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        return Ok(await _chatService.GetMessagesAsync(conversationId, cancellationToken));
    }

    [HttpPost("{conversationId:guid}/messages")]
    [ProducesResponseType<RoommateMessageDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<RoommateMessageDto>> SendMessage(
        Guid conversationId,
        [FromBody] SendRoommateMessageDto request,
        CancellationToken cancellationToken)
    {
        var message = await _chatService.SendMessageAsync(conversationId, request, cancellationToken);
        return Created($"/api/roommate-chats/{conversationId:D}/messages/{message.Id:D}", message);
    }
}
