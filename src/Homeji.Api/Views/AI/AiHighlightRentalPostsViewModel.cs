using Homeji.Application.DTOs.AI;

namespace Homeji.Api.Views.AI;

public sealed record AiHighlightRentalPostsViewModel(
    string? Text,
    int MaxResults = 5,
    AiParsedSearchCriteriaDto? PreviousCriteria = null);
