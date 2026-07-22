using Homeji.Application.DTOs.Conversations;
using Homeji.Application.IServices.Conversations;
using Homeji.Domain.Enums;
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

    [HttpPost("{conversationId:guid}/messages/images")]
    [RequestSizeLimit(42 * 1024 * 1024)]
    [ProducesResponseType<PostMessageDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PostMessageDto>> SendImages(
        Guid conversationId,
        [FromForm] IReadOnlyList<IFormFile> files,
        [FromForm] string? body,
        [FromForm] MessageAttachmentContext context,
        CancellationToken cancellationToken)
    {
        if (files is null || files.Count is < 1 or > 5)
        {
            ModelState.AddModelError("files", "Select between 1 and 5 images.");
            return ValidationProblem(ModelState);
        }

        var uploads = new List<ConversationImageUpload>(files.Count);
        foreach (var file in files)
        {
            if (file.Length is <= 0 or > 8 * 1024 * 1024)
            {
                ModelState.AddModelError("files", "Each image must be between 1 byte and 8 MB.");
                return ValidationProblem(ModelState);
            }

            await using var stream = new MemoryStream((int)file.Length);
            await file.CopyToAsync(stream, cancellationToken);
            uploads.Add(new ConversationImageUpload(
                file.FileName,
                file.ContentType,
                stream.ToArray(),
                context));
        }

        var result = await _conversations.SendImagesAsync(
            conversationId,
            body,
            uploads,
            cancellationToken);
        return Created($"/api/conversations/{conversationId}/messages/{result.Id}", result);
    }

    [HttpGet("{conversationId:guid}/messages/{messageId:guid}/attachments/{attachmentId:guid}/content")]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAttachmentContent(
        Guid conversationId,
        Guid messageId,
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        var result = await _conversations.GetAttachmentContentAsync(
            conversationId,
            messageId,
            attachmentId,
            cancellationToken);
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.ETag = $"\"{result.Sha256}\"";
        return File(result.Content, result.MimeType);
    }

    [HttpDelete("{conversationId:guid}/messages/{messageId:guid}/attachments/{attachmentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAttachment(
        Guid conversationId,
        Guid messageId,
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        await _conversations.DeleteAttachmentAsync(
            conversationId,
            messageId,
            attachmentId,
            cancellationToken);
        return NoContent();
    }
}
