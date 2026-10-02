using Homeji.Domain.Enums;

namespace Homeji.Application.IRepositories.Admin;

public interface IAdminAnalyticsRepository
{
    Task<AdminAnalyticsSource> GetSnapshotAsync(
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken cancellationToken = default);
}

public sealed record AdminAnalyticsSource(
    int TotalUsers,
    IReadOnlyList<AdminAnalyticsRentalRow> Rentals,
    IReadOnlyList<DateTimeOffset> NewUserDates,
    IReadOnlyList<AdminAnalyticsActivityRow> Activities,
    IReadOnlyList<AdminAnalyticsSavedRow> Saves,
    IReadOnlyList<AdminAnalyticsAppointmentRow> ViewingRequests);

public sealed record AdminAnalyticsRentalRow(
    Guid Id,
    RentalPostStatus Status,
    string Address,
    decimal Latitude,
    decimal Longitude,
    decimal Price,
    decimal Area,
    int ViewCount,
    int SaveCount,
    DateTimeOffset CreatedAt);

public sealed record AdminAnalyticsActivityRow(
    UserActivityType Type,
    Guid? RelatedEntityId,
    DateTimeOffset OccurredAt);

public sealed record AdminAnalyticsSavedRow(Guid RentalPostId, DateTimeOffset CreatedAt);

public sealed record AdminAnalyticsAppointmentRow(
    Guid RentalPostId,
    ViewingAppointmentStatus Status,
    DateTimeOffset CreatedAt);
