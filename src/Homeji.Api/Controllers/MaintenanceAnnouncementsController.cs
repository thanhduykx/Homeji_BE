using Homeji.Application.Abstractions.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Homeji.Api.Controllers;

[ApiController]
[Route("api/maintenance-announcements")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class MaintenanceAnnouncementsController(IMaintenanceAnnouncements announcements) : ControllerBase
{
    [HttpGet, AllowAnonymous]
    public async Task<IActionResult> Current(CancellationToken cancellationToken)
    {
        var notice = await announcements.GetCurrentAsync(cancellationToken);
        return notice is null ? Ok(Array.Empty<object>()) : Ok(new[] { notice });
    }
}
