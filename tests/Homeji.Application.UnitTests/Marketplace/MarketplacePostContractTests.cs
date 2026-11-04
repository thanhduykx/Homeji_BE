using Homeji.Application.DTOs.Marketplace;

namespace Homeji.Application.UnitTests.Marketplace;

public sealed class MarketplacePostContractTests
{
    // Search and detail are anonymous endpoints, so this DTO must never carry contact numbers.
    [Fact]
    public void Public_marketplace_post_has_no_phone_field() =>
        Assert.DoesNotContain(typeof(MarketplacePostDto).GetProperties(),
            property => property.Name.Contains("Phone", StringComparison.OrdinalIgnoreCase));
}
