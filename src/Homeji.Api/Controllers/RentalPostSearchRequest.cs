using Homeji.Domain.Enums;

namespace Homeji.Api.Controllers;

/// <summary>
/// Query-string filters for the public rental post search endpoint.
/// Property names must keep matching the query keys the frontend sends.
/// </summary>
public sealed record RentalPostSearchRequest
{
    public string? Keyword { get; init; }

    public decimal? MinPrice { get; init; }

    public decimal? MaxPrice { get; init; }

    public decimal? MinArea { get; init; }

    public decimal? MaxArea { get; init; }

    public decimal? MinLatitude { get; init; }

    public decimal? MaxLatitude { get; init; }

    public decimal? MinLongitude { get; init; }

    public decimal? MaxLongitude { get; init; }

    public string[]? Amenities { get; init; }

    public decimal? MaxDeposit { get; init; }

    public int? MinAvailableSlots { get; init; }

    public DateOnly? AvailableFromBefore { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public string[]? ExcludedAmenities { get; init; }

    public bool ExcludeRoommateShare { get; init; }

    public Guid[]? Ids { get; init; }

    public RentalPostType? Type { get; init; }

    public UserRole? OwnerRole { get; init; }

    public string? PropertyCategory { get; init; }
}
