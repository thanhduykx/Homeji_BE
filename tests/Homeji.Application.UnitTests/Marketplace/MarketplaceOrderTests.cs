using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Domain.Exceptions;

namespace Homeji.Application.UnitTests.Marketplace;

public sealed class MarketplaceOrderTests
{
    [Fact]
    public void Complete_WhenAccepted_ChangesStatus()
    {
        var now = DateTimeOffset.UtcNow;
        var order = CreateOrder(now);
        order.Accept(now.AddMinutes(1));

        order.Complete(now.AddMinutes(2));

        Assert.Equal(MarketplaceOrderStatus.Completed, order.Status);
    }

    [Fact]
    public void Complete_WhenRequested_ThrowsDomainException()
    {
        var now = DateTimeOffset.UtcNow;
        var order = CreateOrder(now);

        Assert.Throws<DomainException>(() => order.Complete(now.AddMinutes(1)));
    }

    private static MarketplaceOrder CreateOrder(DateTimeOffset now) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            250_000,
            now.AddDays(1),
            "FPT University",
            null,
            now);
}
