namespace Homeji.Application.DTOs.RentalPosts;

public sealed record RentalSourceListingDto(
    Guid Id, string Source, string SourceId, string SourceUrl, string Title,
    string Address, string District, decimal Price, decimal Area,
    IReadOnlyList<string> ImageUrls, DateTimeOffset? SourceUpdatedAt, DateTimeOffset CollectedAt);

public sealed record RentalSourceSearchDto(
    string? Keyword, string? District, decimal? MinPrice, decimal? MaxPrice, int Page = 1, int PageSize = 20);
