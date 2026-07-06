using Homeji.Domain.Enums;

namespace Homeji.Application.DTOs.Reports;

public sealed record ReportDto(
    Guid Id,
    Guid ReporterId,
    ReportTargetType TargetType,
    Guid TargetId,
    string Reason,
    string? Description,
    ReportStatus Status,
    string? ResolutionNote,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
