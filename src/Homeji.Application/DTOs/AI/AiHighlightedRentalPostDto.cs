using Homeji.Application.DTOs.RentalPosts;

namespace Homeji.Application.DTOs.AI;

public sealed record AiHighlightedRentalPostDto(
    RentalPostSummaryDto Post,
    decimal Score,
    IReadOnlyCollection<string> Reasons,
    string Tag)
{
    public IReadOnlyCollection<AiRentalEvidenceDto> Evidence { get; init; } = [];
    public IReadOnlyCollection<AiRentalReasonDto> ReasonEvidence { get; init; } = [];
    public DateTimeOffset? UpdatedAt { get; init; }
    public decimal CommercialBoost { get; init; }
    public IReadOnlyCollection<string> UnconfirmedConstraints { get; init; } = [];
}

public sealed record AiRentalEvidenceDto(Guid PostId, string SourceType, string Field, string Value);
public sealed record AiRentalReasonDto(string Text, Guid PostId, string SourceType, string Field, string Value);
