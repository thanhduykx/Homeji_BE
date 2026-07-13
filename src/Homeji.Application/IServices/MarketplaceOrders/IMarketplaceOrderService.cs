using Homeji.Application.DTOs.MarketplaceOrders;

namespace Homeji.Application.IServices.MarketplaceOrders;

public interface IMarketplaceOrderService
{
    Task<MarketplaceOrderDto> CreateAsync(Guid postId, CreateMarketplaceOrderDto request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MarketplaceOrderDto>> GetMineAsync(CancellationToken cancellationToken = default);
    Task<MarketplaceOrderDto> AcceptAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MarketplaceOrderDto> RejectAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MarketplaceOrderDto> CancelAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MarketplaceOrderDto> CompleteAsync(Guid id, CancellationToken cancellationToken = default);
}
