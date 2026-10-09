using Homeji.Application.DTOs.RentalPosts;

namespace Homeji.Application.DTOs.AI;

public sealed record AiHighlightedRentalPostDto(
    RentalPostSummaryDto Post,
    decimal Score,
    IReadOnlyCollection<string> Reasons,
    string Tag)
{
    public IReadOnlyCollection<AiEvidenceDto> Evidence { get; init; } = [];
    public decimal UserFit => Score;
    public decimal CommercialBoost => Post.BoostScore;
}

public sealed record AiEvidenceDto(Guid PostId, string SourceType, string Field, string Text, DateTimeOffset UpdatedAt);
