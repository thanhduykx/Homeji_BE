using Homeji.Application.DTOs.Wallets;
using Homeji.Application.IServices.Wallets;
using Microsoft.AspNetCore.Mvc;

namespace Homeji.Api.Controllers;

[ApiController]
[Route("api/wallet")]
public sealed class WalletController : ControllerBase
{
    private readonly IWalletService _wallets;

    public WalletController(IWalletService wallets)
    {
        _wallets = wallets;
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
}
