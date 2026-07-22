using Homeji.Application.Abstractions.Notifications;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Verifications;
using Homeji.Application.IRepositories.Notifications;
using Homeji.Application.IRepositories.Profiles;
using Homeji.Application.IRepositories.Verifications;
using Homeji.Application.IServices.Verifications;
using Homeji.Application.Services.Common;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;

namespace Homeji.Application.Services.Verifications;

public sealed class LandlordVerificationService : ILandlordVerificationService
{
    private readonly UserContext _userContext;
    private readonly ILandlordVerificationRepository _requests;
    private readonly IUserProfileRepository _profiles;
    private readonly INotificationRepository _notifications;
    private readonly INotificationRealtimePublisher _realtimePublisher;
    private readonly TimeProvider _timeProvider;

    public LandlordVerificationService(
        UserContext userContext,
        ILandlordVerificationRepository requests,
        IUserProfileRepository profiles,
        INotificationRepository notifications,
        INotificationRealtimePublisher realtimePublisher,
        TimeProvider timeProvider)
    {
        _userContext = userContext;
        _requests = requests;
        _profiles = profiles;
        _notifications = notifications;
        _realtimePublisher = realtimePublisher;
        _timeProvider = timeProvider;
    }

    public async Task<LandlordVerificationDto> SubmitAsync(
        SubmitLandlordVerificationDto request,
        CancellationToken cancellationToken = default)
    {
        var profile = await _userContext.GetRequiredProfileAsync(cancellationToken);
        ValidateDocumentUrl(request.DocumentUrl);
        var now = _timeProvider.GetUtcNow();
        profile.SubmitLandlordVerification(now);
        var verification = new LandlordVerificationRequest(profile.Id, request.DocumentUrl!, request.Note, now);
        await _requests.AddAsync(verification, cancellationToken);
        await _profiles.SaveAsync(profile, cancellationToken);
        return ToDto(verification, profile.DisplayName);
    }

    public async Task<LandlordVerificationDto?> GetMineAsync(CancellationToken cancellationToken = default)
    {
        var profile = await _userContext.GetRequiredProfileAsync(cancellationToken);
        var request = await _requests.GetLatestForApplicantAsync(profile.Id, cancellationToken);
        return request is null ? null : ToDto(request, profile.DisplayName);
    }

    public async Task<IReadOnlyList<LandlordVerificationDto>> GetForAdminAsync(
        LandlordVerificationStatus status,
        CancellationToken cancellationToken = default)
    {
        var admin = await _userContext.GetRequiredProfileAsync(cancellationToken);
        UserContext.EnsureAdmin(admin);
        var requests = await _requests.GetByStatusAsync(status, cancellationToken);
        var profiles = await _profiles.GetByIdsAsync(
            requests.Select(request => request.ApplicantId).Distinct().ToArray(),
            cancellationToken);
        var names = profiles.ToDictionary(profile => profile.Id, profile => profile.DisplayName);
        return requests.Select(request => ToDto(
            request,
            names.GetValueOrDefault(request.ApplicantId) ?? "Homeji user")).ToArray();
    }

    public async Task<LandlordVerificationDto> ReviewAsync(
        Guid id,
        ReviewLandlordVerificationDto request,
        CancellationToken cancellationToken = default)
    {
        var admin = await _userContext.GetRequiredProfileAsync(cancellationToken);
        UserContext.EnsureAdmin(admin);
        var verification = await _requests.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(LandlordVerificationRequest), id);
        var applicant = await _profiles.GetByIdAsync(verification.ApplicantId, cancellationToken)
            ?? throw new NotFoundException(nameof(UserProfile), verification.ApplicantId);
        var now = _timeProvider.GetUtcNow();
        verification.Review(request.Approved, admin.Id, request.Note, now);
        applicant.CompleteLandlordVerification(request.Approved, now);

        var notification = new Notification(
            applicant.Id,
            NotificationType.LandlordVerificationUpdated,
            "Hồ sơ xác minh chủ trọ đã cập nhật",
            request.Approved
                ? "Hồ sơ chủ trọ của bạn đã được xác minh."
                : "Hồ sơ chủ trọ của bạn chưa được chấp thuận. Vui lòng xem ghi chú và gửi lại.",
            verification.Id,
            now);
        await _notifications.AddAsync(notification, cancellationToken);
        await _profiles.SaveAsync(applicant, cancellationToken);
        await _realtimePublisher.PublishAsync(notification, cancellationToken);
        return ToDto(verification, applicant.DisplayName);
    }

    private static void ValidateDocumentUrl(string? documentUrl)
    {
        if (!Uri.TryCreate(documentUrl, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || documentUrl!.Length > LandlordVerificationRequest.MaxDocumentUrlLength)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["documentUrl"] = ["Cần URL giấy tờ HTTPS hợp lệ."],
            });
        }
    }

    private static LandlordVerificationDto ToDto(LandlordVerificationRequest request, string displayName)
    {
        return new LandlordVerificationDto(
            request.Id,
            request.ApplicantId,
            displayName,
            request.DocumentUrl,
            request.ApplicantNote,
            request.Status,
            request.ReviewNote,
            request.ReviewedBy,
            request.CreatedAt,
            request.UpdatedAt);
    }
}
