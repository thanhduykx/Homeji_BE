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
    public string BudgetKind { get; init; } = "rent";
    public int? Occupants { get; init; }
    public bool ExcludeShared { get; init; }
    public IReadOnlyCollection<string> RequiredAmenities { get; init; } = [];
    public IReadOnlyCollection<string> ExcludedAmenities { get; init; } = [];
    public IReadOnlyCollection<string> Unknown { get; init; } = [];
    public string? Destination { get; init; }
    public int? MaxCommuteMinutes { get; init; }
}
