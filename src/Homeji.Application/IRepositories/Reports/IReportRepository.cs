using Homeji.Domain.Entities;
using Homeji.Domain.Enums;

namespace Homeji.Application.IRepositories.Reports;

public interface IReportRepository
{
    Task<Report?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Report>> GetByStatusAsync(ReportStatus? status, CancellationToken cancellationToken = default);

    Task AddAsync(Report report, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
