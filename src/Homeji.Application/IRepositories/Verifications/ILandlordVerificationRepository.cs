using Homeji.Domain.Entities;
using Homeji.Domain.Enums;

namespace Homeji.Application.IRepositories.Verifications;

public interface ILandlordVerificationRepository
{
    Task AddAsync(LandlordVerificationRequest request, CancellationToken cancellationToken = default);
    Task<LandlordVerificationRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<LandlordVerificationRequest?> GetLatestForApplicantAsync(Guid applicantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LandlordVerificationRequest>> GetByStatusAsync(LandlordVerificationStatus status, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
