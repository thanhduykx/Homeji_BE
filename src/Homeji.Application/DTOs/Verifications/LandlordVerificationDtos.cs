using Homeji.Domain.Enums;

namespace Homeji.Application.DTOs.Verifications;

public sealed record SubmitLandlordVerificationDto(string? DocumentUrl, string? Note);

public sealed record ReviewLandlordVerificationDto(bool Approved, string? Note);

public sealed record LandlordVerificationDto(
    Guid Id,
    Guid ApplicantId,
    string ApplicantDisplayName,
    string DocumentUrl,
    string? ApplicantNote,
    LandlordVerificationStatus Status,
    string? ReviewNote,
    Guid? ReviewedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
