using Homeji.Application.DTOs.Roommates;
using Homeji.Application.IServices.Roommates;
using Microsoft.AspNetCore.Mvc;

namespace Homeji.Api.Controllers;

[ApiController]
[Route("api/roommate-invitations")]
public sealed class RoommateInvitationsController : ControllerBase
{
    private readonly IRoommateInvitationService _invitationService;

    public RoommateInvitationsController(IRoommateInvitationService invitationService)
    {
        _invitationService = invitationService;
    }

    [HttpGet("mine")]
    [ProducesResponseType<IReadOnlyList<RoommateInvitationDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RoommateInvitationDto>>> GetMine(CancellationToken cancellationToken)
    {
        return Ok(await _invitationService.GetMineAsync(cancellationToken));
    }

    [HttpPost("rental-posts/{postId:guid}")]
    [ProducesResponseType<RoommateInvitationDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<RoommateInvitationDto>> Create(
        Guid postId,
        [FromBody] CreateRoommateInvitationDto request,
        CancellationToken cancellationToken)
    {
        var invitation = await _invitationService.CreateAsync(postId, request, cancellationToken);
        return CreatedAtAction(nameof(GetMine), invitation);
    }

    [HttpPost("{invitationId:guid}/accept")]
    [ProducesResponseType<RoommateInvitationDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RoommateInvitationDto>> Accept(Guid invitationId, CancellationToken cancellationToken)
    {
        return Ok(await _invitationService.AcceptAsync(invitationId, cancellationToken));
    }

    [HttpPost("{invitationId:guid}/reject")]
    [ProducesResponseType<RoommateInvitationDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RoommateInvitationDto>> Reject(Guid invitationId, CancellationToken cancellationToken)
    {
        return Ok(await _invitationService.RejectAsync(invitationId, cancellationToken));
    }

    [HttpPost("{invitationId:guid}/cancel")]
    [ProducesResponseType<RoommateInvitationDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RoommateInvitationDto>> Cancel(Guid invitationId, CancellationToken cancellationToken)
    {
        return Ok(await _invitationService.CancelAsync(invitationId, cancellationToken));
    }
}
