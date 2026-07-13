using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Reviews;
using Homeji.Application.IRepositories.Profiles;
using Homeji.Application.IRepositories.RentalPosts;
using Homeji.Application.IRepositories.Reviews;
using Homeji.Application.IServices.Reviews;
using Homeji.Application.Services.Common;
using Homeji.Application.Services.Moderation;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;

namespace Homeji.Application.Services.Reviews;

public sealed class RentalReviewService : IRentalReviewService
{
    private readonly UserContext _userContext;
    private readonly IRentalPostRepository _rentalPosts;
    private readonly IRentalReviewRepository _reviews;
    private readonly IUserProfileRepository _profiles;
    private readonly ContentModerationService _moderation;
    private readonly TimeProvider _timeProvider;

    public RentalReviewService(
        UserContext userContext,
        IRentalPostRepository rentalPosts,
        IRentalReviewRepository reviews,
        IUserProfileRepository profiles,
        ContentModerationService moderation,
        TimeProvider timeProvider)
    {
        _userContext = userContext;
        _rentalPosts = rentalPosts;
        _reviews = reviews;
        _profiles = profiles;
        _moderation = moderation;
        _timeProvider = timeProvider;
    }

    public async Task<RentalReviewCollectionDto> GetByPostAsync(
        Guid rentalPostId,
        CancellationToken cancellationToken = default)
    {
        _ = await GetActivePostAsync(rentalPostId, cancellationToken);
        var reviews = await _reviews.GetByPostAsync(rentalPostId, cancellationToken);
        var profiles = await _profiles.GetByIdsAsync(
            reviews.Select(review => review.ReviewerId).Distinct().ToArray(),
            cancellationToken);
        var profilesById = profiles.ToDictionary(profile => profile.Id);

        var items = reviews
            .Select(review => ToDto(review, profilesById.GetValueOrDefault(review.ReviewerId)))
            .ToArray();
        var average = items.Length == 0
            ? 0
            : Math.Round(items.Average(item => (decimal)item.Rating), 2);

        return new RentalReviewCollectionDto(rentalPostId, average, items.Length, items);
    }

    public async Task<RentalReviewDto> UpsertAsync(
        Guid rentalPostId,
        UpsertRentalReviewDto request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        var reviewer = await _userContext.GetRequiredProfileAsync(cancellationToken);
        var post = await GetActivePostAsync(rentalPostId, cancellationToken);
        if (post.OwnerId == reviewer.Id)
        {
            throw new ForbiddenAccessException("Rental post owners cannot review their own rental post.");
        }

        var violations = await _moderation.ValidateAsync(request.Comment ?? string.Empty, cancellationToken);
        if (violations.Count > 0)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["comment"] = violations.ToArray(),
            });
        }

        var now = _timeProvider.GetUtcNow();
        var review = await _reviews.GetByPostAndReviewerAsync(rentalPostId, reviewer.Id, cancellationToken);
        if (review is null)
        {
            review = new RentalReview(rentalPostId, reviewer.Id, request.Rating, request.Comment, now);
            await _reviews.AddAsync(review, cancellationToken);
        }
        else
        {
            review.Update(request.Rating, request.Comment, now);
        }

        await _reviews.SaveChangesAsync(cancellationToken);
        return ToDto(review, reviewer);
    }

    public async Task DeleteMineAsync(Guid rentalPostId, CancellationToken cancellationToken = default)
    {
        var reviewerId = _userContext.GetRequiredUserId();
        var review = await _reviews.GetByPostAndReviewerAsync(rentalPostId, reviewerId, cancellationToken)
            ?? throw new NotFoundException(nameof(RentalReview), rentalPostId);
        _reviews.Remove(review);
        await _reviews.SaveChangesAsync(cancellationToken);
    }

    private async Task<RentalPost> GetActivePostAsync(Guid rentalPostId, CancellationToken cancellationToken)
    {
        var post = await _rentalPosts.GetByIdAsync(rentalPostId, cancellationToken)
            ?? throw new NotFoundException(nameof(RentalPost), rentalPostId);
        return post.Status == RentalPostStatus.Active
            ? post
            : throw new NotFoundException(nameof(RentalPost), rentalPostId);
    }

    private static void ValidateRequest(UpsertRentalReviewDto request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.Rating is < 1 or > 5)
        {
            errors["rating"] = ["Rating must be between 1 and 5."];
        }

        if (request.Comment?.Trim().Length > RentalReview.MaxCommentLength)
        {
            errors["comment"] = [$"Comment must not exceed {RentalReview.MaxCommentLength} characters."];
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors);
        }
    }

    private static RentalReviewDto ToDto(RentalReview review, UserProfile? reviewer)
    {
        return new RentalReviewDto(
            review.Id,
            review.RentalPostId,
            review.ReviewerId,
            reviewer?.DisplayName ?? "Homeji user",
            reviewer?.AvatarPath,
            review.Rating,
            review.Comment,
            review.CreatedAt,
            review.UpdatedAt);
    }
}
