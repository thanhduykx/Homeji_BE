using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Reports;
using Homeji.Application.IRepositories.RentalPosts;
using Homeji.Application.IRepositories.Reports;
using Homeji.Application.IRepositories.Profiles;
using Homeji.Application.IRepositories.Marketplace;
using Homeji.Application.IRepositories.Reviews;
using Homeji.Application.IRepositories.WantedPosts;
using Homeji.Application.IRepositories.Roommates;
using Homeji.Application.IServices.Reports;
using Homeji.Application.Mappers.Reports;
using Homeji.Application.Services.Common;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;

namespace Homeji.Application.Services.Reports;

public sealed class ReportService : IReportService
{
    private readonly UserContext _userContext;
    private readonly IReportRepository _reports;
    private readonly IRentalPostRepository _posts;
    private readonly TimeProvider _timeProvider;
    private readonly IUserProfileRepository _profiles;
    private readonly IMarketplacePostRepository _marketplacePosts;
    private readonly IRentalReviewRepository _reviews;
    private readonly IRentalWantedPostRepository _wantedPosts;
    private readonly IRoommateInvitationRepository _roommateInvitations;

    public ReportService(
        UserContext userContext,
        IReportRepository reports,
        IRentalPostRepository posts,
        IUserProfileRepository profiles,
        IMarketplacePostRepository marketplacePosts,
        IRentalReviewRepository reviews,
        IRentalWantedPostRepository wantedPosts,
        IRoommateInvitationRepository roommateInvitations,
        TimeProvider timeProvider)
    {
        _userContext = userContext;
        _reports = reports;
        _posts = posts;
        _profiles = profiles;
        _marketplacePosts = marketplacePosts;
        _reviews = reviews;
        _wantedPosts = wantedPosts;
        _roommateInvitations = roommateInvitations;
        _timeProvider = timeProvider;
    }

    public async Task<ReportDto> CreateAsync(CreateReportDto request, CancellationToken cancellationToken = default)
    {
        var reporterId = _userContext.GetRequiredUserId();
        if (request.TargetType == ReportTargetType.User && request.TargetId == reporterId)
        {
            throw new ForbiddenAccessException("Bạn không thể tự báo cáo tài khoản của mình.");
        }

        if (!await TargetExistsAsync(request.TargetType, request.TargetId, cancellationToken))
        {
            throw new NotFoundException(request.TargetType.ToString(), request.TargetId);
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["reason"] = ["Lý do là bắt buộc."],
            });
        }

        var report = new Report(
            reporterId,
            request.TargetType,
            request.TargetId,
            request.Reason!,
            request.Description,
            _timeProvider.GetUtcNow());
        await _reports.AddAsync(report, cancellationToken);
        await _reports.SaveChangesAsync(cancellationToken);
        return ReportMapper.ToDto(report);
    }

    private async Task<bool> TargetExistsAsync(
        ReportTargetType targetType,
        Guid targetId,
        CancellationToken cancellationToken)
    {
        return targetType switch
        {
            ReportTargetType.RentalPost => await _posts.GetByIdAsync(targetId, cancellationToken) is not null,
            ReportTargetType.User => await _profiles.GetByIdAsync(targetId, cancellationToken) is not null,
            ReportTargetType.MarketplacePost => await _marketplacePosts.GetByIdWithMediaAsync(targetId, cancellationToken) is not null,
            ReportTargetType.RentalReview => await _reviews.GetByIdAsync(targetId, cancellationToken) is not null,
            ReportTargetType.RentalWantedPost => await _wantedPosts.GetByIdAsync(targetId, cancellationToken) is not null,
            ReportTargetType.RoommateInvitation => await _roommateInvitations.GetByIdAsync(targetId, cancellationToken) is not null,
            _ => false,
        };
    }
}
