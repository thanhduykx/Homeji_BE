using Homeji.Application.DTOs.AI;
namespace Homeji.Application.IServices.AI;
public interface IRentalDraftService
{
    Task<RentalDraftResponseDto> GenerateAsync(RentalDraftRequestDto request, CancellationToken cancellationToken = default);
}
