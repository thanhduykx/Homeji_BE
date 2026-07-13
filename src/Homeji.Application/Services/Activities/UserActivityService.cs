using Homeji.Application.DTOs.Activities;
using Homeji.Application.IRepositories.Activities;
using Homeji.Application.IServices.Activities;
using Homeji.Application.Services.Common;
using Homeji.Domain.Entities;

namespace Homeji.Application.Services.Activities;

public sealed class UserActivityService : IUserActivityService
{
    private readonly UserContext _userContext;
    private readonly IUserActivityRepository _activities;
    private readonly TimeProvider _timeProvider;

    public UserActivityService(UserContext userContext, IUserActivityRepository activities, TimeProvider timeProvider)
    {
        _userContext = userContext;
        _activities = activities;
        _timeProvider = timeProvider;
    }

    public async Task RecordAsync(
        Guid userId,
        string action,
        string resourcePath,
        string httpMethod,
        int responseStatusCode,
        CancellationToken cancellationToken = default)
    {
        await _activities.AddAsync(new UserActivity(
            userId,
            action,
            resourcePath,
            httpMethod,
            responseStatusCode,
            _timeProvider.GetUtcNow()), cancellationToken);
        await _activities.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserActivityDto>> GetMineAsync(
        int take,
        CancellationToken cancellationToken = default)
    {
        var userId = _userContext.GetRequiredUserId();
        var activities = await _activities.GetForUserAsync(userId, Math.Clamp(take, 1, 100), cancellationToken);
        return activities.Select(activity => new UserActivityDto(
            activity.Id,
            activity.Action,
            activity.ResourcePath,
            activity.HttpMethod,
            activity.ResponseStatusCode,
            activity.OccurredAt)).ToArray();
    }
}
