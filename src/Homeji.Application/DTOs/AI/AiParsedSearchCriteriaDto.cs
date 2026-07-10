namespace Homeji.Application.DTOs.AI;

public sealed record AiParsedSearchCriteriaDto(
    string? Location,
    string? Keyword,
    decimal? PriceMin,
    decimal? PriceMax,
    decimal? AreaMin,
    decimal? AreaMax,
    IReadOnlyCollection<string> Criteria);
