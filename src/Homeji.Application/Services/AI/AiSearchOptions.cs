namespace Homeji.Application.Services.AI;

public sealed class AiSearchOptions
{
    public const string SectionName = "Ai";

    public int MaxHighlightedPosts { get; set; } = 5;
    public bool GroundedSearchEnabled { get; set; } = true;
}
