using Homeji.Application.IServices.AI;
using Homeji.Application.Services.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Homeji.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/chatbot/conversations")]
public sealed class ChatbotHistoryController(IChatHistoryStore history, UserContext userContext) : ControllerBase
{
    [HttpDelete("{conversationId:guid}")]
    public async Task<IActionResult> Delete(Guid conversationId, CancellationToken cancellationToken)
    {
        await history.DeleteOwnedAsync(conversationId, userContext.GetRequiredUserId(), cancellationToken);
        return NoContent();
    }
}
