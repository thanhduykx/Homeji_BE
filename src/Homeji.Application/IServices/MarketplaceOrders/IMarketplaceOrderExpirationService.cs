namespace Homeji.Application.IServices.MarketplaceOrders;

public interface IMarketplaceOrderExpirationService
{
    Task<int> ExpireOverdueAsync(CancellationToken cancellationToken = default);
    Task<int> ReleaseOverdueFundsAsync(CancellationToken cancellationToken = default);
}
