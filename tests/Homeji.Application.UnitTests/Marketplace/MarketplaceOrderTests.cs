using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Domain.Exceptions;

namespace Homeji.Application.UnitTests.Marketplace;

public sealed class MarketplaceOrderTests
{
    [Fact]
    public void Complete_WhenDelivered_ChangesStatus()
    {
        var now = DateTimeOffset.UtcNow;
        var order = CreateOrder(now);
        order.Accept(now.AddMinutes(1));
        order.MarkDelivered(now.AddMinutes(2));

        order.Complete(now.AddMinutes(3));

        Assert.Equal(MarketplaceOrderStatus.Completed, order.Status);
        Assert.Equal(now.AddMinutes(2), order.DeliveredAt);
    }

    [Fact]
    public void Complete_WhenRequested_ThrowsDomainException()
    {
        var now = DateTimeOffset.UtcNow;
        var order = CreateOrder(now);

        Assert.Throws<DomainException>(() => order.Complete(now.AddMinutes(1)));
    }

    [Fact]
    public void Cancel_WhenRequested_ChangesStatus()
    {
        var now = DateTimeOffset.UtcNow;
        var order = CreateOrder(now);

        order.Cancel(now.AddMinutes(1));

        Assert.Equal(MarketplaceOrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void Cancel_WhenSellerAlreadyAccepted_ThrowsDomainException()
    {
        var now = DateTimeOffset.UtcNow;
        var order = CreateOrder(now);
        order.Accept(now.AddMinutes(1));

        Assert.Throws<DomainException>(() => order.Cancel(now.AddMinutes(2)));
        Assert.Equal(MarketplaceOrderStatus.Accepted, order.Status);
    }

    [Fact]
    public void Constructor_WithQuantityAndCommission_CalculatesEscrowAmounts()
    {
        var now = DateTimeOffset.UtcNow;
        var order = new MarketplaceOrder(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            35_000,
            now.AddHours(1),
            "Ký túc xá",
            null,
            now,
            2,
            0.10m);

        Assert.Equal(35_000, order.UnitPrice);
        Assert.Equal(2, order.Quantity);
        Assert.Equal(70_000, order.AgreedPrice);
        Assert.Equal(7_000, order.PlatformFeeAmount);
        Assert.Equal(63_000, order.SellerNetAmount);
    }

    [Fact]
    public void MarkFundsReleased_RequiresCompletedOrder_AndIsIdempotencyProtected()
    {
        var now = DateTimeOffset.UtcNow;
        var order = CreateOrder(now);

        Assert.Throws<DomainException>(() => order.MarkFundsReleased(now.AddMinutes(1)));
        order.Accept(now.AddMinutes(1));
        order.MarkDelivered(now.AddMinutes(2));
        order.Complete(now.AddMinutes(3));
        order.MarkFundsReleased(now.AddMinutes(4));

        Assert.Equal(now.AddMinutes(4), order.FundsReleasedAt);
        Assert.Throws<DomainException>(() => order.MarkFundsReleased(now.AddMinutes(5)));
    }

    [Fact]
    public void Expire_WhenRequested_AllowsRefundAndPreventsAcceptance()
    {
        var now = DateTimeOffset.UtcNow;
        var order = CreateOrder(now);

        order.Expire(now.AddMinutes(30));
        order.MarkRefunded(now.AddMinutes(30));

        Assert.Equal(MarketplaceOrderStatus.Expired, order.Status);
        Assert.Equal(now.AddMinutes(30), order.RefundedAt);
        Assert.Throws<DomainException>(() => order.Accept(now.AddMinutes(31)));
    }

    [Fact]
    public void Expire_WhenAccepted_ThrowsDomainException()
    {
        var now = DateTimeOffset.UtcNow;
        var order = CreateOrder(now);
        order.Accept(now.AddMinutes(1));

        Assert.Throws<DomainException>(() => order.Expire(now.AddMinutes(30)));
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
