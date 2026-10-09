using Homeji.Application.DTOs.Roommates;
using Homeji.Application.IServices.Roommates;
using Homeji.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
namespace Homeji.Api.Controllers;
[ApiController]
[Route("api/roommates")]
public sealed class RoommatesController(IRoommateDirectoryService service) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<RoommateProfileDto>> GetMine(CancellationToken ct) => Ok(await service.GetMineAsync(ct));
    [HttpPut("me")]
    public async Task<ActionResult<RoommateProfileDto>> UpdateMine(RoommateProfileDto request, CancellationToken ct) => Ok(await service.UpdateMineAsync(request, ct));
    [HttpGet]
    public async Task<ActionResult<RoommateDirectoryPageDto>> Search([FromQuery] RoommateIntent? intent, [FromQuery] string? keyword,
        CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) => Ok(await service.SearchAsync(intent, keyword, page, pageSize, ct));
}
