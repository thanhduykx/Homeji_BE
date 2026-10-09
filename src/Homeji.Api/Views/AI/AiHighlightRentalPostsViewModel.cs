namespace Homeji.Api.Views.AI;

public sealed record AiHighlightRentalPostsViewModel(
    string? Text,
    int MaxResults = 5,
    Homeji.Application.DTOs.AI.AiParsedSearchCriteriaDto? Intent = null);
