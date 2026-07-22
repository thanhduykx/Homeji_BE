using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.WantedPosts;
using Homeji.Application.IRepositories.Profiles;
using Homeji.Application.IRepositories.WantedPosts;
using Homeji.Application.IServices.WantedPosts;
using Homeji.Application.Services.Common;
using Homeji.Application.Services.Moderation;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;

namespace Homeji.Application.Services.WantedPosts;

public sealed class RentalWantedPostService : IRentalWantedPostService
{
    private readonly UserContext _userContext;
    private readonly IRentalWantedPostRepository _posts;
    private readonly IUserProfileRepository _profiles;
    private readonly ContentModerationService _moderation;
    private readonly TimeProvider _timeProvider;

    public RentalWantedPostService(
        UserContext userContext,
        IRentalWantedPostRepository posts,
        IUserProfileRepository profiles,
        ContentModerationService moderation,
        TimeProvider timeProvider)
    {
        _userContext = userContext;
        _posts = posts;
        _profiles = profiles;
        _moderation = moderation;
        _timeProvider = timeProvider;
    }

    public async Task<RentalWantedPostDto> CreateAsync(
        UpsertRentalWantedPostDto request,
        CancellationToken cancellationToken = default)
    {
        var renter = await _userContext.GetRequiredProfileAsync(cancellationToken);
        UserContext.EnsureRenter(renter);
        await ValidateAsync(request, cancellationToken);
        var now = _timeProvider.GetUtcNow();
        var post = new RentalWantedPost(
            renter.Id,
            request.Title!,
            request.Description!,
            request.PreferredArea!,
            request.MaxBudget,
            request.OccupantCount,
            request.AmenityCodes,
            request.DesiredMoveInDate,
            now);
        await _posts.AddAsync(post, cancellationToken);
        await _posts.SaveChangesAsync(cancellationToken);
        return ToDto(post, renter);
    }

    public async Task<RentalWantedPostDto> UpdateAsync(
        Guid id,
        UpsertRentalWantedPostDto request,
        CancellationToken cancellationToken = default)
    {
        var post = await GetOwnedAsync(id, cancellationToken);
        await ValidateAsync(request, cancellationToken);
        post.Update(
            request.Title!,
            request.Description!,
            request.PreferredArea!,
            request.MaxBudget,
            request.OccupantCount,
            request.AmenityCodes,
            request.DesiredMoveInDate,
            _timeProvider.GetUtcNow());
        await _posts.SaveChangesAsync(cancellationToken);
        var profile = await _profiles.GetByIdAsync(post.RequesterId, cancellationToken);
        return ToDto(post, profile);
    }

    public async Task<RentalWantedPostDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var post = await _posts.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(RentalWantedPost), id);
        if (post.Status != WantedPostStatus.Active && _userContext.TryGetUserId() != post.RequesterId)
        {
            throw new NotFoundException(nameof(RentalWantedPost), id);
        }

        var profile = await _profiles.GetByIdAsync(post.RequesterId, cancellationToken);
        return ToDto(post, profile);
    }

    public async Task<IReadOnlyList<RentalWantedPostDto>> SearchAsync(
        string? area,
        decimal? maxBudget,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = Math.Max(1, page);
        var normalizedSize = Math.Clamp(pageSize, 1, 100);
        var posts = await _posts.SearchActiveAsync(
            area,
            maxBudget,
            (normalizedPage - 1) * normalizedSize,
            normalizedSize,
            cancellationToken);
        var profiles = await _profiles.GetByIdsAsync(posts.Select(post => post.RequesterId).Distinct().ToArray(), cancellationToken);
        var profileMap = profiles.ToDictionary(profile => profile.Id);
        return posts.Select(post => ToDto(post, profileMap.GetValueOrDefault(post.RequesterId))).ToArray();
    }

    public async Task CloseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var post = await GetOwnedAsync(id, cancellationToken);
        post.Close(_timeProvider.GetUtcNow());
        await _posts.SaveChangesAsync(cancellationToken);
    }

    private async Task<RentalWantedPost> GetOwnedAsync(Guid id, CancellationToken cancellationToken)
    {
        var renter = await _userContext.GetRequiredProfileAsync(cancellationToken);
        UserContext.EnsureRenter(renter);
        var post = await _posts.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(RentalWantedPost), id);
        UserContext.EnsureOwner(renter.Id, post.RequesterId);
        return post;
    }

    private async Task ValidateAsync(UpsertRentalWantedPostDto request, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Title)) errors["title"] = ["Tiêu đề là bắt buộc."];
        if (string.IsNullOrWhiteSpace(request.Description)) errors["description"] = ["Mô tả là bắt buộc."];
        if (string.IsNullOrWhiteSpace(request.PreferredArea)) errors["preferredArea"] = ["Khu vực mong muốn là bắt buộc."];
        if (request.MaxBudget <= 0) errors["maxBudget"] = ["Ngân sách tối đa phải lớn hơn 0."];
        if (request.OccupantCount <= 0) errors["occupantCount"] = ["Số người ở phải lớn hơn 0."];
        if (request.DesiredMoveInDate < DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime))
        {
            errors["desiredMoveInDate"] = ["Ngày muốn chuyển vào không được ở quá khứ."];
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors);
        }

        var violations = await _moderation.ValidateAsync($"{request.Title} {request.Description}", cancellationToken);
        if (violations.Count > 0)
        {
            throw new RequestValidationException(new Dictionary<string, string[]> { ["description"] = violations.ToArray() });
        }
    }

    private static RentalWantedPostDto ToDto(RentalWantedPost post, UserProfile? profile)
    {
        return new RentalWantedPostDto(
            post.Id,
            post.RequesterId,
            profile?.DisplayName ?? "Homeji user",
            profile?.AvatarPath,
            post.Status,
            post.Title,
            post.Description,
            post.PreferredArea,
            post.MaxBudget,
            post.OccupantCount,
            post.AmenityCodes,
            post.DesiredMoveInDate,
            post.CreatedAt,
            post.UpdatedAt);
    }
}
