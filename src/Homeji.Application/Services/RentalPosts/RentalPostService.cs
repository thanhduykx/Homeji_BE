using FluentValidation;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.RentalPosts;
using Homeji.Application.IRepositories.RentalPosts;
using Homeji.Application.IRepositories.Subscriptions;
using Homeji.Application.IRepositories.Reviews;
using Homeji.Application.IRepositories.Profiles;
using Homeji.Application.IRepositories.Conversations;
using Homeji.Application.IRepositories.Appointments;
using Homeji.Application.IServices.RentalPosts;
using Homeji.Application.IServices.Activities;
using Homeji.Application.Mappers.RentalPosts;
using Homeji.Application.Services.Common;
using Homeji.Application.Services.Moderation;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using System.Text.Json;

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
    private readonly IUserActivityService _activities;
    private readonly IRentalReviewRepository _reviews;
    private readonly IUserProfileRepository _profiles;
    private readonly IPostConversationRepository _conversations;
    private readonly IViewingAppointmentRepository _appointments;

    public RentalPostService(
        UserContext userContext,
        IRentalPostRepository posts,
        IUserSubscriptionRepository subscriptions,
        IValidator<UpdateRentalPostDto> updateValidator,
        IValidator<AddRentalPostMediaDto> mediaValidator,
        ContentModerationService moderation,
        IUserActivityService activities,
        IRentalReviewRepository reviews,
        IUserProfileRepository profiles,
        IPostConversationRepository conversations,
        IViewingAppointmentRepository appointments,
        TimeProvider timeProvider)
    {
        _userContext = userContext;
        _posts = posts;
        _subscriptions = subscriptions;
        _updateValidator = updateValidator;
        _mediaValidator = mediaValidator;
        _moderation = moderation;
        _activities = activities;
        _reviews = reviews;
        _profiles = profiles;
        _conversations = conversations;
        _appointments = appointments;
        _timeProvider = timeProvider;
    }

    public async Task<RentalPostDto> CreateDraftAsync(CreateRentalPostDraftDto request, CancellationToken cancellationToken = default)
    {
        var landlord = await _userContext.GetRequiredProfileAsync(cancellationToken);
        UserContext.EnsureLandlord(landlord);
        var post = RentalPost.CreateDraft(landlord.Id, request.Type, _timeProvider.GetUtcNow());
        await _posts.AddAsync(post, cancellationToken);
        await _posts.SaveChangesAsync(cancellationToken);
        return RentalPostMapper.ToDto(post);
    }

    public async Task<RentalPostDto> UpdateAsync(Guid postId, UpdateRentalPostDto request, CancellationToken cancellationToken = default)
    {
        var post = await GetOwnedPostAsync(postId, cancellationToken);
        await ValidateAsync(_updateValidator, request, cancellationToken);
        var violations = await _moderation.ValidateAsync(request.Description ?? string.Empty, cancellationToken);
        if (violations.Count > 0)
        {
            throw new RequestValidationException(new Dictionary<string, string[]> { ["description"] = violations.ToArray() });
        }

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
            _timeProvider.GetUtcNow(),
            request.ElectricityPrice,
            request.WaterPrice,
            request.InternetPrice,
            request.MaxOccupants,
            request.AvailableSlots,
            request.HouseRules,
            request.AvailableFrom);
        await _posts.SaveChangesAsync(cancellationToken);
        return RentalPostMapper.ToDto(post);
    }

    public async Task<RentalPostDto> AddMediaAsync(Guid postId, AddRentalPostMediaDto request, CancellationToken cancellationToken = default)
    {
        var post = await GetOwnedPostAsync(postId, cancellationToken);
        await ValidateAsync(_mediaValidator, request, cancellationToken);

        var expectedPrefix = $"rental-posts/{post.OwnerId:D}/{postId:D}/";
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

    public async Task MarkRentedAsync(Guid postId, CancellationToken cancellationToken = default)
    {
        var post = await GetOwnedPostAsync(postId, cancellationToken);
        post.MarkRented(_timeProvider.GetUtcNow());
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

        if (currentUserId is { } viewerId && !isOwner)
        {
            await _activities.RecordAsync(
                viewerId,
                "Viewed rental post",
                $"/api/rental-posts/{post.Id:D}",
                "GET",
                200,
                UserActivityType.ViewedRentalPost,
                post.Id,
                post.Title,
                cancellationToken);
        }

        var owner = await _profiles.GetByIdAsync(post.OwnerId, cancellationToken);
        var premiumByOwner = await _subscriptions.GetActivePremiumByUserIdsAsync(
            [post.OwnerId],
            _timeProvider.GetUtcNow(),
            cancellationToken);
        var ownerIsPremium = premiumByOwner.ContainsKey(post.OwnerId);
        var dto = RentalPostMapper.ToDto(post) with
        {
            OwnerDisplayName = owner?.DisplayName,
            OwnerPhone = owner?.Phone,
            OwnerAvatarPath = owner?.AvatarPath,
            IsOwnerVerified = owner?.LandlordVerificationStatus == LandlordVerificationStatus.Verified,
            IsOwnerPremium = ownerIsPremium,
            OwnerBadge = ownerIsPremium ? "Premium" : null,
        };
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

        // Premium boost is best-effort: search must still work if subscriptions
        // schema/migration is missing or the lookup fails.
        IReadOnlyDictionary<Guid, UserSubscription> premiumByUserId;
        try
        {
            premiumByUserId = await _subscriptions.GetActivePremiumByUserIdsAsync(
                posts.Select(post => post.OwnerId).ToArray(),
                now,
                cancellationToken);
        }
        catch (Exception)
        {
            premiumByUserId = new Dictionary<Guid, UserSubscription>();
        }

        var result = posts
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

        if (_userContext.TryGetUserId() is { } userId && HasSearchCriteria(search))
        {
            await _activities.RecordAsync(
                userId,
                "Searched rental posts",
                "/api/rental-posts",
                "GET",
                200,
                UserActivityType.RentalSearch,
                details: JsonSerializer.Serialize(search),
                cancellationToken: cancellationToken);
        }

        return result;
    }

    public async Task<RentalPostOwnerStatsDto> GetOwnerStatsAsync(
        CancellationToken cancellationToken = default)
    {
        var landlord = await _userContext.GetRequiredProfileAsync(cancellationToken);
        UserContext.EnsureLandlord(landlord);
        var ownerId = landlord.Id;
        var posts = await _posts.GetByOwnerAsync(ownerId, cancellationToken);
        var postIds = posts.Select(post => post.Id).ToArray();
        var contactCounts = await _conversations.CountBySubjectsAsync(
            ConversationSubjectType.RentalPost,
            postIds,
            cancellationToken);
        var appointmentCounts = await _appointments.CountByPostIdsAsync(postIds, cancellationToken);
        var now = _timeProvider.GetUtcNow();
        var premium = await _subscriptions.GetActivePremiumAsync(ownerId, now, cancellationToken);
        var isPremium = premium is not null;
        var items = posts.Select(post => new RentalPostOwnerStatsItemDto(
            post.Id,
            post.Title,
            post.Type,
            post.Status,
            post.ViewCount,
            post.SaveCount,
            contactCounts.GetValueOrDefault(post.Id),
            appointmentCounts.GetValueOrDefault(post.Id),
            CalculateBoostScore(post, isPremium, now),
            post.UpdatedAt)).ToArray();

        return new RentalPostOwnerStatsDto(
            items.Length,
            items.Sum(item => item.ViewCount),
            items.Sum(item => item.SaveCount),
            items.Sum(item => item.ContactCount),
            items.Sum(item => item.AppointmentCount),
            isPremium,
            items);
    }

    public async Task<RentalPostComparisonDto> CompareAsync(
        CompareRentalPostsDto request,
        CancellationToken cancellationToken = default)
    {
        var ids = request.PostIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (ids.Length is < 2 or > 4)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["postIds"] = ["Select between 2 and 4 different rental posts to compare."],
            });
        }

        var posts = await _posts.GetByIdsWithMediaAsync(ids, cancellationToken);
        if (posts.Count != ids.Length || posts.Any(post => post.Status != RentalPostStatus.Active))
        {
            throw new NotFoundException(nameof(RentalPost), "comparison selection");
        }

        var reviews = await _reviews.GetByPostIdsAsync(ids, cancellationToken);
        var reviewMap = reviews.GroupBy(review => review.RentalPostId).ToDictionary(group => group.Key, group => group.ToArray());
        var postMap = posts.ToDictionary(post => post.Id);
        var items = ids.Select(id =>
        {
            var postReviews = reviewMap.GetValueOrDefault(id) ?? [];
            var average = postReviews.Length == 0
                ? 0
                : Math.Round(postReviews.Average(review => (decimal)review.Rating), 2);
            return new RentalPostComparisonItemDto(
                RentalPostMapper.ToDto(postMap[id]),
                average,
                postReviews.Length);
        }).ToArray();

        return new RentalPostComparisonDto(items);
    }

    private static decimal CalculateBoostScore(RentalPost post, bool isPremium, DateTimeOffset now)
    {
        var recencyDays = Math.Max(0, (now - post.UpdatedAt).TotalDays);
        var recencyScore = Math.Max(0, 30 - (decimal)recencyDays);
        var engagementScore = (post.SaveCount * 5) + Math.Min(post.ViewCount, 500) / 10m;
        var premiumScore = isPremium ? 100 : 0;

        return Math.Round(premiumScore + engagementScore + recencyScore, 2);
    }

    private static bool HasSearchCriteria(RentalPostSearchDto search)
    {
        return !string.IsNullOrWhiteSpace(search.Keyword)
            || search.MinPrice.HasValue
            || search.MaxPrice.HasValue
            || search.MinArea.HasValue
            || search.MaxArea.HasValue
            || search.MinLatitude.HasValue
            || search.MaxLatitude.HasValue
            || search.MinLongitude.HasValue
            || search.MaxLongitude.HasValue
            || search.Amenities.Count > 0
            || search.MaxDeposit.HasValue
            || search.MinAvailableSlots.HasValue
            || search.AvailableFromBefore.HasValue;
    }

    private async Task<RentalPost> GetOwnedPostAsync(Guid postId, CancellationToken cancellationToken)
    {
        var landlord = await _userContext.GetRequiredProfileAsync(cancellationToken);
        UserContext.EnsureLandlord(landlord);
        var post = await _posts.GetByIdWithMediaAsync(postId, cancellationToken)
            ?? throw new NotFoundException(nameof(RentalPost), postId);
        UserContext.EnsureOwner(landlord.Id, post.OwnerId);
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
