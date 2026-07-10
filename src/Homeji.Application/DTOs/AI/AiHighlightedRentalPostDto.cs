using Homeji.Application.DTOs.RentalPosts;

namespace Homeji.Application.DTOs.AI;

public sealed record AiHighlightedRentalPostDto(
    RentalPostSummaryDto Post,
    decimal Score,
    IReadOnlyCollection<string> Reasons,
    string Tag);
