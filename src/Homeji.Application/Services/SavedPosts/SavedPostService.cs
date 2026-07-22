using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.RentalPosts;
using Homeji.Application.DTOs.SavedPosts;
using Homeji.Application.IRepositories.Profiles;
using Homeji.Application.IRepositories.RentalPosts;
using Homeji.Application.IRepositories.SavedPosts;
using Homeji.Application.IServices.SavedPosts;
using Homeji.Application.Mappers.RentalPosts;
using Homeji.Application.Services.Common;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;

namespace Homeji.Application.Services.SavedPosts;

public sealed class SavedPostService : ISavedPostService
{
    private readonly UserContext _userContext;
    private readonly ISavedPostRepository _savedPosts;
    private readonly IRentalPostRepository _posts;
    private readonly IUserProfileRepository _profiles;
    private readonly TimeProvider _timeProvider;

    public SavedPostService(
        UserContext userContext,
        ISavedPostRepository savedPosts,
        IRentalPostRepository posts,
        IUserProfileRepository profiles,
        TimeProvider timeProvider)
    {
        _userContext = userContext;
        _savedPosts = savedPosts;
        _posts = posts;
        _profiles = profiles;
        _timeProvider = timeProvider;
    }

    public async Task SaveAsync(Guid postId, CancellationToken cancellationToken = default)
    {
        var renter = await GetRequiredRenterAsync(cancellationToken);
        var userId = renter.Id;
        var post = await GetActivePostAsync(postId, cancellationToken);
        if (await _savedPosts.ExistsAsync(userId, postId, cancellationToken))
        {
            return;
        }

        await _savedPosts.AddAsync(new SavedPost(userId, postId, _timeProvider.GetUtcNow()), cancellationToken);
        post.ApplySaveDelta(1);
        await _savedPosts.SaveChangesAsync(cancellationToken);
    }

    public async Task UnsaveAsync(Guid postId, CancellationToken cancellationToken = default)
    {
        var renter = await GetRequiredRenterAsync(cancellationToken);
        var userId = renter.Id;
        var post = await GetActivePostAsync(postId, cancellationToken);
        if (await _savedPosts.RemoveAsync(userId, postId, cancellationToken))
        {
            post.ApplySaveDelta(-1);
            await _savedPosts.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<RentalPostSummaryDto>> GetMineAsync(CancellationToken cancellationToken = default)
    {
        var renter = await GetRequiredRenterAsync(cancellationToken);
        var userId = renter.Id;
        var saved = await _savedPosts.GetByUserAsync(userId, cancellationToken);
        var posts = new List<RentalPostSummaryDto>();
        foreach (var item in saved)
        {
            var post = await _posts.GetByIdWithMediaAsync(item.RentalPostId, cancellationToken);
            if (post is { Status: RentalPostStatus.Active })
            {
                posts.Add(RentalPostMapper.ToSummaryDto(post));
            }
        }

        return posts;
    }

    public async Task<IReadOnlyList<RoommateCandidateDto>> GetRoommateCandidatesAsync(Guid postId, CancellationToken cancellationToken = default)
    {
        var current = await _userContext.GetRequiredProfileAsync(cancellationToken);
        UserContext.EnsureRenter(current);
        if (!await _savedPosts.ExistsAsync(current.Id, postId, cancellationToken))
        {
            throw new ForbiddenAccessException("Hãy lưu tin đăng này trước khi xem ứng viên ở ghép.");
        }

        _ = await GetActivePostAsync(postId, cancellationToken);
        var saved = await _savedPosts.GetByPostAsync(postId, cancellationToken);
        var candidateIds = saved
            .Select(item => item.UserId)
            .Where(id => id != current.Id)
            .Distinct()
            .ToArray();

        var candidates = await _profiles.GetByIdsAsync(candidateIds, cancellationToken);
        return candidates
            .Select(profile => new RoommateCandidateDto(
                profile.Id,
                profile.DisplayName,
                profile.School,
                profile.PreferredArea,
                CalculateMatchScore(current, profile)))
            .OrderByDescending(candidate => candidate.MatchScore)
            .ToArray();
    }

    private async Task<UserProfile> GetRequiredRenterAsync(CancellationToken cancellationToken)
    {
        var profile = await _userContext.GetRequiredProfileAsync(cancellationToken);
        UserContext.EnsureRenter(profile);
        return profile;
    }

    private async Task<RentalPost> GetActivePostAsync(Guid postId, CancellationToken cancellationToken)
    {
        var post = await _posts.GetByIdWithMediaAsync(postId, cancellationToken)
            ?? throw new NotFoundException(nameof(RentalPost), postId);
        if (post.Status != RentalPostStatus.Active)
        {
            throw new NotFoundException(nameof(RentalPost), postId);
        }

        return post;
    }

    private static int CalculateMatchScore(UserProfile source, UserProfile target)
    {
        var total = 0;
        var matched = 0;

        void Compare<T>(T left, T right, T unknown)
            where T : struct, Enum
        {
            if (!EqualityComparer<T>.Default.Equals(left, unknown)
                && !EqualityComparer<T>.Default.Equals(right, unknown))
            {
                total++;
                if (EqualityComparer<T>.Default.Equals(left, right))
                {
                    matched++;
                }
            }
        }

        Compare(source.SleepHabit, target.SleepHabit, SleepHabit.Unknown);
        Compare(source.PetPreference, target.PetPreference, PetPreference.Unknown);
        Compare(source.SmokingPreference, target.SmokingPreference, SmokingPreference.Unknown);

        return total == 0 ? 0 : (int)Math.Round(matched * 100m / total);
    }
}
