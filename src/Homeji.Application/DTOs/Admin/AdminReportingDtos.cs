using Homeji.Domain.Enums;

namespace Homeji.Application.DTOs.Admin;

public sealed record AdminPageDto<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
public sealed record AdminRegisteredAccountDto(Guid UserId, string DisplayName, string? Email, int? Role,
    DateTimeOffset RegisteredAt, bool EmailConfirmed, bool ProfileCompleted, bool IsPremium, DateTimeOffset? PremiumExpiresAt);
public sealed record AdminMemberDto(Guid Id, Guid UserId, string DisplayName, string? Email, string PackageCode,
    string PackageName, DateTimeOffset StartedAt, DateTimeOffset ExpiresAt, DateTimeOffset RegisteredAt,
    string State, Guid? PaymentId, decimal? Amount, PaymentStatus? PaymentStatus, PaymentMethod? PaymentMethod);
public sealed record CreateAdminReportDto(Guid RequestId, int Days = 30);
public sealed record AdminReportExportDto(Guid Id, string FileName, int PeriodDays, DateTimeOffset CreatedAt,
    string Recipient, string EmailStatus, DateTimeOffset? EmailAcceptedAt, int Attempts, string? LastErrorCode);
public sealed record AdminReportFileDto(byte[] Content, string FileName);
public sealed record AdminReportingSnapshot(IReadOnlyList<AdminRegisteredAccountDto> Accounts,
    IReadOnlyList<AdminMemberDto> Members, AdminProductAnalyticsDto Analytics);
