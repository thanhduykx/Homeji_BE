using Homeji.Application.DTOs.Reports;

namespace Homeji.Application.IServices.Reports;

public interface IReportService
{
    Task<ReportDto> CreateAsync(CreateReportDto request, CancellationToken cancellationToken = default);
}
