using Homeji.Application.IRepositories.Admin;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Infrastructure.Repositories;

public sealed class AdminAnalyticsRepository : IAdminAnalyticsRepository
{
    private readonly ApplicationDbContext _dbContext;

    public AdminAnalyticsRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminAnalyticsSource> GetSnapshotAsync(
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken cancellationToken = default)
    {
        var totalUsers = await _dbContext.UserProfiles.AsNoTracking().CountAsync(cancellationToken);
        var rentals = await _dbContext.RentalPosts
            .AsNoTracking()
            .Where(post => post.Status == RentalPostStatus.Active || post.CreatedAt >= from)
            .Where(post => post.CreatedAt <= until)
            .Select(post => new AdminAnalyticsRentalRow(
                post.Id,
                post.Status,
                post.Address,
                post.Latitude,
                post.Longitude,
                post.Price,
                post.Area,
                post.ViewCount,
                post.SaveCount,
                post.CreatedAt))
            .ToListAsync(cancellationToken);
        var newUsers = await _dbContext.UserProfiles
            .AsNoTracking()
            .Where(profile => profile.CreatedAt >= from && profile.CreatedAt <= until)
            .Select(profile => profile.CreatedAt)
            .ToListAsync(cancellationToken);
        var activities = await _dbContext.UserActivities
            .AsNoTracking()
            .Where(activity => activity.OccurredAt >= from && activity.OccurredAt <= until)
            .Where(activity => activity.Type == UserActivityType.RentalSearch
                || activity.Type == UserActivityType.ViewedRentalPost)
            .Select(activity => new AdminAnalyticsActivityRow(
                activity.Type,
                activity.RelatedEntityId,
                activity.OccurredAt))
            .ToListAsync(cancellationToken);
        var saves = await _dbContext.SavedPosts
            .AsNoTracking()
            .Where(saved => saved.CreatedAt >= from && saved.CreatedAt <= until)
            .Select(saved => new AdminAnalyticsSavedRow(saved.RentalPostId, saved.CreatedAt))
            .ToListAsync(cancellationToken);
        var viewingRequests = await _dbContext.ViewingAppointments
            .AsNoTracking()
            .Where(appointment => appointment.CreatedAt >= from && appointment.CreatedAt <= until)
            .Select(appointment => new AdminAnalyticsAppointmentRow(
                appointment.RentalPostId,
                appointment.Status,
                appointment.CreatedAt))
            .ToListAsync(cancellationToken);

        return new AdminAnalyticsSource(
            totalUsers,
            rentals,
            newUsers,
            activities,
            saves,
            viewingRequests);
    }
}
