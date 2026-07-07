using Homeji.Application.IRepositories.Reports;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Infrastructure.Repositories;

public sealed class ReportRepository : IReportRepository
{
    private readonly ApplicationDbContext _dbContext;

    public ReportRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Report?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Reports.SingleOrDefaultAsync(report => report.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Report>> GetByStatusAsync(ReportStatus? status, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Reports.AsNoTracking();
        if (status.HasValue)
        {
            query = query.Where(report => report.Status == status.Value);
        }

        return await query.OrderByDescending(report => report.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Report report, CancellationToken cancellationToken = default)
    {
        await _dbContext.Reports.AddAsync(report, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
