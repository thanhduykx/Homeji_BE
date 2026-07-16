using Homeji.Application.DTOs.Wallets;
using Homeji.Application.IServices.Wallets;
using Microsoft.AspNetCore.Mvc;

namespace Homeji.Api.Controllers;

[ApiController]
[Route("api/wallet")]
public sealed class WalletController : ControllerBase
{
    private readonly IWalletService _wallets;
    private readonly IWalletWithdrawalService _withdrawals;

    public WalletController(IWalletService wallets, IWalletWithdrawalService withdrawals)
    {
        _wallets = wallets;
        _withdrawals = withdrawals;
    }

    [HttpGet]
    [ProducesResponseType<WalletDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<WalletDto>> GetMine(CancellationToken cancellationToken) =>
        Ok(await _wallets.GetMineAsync(cancellationToken));

    [HttpGet("transactions")]
    [ProducesResponseType<IReadOnlyList<WalletTransactionDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<WalletTransactionDto>>> GetTransactions(
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await _wallets.GetMyTransactionsAsync(take, cancellationToken));

    [HttpGet("withdrawals")]
    [ProducesResponseType<IReadOnlyList<WalletWithdrawalDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<WalletWithdrawalDto>>> GetWithdrawals(CancellationToken cancellationToken) =>
        Ok(await _withdrawals.GetMineAsync(cancellationToken));

    [HttpPost("withdrawals")]
    [ProducesResponseType<WalletWithdrawalDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<WalletWithdrawalDto>> CreateWithdrawal(
        [FromBody] CreateWalletWithdrawalDto request,
        CancellationToken cancellationToken)
    {
        var created = await _withdrawals.CreateAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }
}
