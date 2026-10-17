using Homeji.Domain.Entities;

namespace Homeji.Application.IServices.Emails;

public interface IOrderReceiptWriter
{
    Task RecordOrderAsync(MarketplaceOrder order, string itemTitle, CancellationToken cancellationToken);
}
