using Homeji.Application.DTOs.Conversations;
using Homeji.Application.IServices.Conversations;
using Microsoft.AspNetCore.Mvc;

namespace Homeji.Api.Controllers;

[ApiController]
[Route("api/conversations")]
public sealed class ConversationsController : ControllerBase
{
    private readonly IPostConversationService _conversations;

    public ConversationsController(IPostConversationService conversations)
    {
        _conversations = conversations;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PostConversationDto>>> GetMine(CancellationToken cancellationToken)
    {
        return Ok(await _conversations.GetMineAsync(cancellationToken));
    }

    [HttpPost("rental-posts/{postId:guid}")]
    public async Task<ActionResult<PostConversationDto>> StartRental(Guid postId, CancellationToken cancellationToken)
    {
        return Ok(await _conversations.StartRentalConversationAsync(postId, cancellationToken));
    }

    [HttpPost("marketplace-posts/{postId:guid}")]
    public async Task<ActionResult<PostConversationDto>> StartMarketplace(Guid postId, CancellationToken cancellationToken)
    {
        return Ok(await _conversations.StartMarketplaceConversationAsync(postId, cancellationToken));
    }

    [HttpPost("rental-wanted-posts/{postId:guid}")]
    public async Task<ActionResult<PostConversationDto>> StartWantedPost(Guid postId, CancellationToken cancellationToken)
    {
        return Ok(await _conversations.StartWantedPostConversationAsync(postId, cancellationToken));
    }

    [HttpGet("{conversationId:guid}/messages")]
    public async Task<ActionResult<IReadOnlyList<PostMessageDto>>> GetMessages(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        return Ok(await _conversations.GetMessagesAsync(conversationId, cancellationToken));
    }

    [HttpPost("{conversationId:guid}/messages")]
    public async Task<ActionResult<PostMessageDto>> SendMessage(
        Guid conversationId,
        [FromBody] SendPostMessageDto request,
        CancellationToken cancellationToken)
    {
        var result = await _conversations.SendMessageAsync(conversationId, request, cancellationToken);
        return Created($"/api/conversations/{conversationId}/messages/{result.Id}", result);
    }
}
