using Homeji.Application.DTOs.AI;

namespace Homeji.Application.IServices.AI;

public interface IRentalDecisionService
{
    Task<RentalDecisionResponseDto> CompareAsync(RentalDecisionRequestDto request, CancellationToken cancellationToken);
    Task<RentalDraftPreviewDto> PreviewDraftAsync(RentalDraftPreviewRequestDto request, CancellationToken cancellationToken);
    Task<AdminAssistantSummaryDto> GetAdminSummaryAsync(CancellationToken cancellationToken);
}
