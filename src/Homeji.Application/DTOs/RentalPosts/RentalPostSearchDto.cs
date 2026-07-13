namespace Homeji.Application.DTOs.RentalPosts;

public sealed record RentalPostSearchDto(
    string? Keyword,
    decimal? MinPrice,
    decimal? MaxPrice,
    decimal? MinArea,
    decimal? MaxArea,
    decimal? MinLatitude,
    decimal? MaxLatitude,
    decimal? MinLongitude,
    decimal? MaxLongitude,
    IReadOnlyCollection<string> Amenities,
    int Page = 1,
    int PageSize = 20,
    decimal? MaxDeposit = null,
    int? MinAvailableSlots = null,
    DateOnly? AvailableFromBefore = null);
