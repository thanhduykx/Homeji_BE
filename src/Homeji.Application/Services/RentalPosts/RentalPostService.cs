using FluentValidation;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.RentalPosts;
using Homeji.Application.IRepositories.RentalPosts;
using Homeji.Application.IRepositories.Subscriptions;
using Homeji.Application.IServices.RentalPosts;
using Homeji.Application.Mappers.RentalPosts;
using Homeji.Application.Services.Common;
using Homeji.Application.Services.Moderation;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;

namespace Homeji.Application.Services.RentalPosts;

public sealed class RentalPostService : IRentalPostService
{
    private readonly UserContext _userContext;
    private readonly IRentalPostRepository _posts;
    private readonly IUserSubscriptionRepository _subscriptions;
    private readonly IValidator<UpdateRentalPostDto> _updateValidator;
    private readonly IValidator<AddRentalPostMediaDto> _mediaValidator;
    private readonly ContentModerationService _moderation;
    private readonly TimeProvider _timeProvider;

    public RentalPostService(
        UserContext userContext,
        IRentalPostRepository posts,
        IUserSubscriptionRepository subscriptions,
        IValidator<UpdateRentalPostDto> updateValidator,
        IValidator<AddRentalPostMediaDto> mediaValidator,
        ContentModerationService moderation,
        TimeProvider timeProvider)
    {
        _userContext = userContext;
        _posts = posts;
        _subscriptions = subscriptions;
        _updateValidator = updateValidator;
        _mediaValidator = mediaValidator;
        _moderation = moderation;
        _timeProvider = timeProvider;
    }

    public async Task<RentalPostDto> CreateDraftAsync(CreateRentalPostDraftDto request, CancellationToken cancellationToken = default)
    {
        var userId = _userContext.GetRequiredUserId();
        var post = RentalPost.CreateDraft(userId, request.Type, _timeProvider.GetUtcNow());
        await _posts.AddAsync(post, cancellationToken);
        await _posts.SaveChangesAsync(cancellationToken);
        return RentalPostMapper.ToDto(post);
    }

    public async Task<RentalPostDto> UpdateAsync(Guid postId, UpdateRentalPostDto request, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(_updateValidator, request, cancellationToken);
        var post = await GetOwnedPostAsync(postId, cancellationToken);
        post.UpdateDetails(
            request.Type,
            request.Title!,
            request.Description!,
            request.Price,
            request.Deposit,
            request.Area,
            request.Address!,
            request.Latitude,
            request.Longitude,
            request.Amenities,
            _timeProvider.GetUtcNow());
        await _posts.SaveChangesAsync(cancellationToken);
        return RentalPostMapper.ToDto(post);
    }

    public async Task<RentalPostDto> AddMediaAsync(Guid postId, AddRentalPostMediaDto request, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(_mediaValidator, request, cancellationToken);
        var userId = _userContext.GetRequiredUserId();
        var post = await GetOwnedPostAsync(postId, cancellationToken);

        var expectedPrefix = $"rental-posts/{userId:D}/{postId:D}/";
        if (!request.Path!.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["path"] = [$"Storage path must start with '{expectedPrefix}'."],
            });
        }

        post.AddMedia(
            request.MediaType,
            request.Bucket!,
            request.Path!,
            request.IsThumbnail,
            request.SortOrder,
            _timeProvider.GetUtcNow());
        await _posts.SaveChangesAsync(cancellationToken);
        return RentalPostMapper.ToDto(post);
    }

    public async Task DeleteMediaAsync(Guid postId, Guid mediaId, CancellationToken cancellationToken = default)
    {
        var post = await GetOwnedPostAsync(postId, cancellationToken);
        post.RemoveMedia(mediaId, _timeProvider.GetUtcNow());
        await _posts.SaveChangesAsync(cancellationToken);
    }

    public async Task<RentalPostDto> SubmitAsync(Guid postId, CancellationToken cancellationToken = default)
    {
        var post = await GetOwnedPostAsync(postId, cancellationToken);
        var violations = await _moderation.ValidateAsync(post.Description, cancellationToken);
        if (violations.Count > 0)
        {
            throw new RequestValidationException(new Dictionary<string, string[]> { ["description"] = violations.ToArray() });
        }

        post.Submit(_timeProvider.GetUtcNow());
        await _posts.SaveChangesAsync(cancellationToken);
        return RentalPostMapper.ToDto(post);
    }

    public async Task ArchiveAsync(Guid postId, CancellationToken cancellationToken = default)
    {
        var post = await GetOwnedPostAsync(postId, cancellationToken);
        post.Archive(_timeProvider.GetUtcNow());
        await _posts.SaveChangesAsync(cancellationToken);
    }

    public async Task<RentalPostDto> GetDetailAsync(Guid postId, CancellationToken cancellationToken = default)
    {
        var post = await _posts.GetByIdWithMediaAsync(postId, cancellationToken)
            ?? throw new NotFoundException(nameof(RentalPost), postId);

        var currentUserId = _userContext.TryGetUserId();
        var isOwner = currentUserId is { } userId && post.OwnerId == userId;
        if (post.Status != RentalPostStatus.Active && !isOwner)
        {
            throw new NotFoundException(nameof(RentalPost), postId);
        }

        post.IncrementViewCount();
        await _posts.SaveChangesAsync(cancellationToken);

        var dto = RentalPostMapper.ToDto(post);
        // Guests: hide internal moderation notes (Active posts usually null anyway).
        if (currentUserId is null)
        {
            return dto with { ModerationReason = null };
        }

        return dto;
    }

    public async Task<IReadOnlyList<RentalPostSummaryDto>> SearchAsync(
        RentalPostSearchDto request,
        CancellationToken cancellationToken = default)
    {
        var search = request;
        if (_userContext.TryGetUserId() is null)
        {
            search = request with
            {
                Page = 1,
                PageSize = Math.Min(Math.Max(1, request.PageSize), 3),
            };
        }

        var posts = await _posts.SearchActiveAsync(search, cancellationToken);
        var now = _timeProvider.GetUtcNow();
        var premiumByUserId = await _subscriptions.GetActivePremiumByUserIdsAsync(
            posts.Select(post => post.OwnerId).ToArray(),
            now,
            cancellationToken);

        return posts
            .Select(post =>
            {
                var isPremium = premiumByUserId.ContainsKey(post.OwnerId);
                var boostScore = CalculateBoostScore(post, isPremium, now);
                return RentalPostMapper.ToSummaryDto(post, isPremium, boostScore);
            })
            .OrderByDescending(post => post.IsOwnerPremium)
            .ThenByDescending(post => post.BoostScore)
            .ThenByDescending(post => post.SaveCount)
            .ThenByDescending(post => post.ViewCount)
            .ToArray();
    }

    private static decimal CalculateBoostScore(RentalPost post, bool isPremium, DateTimeOffset now)
    {
        var recencyDays = Math.Max(0, (now - post.UpdatedAt).TotalDays);
        var recencyScore = Math.Max(0, 30 - (decimal)recencyDays);
        var engagementScore = (post.SaveCount * 5) + Math.Min(post.ViewCount, 500) / 10m;
        var premiumScore = isPremium ? 100 : 0;

        return Math.Round(premiumScore + engagementScore + recencyScore, 2);
    }

    private async Task<RentalPost> GetOwnedPostAsync(Guid postId, CancellationToken cancellationToken)
    {
        var userId = _userContext.GetRequiredUserId();
        var post = await _posts.GetByIdWithMediaAsync(postId, cancellationToken)
            ?? throw new NotFoundException(nameof(RentalPost), postId);
        UserContext.EnsureOwner(userId, post.OwnerId);
        return post;
    }

    private static async Task ValidateAsync<T>(IValidator<T> validator, T request, CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(request, cancellationToken);
        if (!result.IsValid)
        {
            throw new RequestValidationException(result.Errors
                .GroupBy(error => error.PropertyName, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.ErrorMessage).Distinct().ToArray(),
                    StringComparer.Ordinal));
        }
    }
}
