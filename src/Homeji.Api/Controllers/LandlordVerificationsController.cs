using Homeji.Application.DTOs.Verifications;
using Homeji.Application.IServices.Verifications;
using Homeji.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Homeji.Api.Controllers;

[ApiController]
[Route("api/landlord-verifications")]
public sealed class LandlordVerificationsController : ControllerBase
{
    private readonly ILandlordVerificationService _service;

    public LandlordVerificationsController(ILandlordVerificationService service)
    {
        _service = service;
    }

    [HttpGet("mine")]
    public async Task<ActionResult<LandlordVerificationDto>> GetMine(CancellationToken cancellationToken)
    {
        var result = await _service.GetMineAsync(cancellationToken);
        return result is null ? NoContent() : Ok(result);
    }

    [HttpPost]
    [ProducesResponseType<LandlordVerificationDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<LandlordVerificationDto>> Submit(
        [FromBody] SubmitLandlordVerificationDto request,
        CancellationToken cancellationToken)
    {
        var result = await _service.SubmitAsync(request, cancellationToken);
        return Created($"/api/landlord-verifications/{result.Id}", result);
    }

    [HttpGet("/api/admin/landlord-verifications")]
    public async Task<ActionResult<IReadOnlyList<LandlordVerificationDto>>> GetForAdmin(
        [FromQuery] LandlordVerificationStatus status = LandlordVerificationStatus.Pending,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _service.GetForAdminAsync(status, cancellationToken));
    }

    [HttpPost("/api/admin/landlord-verifications/{id:guid}/review")]
    public async Task<ActionResult<LandlordVerificationDto>> Review(
        Guid id,
        [FromBody] ReviewLandlordVerificationDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.ReviewAsync(id, request, cancellationToken));
    }
}
