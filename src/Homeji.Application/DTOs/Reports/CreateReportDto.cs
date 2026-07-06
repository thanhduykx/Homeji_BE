using Homeji.Domain.Enums;

namespace Homeji.Application.DTOs.Reports;

public sealed record CreateReportDto(
    ReportTargetType TargetType,
    Guid TargetId,
    string? Reason,
    string? Description);
