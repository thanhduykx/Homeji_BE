using Homeji.Application.DTOs.MarketplaceOrders;
using Homeji.Application.Services.MarketplaceOrders.Validation;

namespace Homeji.Application.UnitTests.Marketplace;

public sealed class CreateMarketplaceCartOrderDtoValidatorTests
{
    private static readonly DateTimeOffset UtcNow = new(2026, 7, 16, 12, 0, 0, TimeSpan.Zero);
    private readonly CreateMarketplaceCartOrderDtoValidator _validator = new(new StubTimeProvider());

    [Fact]
    public async Task ValidateAsync_ValidSingleSellerCartInput_Passes()
    {
        var request = new CreateMarketplaceCartOrderDto(
            [new MarketplaceCartItemDto(Guid.NewGuid(), 2)],
            UtcNow.AddHours(1),
            "Bếp Homeji",
            "Ít cay");

        var result = await _validator.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_DuplicatePost_RejectsCart()
    {
        var postId = Guid.NewGuid();
        var request = new CreateMarketplaceCartOrderDto(
            [new MarketplaceCartItemDto(postId, 1), new MarketplaceCartItemDto(postId, 2)],
            UtcNow.AddHours(1),
            "Bếp Homeji",
            null);

        var result = await _validator.ValidateAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(request.Items));
    }

    [Fact]
    public async Task ValidateAsync_EmptyCart_RejectsCart()
    {
        var request = new CreateMarketplaceCartOrderDto(
            [],
            UtcNow.AddHours(1),
            "Bếp Homeji",
            null);

        var result = await _validator.ValidateAsync(request);

        Assert.False(result.IsValid);
    }

    private sealed class StubTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
