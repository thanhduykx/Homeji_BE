using Homeji.Application.Abstractions.Authentication;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.IRepositories.Admin;
using Homeji.Application.IRepositories.Profiles;
using Homeji.Application.Services.Admin;
using Homeji.Application.Services.Common;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;

namespace Homeji.Application.UnitTests.Admin;

public sealed class AdminAnalyticsServiceTests
{
    [Fact]
    public async Task NonAdmin_IsRejectedBeforeAnalyticsDataIsRead()
    {
        var profile = UserProfile.Create(Guid.NewGuid(), "Renter", DateTimeOffset.UtcNow);
        var repository = new SnapshotRepository();
        var service = CreateService(profile, repository);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.GetProductAnalyticsAsync(30));
        Assert.False(repository.WasRead);
    }

    [Theory]
    [InlineData(6)]
    [InlineData(91)]
    public async Task InvalidPeriod_IsRejectedBeforeAnalyticsDataIsRead(int days)
    {
        var profile = UserProfile.Create(Guid.NewGuid(), "Admin", DateTimeOffset.UtcNow);
        profile.SetRole(UserRole.Admin, DateTimeOffset.UtcNow);
        var repository = new SnapshotRepository();

        var error = await Assert.ThrowsAsync<RequestValidationException>(() =>
            CreateService(profile, repository).GetProductAnalyticsAsync(days));

        Assert.Contains("days", error.Errors.Keys);
        Assert.False(repository.WasRead);
    }

    [Fact]
    public async Task Admin_ReadsRequestedPeriodStartingAtVietnamMidnight()
    {
        var profile = UserProfile.Create(Guid.NewGuid(), "Admin", DateTimeOffset.UtcNow);
        profile.SetRole(UserRole.Admin, DateTimeOffset.UtcNow);
        var repository = new SnapshotRepository();

        var result = await CreateService(profile, repository).GetProductAnalyticsAsync(7);

        Assert.True(repository.WasRead);
        Assert.Equal(new DateTimeOffset(2026, 9, 26, 17, 0, 0, TimeSpan.Zero), repository.From);
        Assert.Equal(7, result.Trend.Count);
        Assert.Empty(result.Areas);
    }

    private static AdminAnalyticsService CreateService(UserProfile profile, SnapshotRepository repository) =>
        new(new UserContext(new CurrentUser(profile.Id), new ProfileRepository(profile)), repository, new FixedClock());

    private sealed record CurrentUser(Guid? UserId) : ICurrentUser;

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 2, 18, 0, 0, TimeSpan.Zero);
    }

    private sealed class SnapshotRepository : IAdminAnalyticsRepository
    {
        public bool WasRead { get; private set; }
        public DateTimeOffset From { get; private set; }

        public Task<AdminAnalyticsSource> GetSnapshotAsync(DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken = default)
        {
            WasRead = true;
            From = from;
            return Task.FromResult(new AdminAnalyticsSource(0, [], [], [], [], []));
        }
    }

    private sealed class ProfileRepository(UserProfile profile) : IUserProfileRepository
    {
        public Task<UserProfile?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<UserProfile?>(profile);
        public Task<UserProfile> UpsertAsync(UserProfile value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<UserProfile> SaveAsync(UserProfile value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<UserProfile>> GetByIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> GetAllUserIdsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<UserProfile>> GetMatchingRentersAsync(string address, decimal price, Guid excludedUserId, int take, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
