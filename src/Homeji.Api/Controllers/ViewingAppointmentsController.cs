using Homeji.Application.DTOs.Appointments;
using Homeji.Application.IServices.Appointments;
using Microsoft.AspNetCore.Mvc;

namespace Homeji.Api.Controllers;

[ApiController]
[Route("api/viewing-appointments")]
public sealed class ViewingAppointmentsController : ControllerBase
{
    private readonly IViewingAppointmentService _appointments;

    public ViewingAppointmentsController(IViewingAppointmentService appointments)
    {
        _appointments = appointments;
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ViewingAppointmentDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ViewingAppointmentDto>>> GetMine(CancellationToken cancellationToken)
    {
        return Ok(await _appointments.GetMineAsync(cancellationToken));
    }

    [HttpPost("/api/rental-posts/{rentalPostId:guid}/viewing-appointments")]
    [ProducesResponseType<ViewingAppointmentDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ViewingAppointmentDto>> Create(
        Guid rentalPostId,
        [FromBody] CreateViewingAppointmentDto request,
        CancellationToken cancellationToken)
    {
        var result = await _appointments.CreateAsync(rentalPostId, request, cancellationToken);
        return Created($"/api/viewing-appointments/{result.Id}", result);
    }

    [HttpPost("{id:guid}/confirm")]
    public async Task<ActionResult<ViewingAppointmentDto>> Confirm(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _appointments.ConfirmAsync(id, cancellationToken));
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<ViewingAppointmentDto>> Reject(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _appointments.RejectAsync(id, cancellationToken));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<ViewingAppointmentDto>> Cancel(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _appointments.CancelAsync(id, cancellationToken));
    }
}
