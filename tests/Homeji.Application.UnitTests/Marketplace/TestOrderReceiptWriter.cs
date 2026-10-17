using Homeji.Application.IServices.Emails;
using Homeji.Domain.Entities;

namespace Homeji.Application.UnitTests.Marketplace;

internal sealed class TestOrderReceiptWriter : IOrderReceiptWriter
{
    public List<(Guid OrderId, decimal Amount, string Title)> Recorded { get; } = [];
    public Task RecordOrderAsync(MarketplaceOrder order, string itemTitle, CancellationToken cancellationToken)
    {
        Recorded.Add((order.Id, order.AgreedPrice, itemTitle));
        return Task.CompletedTask;
    }
}
