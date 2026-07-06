using Homeji.Domain.Enums;
using Homeji.Domain.Exceptions;

namespace Homeji.Domain.Entities;

public sealed class Report
{
    public const int MaxReasonLength = 120;
    public const int MaxDescriptionLength = 1_000;
    public const int MaxResolutionNoteLength = 500;

    private Report()
    {
        Reason = null!;
    }

    public Report(
        Guid reporterId,
        ReportTargetType targetType,
        Guid targetId,
        string reason,
        string? description,
        DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        ReporterId = reporterId;
        TargetType = targetType;
        TargetId = targetId;
        Reason = NormalizeRequired(reason, MaxReasonLength, nameof(Reason));
        Description = NormalizeOptional(description, MaxDescriptionLength, nameof(Description));
        Status = ReportStatus.New;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid ReporterId { get; private set; }

    public ReportTargetType TargetType { get; private set; }

    public Guid TargetId { get; private set; }

    public string Reason { get; private set; }

    public string? Description { get; private set; }

    public ReportStatus Status { get; private set; }

    public string? ResolutionNote { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void Resolve(string? note, DateTimeOffset updatedAt)
    {
        Status = ReportStatus.Resolved;
        ResolutionNote = NormalizeOptional(note, MaxResolutionNoteLength, nameof(note));
        UpdatedAt = updatedAt;
    }

    public void Reject(string? note, DateTimeOffset updatedAt)
    {
        Status = ReportStatus.Rejected;
        ResolutionNote = NormalizeOptional(note, MaxResolutionNoteLength, nameof(note));
        UpdatedAt = updatedAt;
    }

    private static string NormalizeRequired(string value, int maxLength, string fieldName)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new DomainException($"{fieldName} is required.");
        }

        if (normalized.Length > maxLength)
        {
            throw new DomainException($"{fieldName} must not exceed {maxLength} characters.");
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength, string fieldName)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (normalized.Length > maxLength)
        {
            throw new DomainException($"{fieldName} must not exceed {maxLength} characters.");
        }

        return normalized;
    }
}
