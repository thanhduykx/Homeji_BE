using Homeji.Application.DTOs.MarketplaceOrders;
using Homeji.Application.Services.MarketplaceOrders.Validation;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Domain.Exceptions;

namespace Homeji.Application.UnitTests.Marketplace;

public sealed class MarketplaceFulfillmentTests
{
    [Fact]
    public void Legacy_order_defaults_to_pickup_and_independent_checkout()
    {
        var first = Order();
        var second = Order();
        Assert.Equal(MarketplaceFulfillmentType.Pickup, first.FulfillmentType);
        Assert.Null(first.DeliveryAddress);
        Assert.NotEqual(first.CheckoutId, second.CheckoutId);
    }

    [Fact]
    public void Seller_delivery_stores_recipient_snapshot()
    {
        var order = Order(new MarketplaceDelivery(" Sinh viên ", "0901234567", " KTX Thủ Đức ", 10.85m, 106.77m));
        Assert.Equal(MarketplaceFulfillmentType.SellerDelivery, order.FulfillmentType);
        Assert.Equal("Sinh viên", order.RecipientName);
        Assert.Equal("KTX Thủ Đức", order.DeliveryAddress);
        Assert.Equal(10.85m, order.DeliveryLatitude);
    }

    [Theory]
    [InlineData(0, false, true)]
    [InlineData(0, true, false)]
    [InlineData(1, false, false)]
    [InlineData(1, true, true)]
    [InlineData(99, false, false)]
    public void Mode_and_delivery_must_agree(int mode, bool hasDelivery, bool valid)
    {
        var request = new MarketplaceFulfillmentDto((MarketplaceFulfillmentType)mode, hasDelivery
            ? new MarketplaceDeliveryDto("Sinh viên", "0901234567", "KTX Thủ Đức", 10.85m, 106.77m) : null);
        Assert.Equal(valid, new MarketplaceFulfillmentDtoValidator().Validate(request).IsValid);
    }

    [Theory]
    [InlineData("<script>", 10.85, 106.77)]
    [InlineData("0901234567", 21.03, 105.85)]
    public void Invalid_contact_or_outside_service_area_is_rejected(string phone, double latitude, double longitude)
    {
        var request = new MarketplaceFulfillmentDto(MarketplaceFulfillmentType.SellerDelivery,
            new MarketplaceDeliveryDto("Sinh viên", phone, "Địa chỉ", (decimal)latitude, (decimal)longitude));
        Assert.False(new MarketplaceFulfillmentDtoValidator().Validate(request).IsValid);
    }

    [Fact]
    public void Domain_rejects_delivery_without_recipient()
    {
        Assert.Throws<DomainException>(() => Order(new MarketplaceDelivery("", "0901234567", "KTX", 10.85m, 106.77m)));
    }

    private static MarketplaceOrder Order(MarketplaceDelivery? delivery = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new MarketplaceOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 30_000,
            now.AddHours(1), "Nhận tại tiệm", null, now,
            fulfillmentType: delivery is null ? MarketplaceFulfillmentType.Pickup : MarketplaceFulfillmentType.SellerDelivery,
            delivery: delivery);
    }
}
