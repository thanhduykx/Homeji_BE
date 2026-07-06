using Homeji.Application.DTOs.Reports;
using Homeji.Domain.Entities;

namespace Homeji.Application.Mappers.Reports;

public static class ReportMapper
{
    public static ReportDto ToDto(Report report)
    {
        return new ReportDto(
            report.Id,
            report.ReporterId,
            report.TargetType,
            report.TargetId,
            report.Reason,
            report.Description,
            report.Status,
            report.ResolutionNote,
            report.CreatedAt,
            report.UpdatedAt);
    }
}
