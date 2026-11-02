namespace Homeji.Application.DTOs.RentalPosts;

public sealed record RentalSourceListingDto(
    Guid Id, string Source, string SourceId, string SourceUrl, string Title,
    string Address, string District, decimal Price, decimal Area,
    IReadOnlyList<string> ImageUrls, DateTimeOffset? SourceUpdatedAt, DateTimeOffset CollectedAt)
{
    public string? PublisherName { get; init; }
    public string? Description { get; init; }
    public string? ContactPhone { get; init; }
    public DateTimeOffset? DetailsCollectedAt { get; init; }
    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }
    public DateTimeOffset? SourceExpiresAt { get; init; }
    public DateTimeOffset? SourceCheckedAt { get; init; }
    public string? DepositLabel { get; init; }
    public IReadOnlyList<string> AmenityHighlights { get; init; } = [];
}

public sealed record RentalSourceContactDto(string? Phone);

public sealed record RentalSourceSearchDto(
    string? Keyword, string? District, decimal? MinPrice, decimal? MaxPrice, int Page = 1, int PageSize = 20,
    string? PropertyCategory = null);
