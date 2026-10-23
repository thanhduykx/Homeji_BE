using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Chatbot;
using Homeji.Application.DTOs.Marketplace;
using Homeji.Application.IServices.Marketplace;
using Homeji.Application.Services.AI;
using Homeji.Application.Services.Chatbot;
using Homeji.Application.Services.Common;
using Homeji.Domain.Enums;

namespace Homeji.Application.UnitTests.Chatbot;

public sealed class ChatbotFoodServiceTests
{
    [Theory]
    [InlineData("CÓ GÌ ĂN GIỚI THIỆU ĐI", true)]
    [InlineData("GIỚI THIỆU ĐỒ ĂN NGON GẦN TUI", true)]
    [InlineData("Đồ ăn gần phòng này dưới 40k", true)]
    [InlineData("Có cơm gà không?", true)]
    [InlineData("Gợi ý phòng có bếp nấu ăn", false)]
    [InlineData("Tìm phòng gần quán ăn", false)]
    [InlineData("Phòng này có tiện nấu cơm không?", false)]
    [InlineData("Cách đặt đồ ăn gần đây?", false)]
    public void FoodDiscoveryHasPriorityWithoutStealingHousingOrSupport(string message, bool expected)
    {
        Assert.Equal(expected, FoodSearchIntent.IsDiscovery(message));
        if (FoodSearchIntent.IsFoodTopic(message)) Assert.False(RentalSearchIntent.IsRentalQuery(message));
    }

    [Fact]
    public async Task NearbyRequiresExplicitOriginAndDoesNotQueryUnlocatedInventory()
    {
        var marketplace = new MarketplaceStub();
        var result = await new ChatbotFoodService(marketplace).SearchAsync("GIỚI THIỆU ĐỒ ĂN NGON GẦN TUI", null);
        Assert.Equal("locationRequired", result.Status);
        Assert.Empty(result.Items);
        Assert.Null(marketplace.Request);
    }

    [Fact]
    public async Task FoodQueryAppliesTypeBudgetAndGeoBeforePagingAndReturnsMinimalCurrentFacts()
    {
        var marketplace = new MarketplaceStub { Posts = [Food()] };
        var origin = new ChatbotFoodContextDto(10.84m, 106.8m, 3);
        var result = await new ChatbotFoodService(marketplace).SearchAsync("Gợi ý cơm gà gần tui dưới 40k", origin);
        var query = Assert.IsType<MarketplaceSearchDto>(marketplace.Request);
        Assert.Equal(MarketplaceListingType.Food, query.ListingType);
        Assert.Equal("cơm gà", query.Keyword);
        Assert.Equal(40_000, query.MaxPrice);
        Assert.Equal(origin.Latitude, query.Latitude); Assert.Equal(origin.Longitude, query.Longitude);
        Assert.Equal(3, query.RadiusKm); Assert.Equal(1, query.Page); Assert.Equal(5, query.PageSize);
        Assert.Null(query.NearRentalPostId); Assert.Null(query.SellerId);
        Assert.Equal("results", result.Status);
        var item = Assert.Single(result.Items);
        Assert.Equal("Cơm gà", item.Title); Assert.Equal(35_000, item.Price); Assert.Equal(2, item.AvailableQuantity);
        Assert.Equal(0.5m, item.DistanceKm); Assert.Equal(origin, result.Origin);
        Assert.DoesNotContain("private", System.Text.Json.JsonSerializer.Serialize(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task NeverLabelsGlobalInventoryAsNearbyOrReturnsOtherTypesHiddenSoldSyntheticOrOverBudget()
    {
        var item = Food();
        var marketplace = new MarketplaceStub { Posts = [item,
            item with { Id = Guid.NewGuid(), ListingType = MarketplaceListingType.SecondHand },
            item with { Id = Guid.NewGuid(), Status = MarketplacePostStatus.Archived },
            item with { Id = Guid.NewGuid(), IsSynthetic = true },
            item with { Id = Guid.NewGuid(), AvailableQuantity = 0 },
            item with { Id = Guid.NewGuid(), Price = 50_000 }] };
        var result = await new ChatbotFoodService(marketplace).SearchAsync("Có gì ăn dưới 40k", null);
        Assert.Null(Assert.Single(result.Items).DistanceKm); Assert.Null(result.Origin);
        Assert.Null(marketplace.Request!.Latitude); Assert.Null(marketplace.Request.RadiusKm);
        marketplace.Posts = [item with { DistanceKm = null }, item with { DistanceKm = 4 }];
        result = await new ChatbotFoodService(marketplace).SearchAsync("Có gì ăn gần tui", new(10.84m, 106.8m));
        Assert.Equal("empty", result.Status); Assert.Empty(result.Items);
    }

    [Theory]
    [InlineData("Đồ ăn chay gần tôi")]
    [InlineData("Gợi ý đồ ăn không cay")]
    [InlineData("Đồ ăn cho người dị ứng sữa")]
    [InlineData("Đồ ăn giá 30k")]
    [InlineData("Đồ ăn dưới 79228162514264337593543950335k")]
    public async Task UnsupportedDietaryOrBudgetConditionsRequireClarificationInsteadOfIgnoringThem(string message)
    {
        var marketplace = new MarketplaceStub { Posts = [Food()] };
        var result = await new ChatbotFoodService(marketplace).SearchAsync(message, null);
        Assert.Equal("clarificationRequired", result.Status); Assert.Empty(result.Items); Assert.Null(marketplace.Request);
    }

    [Theory]
    [InlineData(91, 106, 3)]
    [InlineData(10, 181, 3)]
    [InlineData(10, 106, 0)]
    [InlineData(10, 106, 11)]
    public async Task InvalidOriginIsRejectedBeforeAnyQuery(decimal latitude, decimal longitude, decimal radius)
    {
        var marketplace = new MarketplaceStub();
        await Assert.ThrowsAsync<RequestValidationException>(() => new ChatbotFoodService(marketplace).SearchAsync("Có gì ăn", new(latitude, longitude, radius)));
        Assert.Null(marketplace.Request);
    }

    [Fact]
    public async Task UnavailableCatalogueDoesNotInventFoodAndCancellationPropagates()
    {
        var marketplace = new MarketplaceStub { Error = new HttpRequestException("offline") };
        var service = new ChatbotFoodService(marketplace);
        var result = await service.SearchAsync("Có gì ăn", null);
        Assert.Equal("unavailable", result.Status); Assert.Empty(result.Items);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        marketplace.Error = new OperationCanceledException(cancellation.Token);
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.SearchAsync("Có gì ăn", null, cancellation.Token));
    }

    private static MarketplacePostDto Food() => new(Guid.NewGuid(), Guid.NewGuid(), "Bếp A", "private phone",
        MarketplacePostStatus.Active, "Cơm gà", "private description", 35_000, "", "", "private address",
        10.84m, 106.8m, null, [], 0.5m, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
        MarketplaceListingType.Food, 2, 1, "phần", 15);

    private sealed class MarketplaceStub : IMarketplacePostService
    {
        public MarketplaceSearchDto? Request { get; private set; }
        public IReadOnlyList<MarketplacePostDto> Posts { get; set; } = [];
        public Exception? Error { get; set; }
        public Task<IReadOnlyList<MarketplacePostDto>> SearchAsync(MarketplaceSearchDto request, CancellationToken cancellationToken = default)
        { Request = request; return Error is null ? Task.FromResult(Posts) : Task.FromException<IReadOnlyList<MarketplacePostDto>>(Error); }
        public Task<IReadOnlyList<MarketplacePostDto>> GetMineAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MarketplacePostDto> GetDetailAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MarketplacePostDto> CreateAsync(UpsertMarketplacePostDto request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MarketplacePostDto> UpdateAsync(Guid id, UpsertMarketplacePostDto request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task MarkSoldAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ArchiveAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
