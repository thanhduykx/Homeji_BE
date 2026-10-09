namespace Homeji.Application.DTOs.AI;

public sealed record AiParsedSearchCriteriaDto(
    string? Location,
    string? Keyword,
    decimal? PriceMin,
    decimal? PriceMax,
    decimal? AreaMin,
    decimal? AreaMax,
    IReadOnlyCollection<string> Criteria)
{
    public IReadOnlyCollection<string> RequiredAmenities { get; init; } = [];
    public IReadOnlyCollection<string> ExcludedAmenities { get; init; } = [];
    public IReadOnlyCollection<string> Unknown { get; init; } = [];
    public int? Occupants { get; init; }
    public bool ExcludeRoommateShare { get; init; }
    public string BudgetBasis { get; init; } = "rent";
    public string? Destination { get; init; }
    public int? MaxCommuteMinutes { get; init; }
    public string? TravelMode { get; init; }
}
