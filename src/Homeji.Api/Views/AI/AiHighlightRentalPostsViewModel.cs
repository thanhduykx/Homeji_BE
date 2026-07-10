namespace Homeji.Api.Views.AI;

public sealed record AiHighlightRentalPostsViewModel(
    string? Text,
    int MaxResults = 5);
