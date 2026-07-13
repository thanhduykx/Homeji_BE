using Homeji.Application.IRepositories.Verifications;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Infrastructure.Repositories;

public sealed class LandlordVerificationRepository : ILandlordVerificationRepository
{
    private readonly ApplicationDbContext _dbContext;

    public LandlordVerificationRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(LandlordVerificationRequest request, CancellationToken cancellationToken = default)
    {
        await _dbContext.LandlordVerificationRequests.AddAsync(request, cancellationToken);
    }

    public Task<LandlordVerificationRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.LandlordVerificationRequests.SingleOrDefaultAsync(request => request.Id == id, cancellationToken);
    }

    public Task<LandlordVerificationRequest?> GetLatestForApplicantAsync(
        Guid applicantId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.LandlordVerificationRequests
            .AsNoTracking()
            .Where(request => request.ApplicantId == applicantId)
            .OrderByDescending(request => request.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LandlordVerificationRequest>> GetByStatusAsync(
        LandlordVerificationStatus status,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.LandlordVerificationRequests
            .AsNoTracking()
            .Where(request => request.Status == status)
            .OrderBy(request => request.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
