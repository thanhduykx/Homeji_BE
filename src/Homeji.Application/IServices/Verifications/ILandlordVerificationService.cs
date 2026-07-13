using Homeji.Application.DTOs.Verifications;
using Homeji.Domain.Enums;

namespace Homeji.Application.IServices.Verifications;

public interface ILandlordVerificationService
{
    Task<LandlordVerificationDto> SubmitAsync(SubmitLandlordVerificationDto request, CancellationToken cancellationToken = default);
    Task<LandlordVerificationDto?> GetMineAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LandlordVerificationDto>> GetForAdminAsync(LandlordVerificationStatus status, CancellationToken cancellationToken = default);
    Task<LandlordVerificationDto> ReviewAsync(Guid id, ReviewLandlordVerificationDto request, CancellationToken cancellationToken = default);
}
