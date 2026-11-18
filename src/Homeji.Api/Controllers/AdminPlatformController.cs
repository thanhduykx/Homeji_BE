using Homeji.Application.DTOs.Admin;
using Homeji.Application.IServices.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Homeji.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/admin/platform")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminPlatformController(IAdminPlatformService platform) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AdminPlatformSnapshot>> Get(CancellationToken cancellationToken) =>
        Ok(await platform.GetAsync(cancellationToken));
}
