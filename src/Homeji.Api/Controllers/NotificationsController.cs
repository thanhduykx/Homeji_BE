using Homeji.Application.DTOs.Notifications;
using Homeji.Application.IServices.Notifications;
using Microsoft.AspNetCore.Mvc;

namespace Homeji.Api.Controllers;

[ApiController]
[Route("api/notifications")]
public sealed class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<NotificationDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<NotificationDto>>> GetMine(
        [FromQuery] bool unreadOnly = false,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _notificationService.GetMineAsync(unreadOnly, cancellationToken));
    }

    [HttpPost("{notificationId:guid}/read")]
    [ProducesResponseType<NotificationDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationDto>> MarkRead(Guid notificationId, CancellationToken cancellationToken)
    {
        return Ok(await _notificationService.MarkReadAsync(notificationId, cancellationToken));
    }

    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        await _notificationService.MarkAllReadAsync(cancellationToken);
        return NoContent();
    }
}
