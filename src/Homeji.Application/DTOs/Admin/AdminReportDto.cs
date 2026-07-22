using Homeji.Domain.Enums;

namespace Homeji.Application.DTOs.Admin;

public sealed record AdminReportDto(
    Guid Id,
    Guid ReporterId,
    string ReporterDisplayName,
    ReportTargetType TargetType,
    Guid TargetId,
    string TargetDisplayName,
    Guid? RelatedRentalPostId,
    string Reason,
    string? Description,
    ReportStatus Status,
    string? ResolutionNote,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
