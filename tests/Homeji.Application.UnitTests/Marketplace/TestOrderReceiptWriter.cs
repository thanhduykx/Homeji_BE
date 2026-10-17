using Homeji.Application.IServices.Emails;
using Homeji.Domain.Entities;

namespace Homeji.Application.UnitTests.Marketplace;

internal sealed class TestOrderReceiptWriter : IOrderReceiptWriter
{
    public Task RecordOrderAsync(MarketplaceOrder order, string itemTitle, CancellationToken cancellationToken) => Task.CompletedTask;
}
