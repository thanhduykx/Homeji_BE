using Homeji.Application.DTOs.Wallets;
using Homeji.Application.IServices.Wallets;
using Homeji.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Homeji.Api.Controllers;

[ApiController]
[Route("api/admin/wallet-withdrawals")]
public sealed class AdminWalletWithdrawalsController : ControllerBase
{
    private readonly IWalletWithdrawalService _withdrawals;

    public AdminWalletWithdrawalsController(IWalletWithdrawalService withdrawals)
    {
        _withdrawals = withdrawals;
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<WalletWithdrawalDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<WalletWithdrawalDto>>> GetAll(
        [FromQuery] WalletWithdrawalStatus? status,
        CancellationToken cancellationToken) =>
        Ok(await _withdrawals.GetForAdminAsync(status, cancellationToken));

    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType<WalletWithdrawalDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<WalletWithdrawalDto>> Complete(
        Guid id,
        [FromBody] ReviewWalletWithdrawalDto request,
        CancellationToken cancellationToken) =>
        Ok(await _withdrawals.CompleteAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType<WalletWithdrawalDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<WalletWithdrawalDto>> Reject(
        Guid id,
        [FromBody] ReviewWalletWithdrawalDto request,
        CancellationToken cancellationToken) =>
        Ok(await _withdrawals.RejectAsync(id, request, cancellationToken));
}
