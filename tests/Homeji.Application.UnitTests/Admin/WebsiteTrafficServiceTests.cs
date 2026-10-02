using Homeji.Application.Abstractions.Authentication;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Admin;
using Homeji.Application.IRepositories.Admin;
using Homeji.Application.IRepositories.Profiles;
using Homeji.Application.Services.Admin;
using Homeji.Application.Services.Common;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;

namespace Homeji.Application.UnitTests.Admin;

public sealed class WebsiteTrafficServiceTests
{
    [Theory]
    [InlineData("/private?email=user@example.com")]
    [InlineData("admin")]
    [InlineData("")]
    public async Task InvalidPage_IsRejectedBeforeStorage(string page)
    {
        var repository = new TrafficRepository();
        var service = CreateService(null, repository);
        await Assert.ThrowsAsync<RequestValidationException>(() => service.RecordAsync(
            new(Guid.NewGuid(), Guid.NewGuid(), page), CancellationToken.None));
        Assert.Null(repository.Recorded);
    }

    [Fact]
    public async Task Guest_CanRecordCoarsePageWithServerTimestamp()
    {
        var repository = new TrafficRepository();
        var request = new RecordWebsitePageViewDto(Guid.NewGuid(), Guid.NewGuid(), "home");
        await CreateService(null, repository).RecordAsync(request, CancellationToken.None);
        Assert.NotNull(repository.Recorded);
        Assert.Equal(request.EventId, repository.Recorded.Id);
        Assert.Equal(request.SessionId, repository.Recorded.SessionId);
        Assert.Equal("home", repository.Recorded.Page);
        Assert.Equal(FixedClock.Now, repository.Recorded.OccurredAt);
    }

    [Fact]
    public async Task NonAdmin_CannotReadWebsiteTraffic()
    {
        var profile = UserProfile.Create(Guid.NewGuid(), "Renter", FixedClock.Now);
        var repository = new TrafficRepository();
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService(profile, repository).GetReportAsync(30, CancellationToken.None));
        Assert.False(repository.Read);
    }

    [Fact]
    public async Task Admin_ReportStartsAtVietnamMidnight()
    {
        var profile = UserProfile.Create(Guid.NewGuid(), "Admin", FixedClock.Now);
        profile.SetRole(UserRole.Admin, FixedClock.Now);
        var repository = new TrafficRepository();
        await CreateService(profile, repository).GetReportAsync(7, CancellationToken.None);
        Assert.Equal(new DateTimeOffset(2026, 9, 26, 17, 0, 0, TimeSpan.Zero), repository.From);
    }

    private static WebsiteTrafficService CreateService(UserProfile? profile, TrafficRepository repository) =>
        new(new UserContext(new CurrentUser(profile?.Id), new ProfileRepository(profile)), repository, new FixedClock());

    private sealed record CurrentUser(Guid? UserId) : ICurrentUser;
    private sealed class FixedClock : TimeProvider
    {
        public static readonly DateTimeOffset Now = new(2026, 10, 2, 18, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class TrafficRepository : IWebsiteTrafficRepository
    {
        public WebsitePageView? Recorded { get; private set; }
        public bool Read { get; private set; }
        public DateTimeOffset From { get; private set; }
        public Task RecordAsync(WebsitePageView pageView, CancellationToken cancellationToken) { Recorded = pageView; return Task.CompletedTask; }
        public Task<WebsiteTrafficReportDto> GetReportAsync(DateTimeOffset from, DateTimeOffset until, int days, CancellationToken cancellationToken)
        {
            Read = true;
            From = from;
            return Task.FromResult(new WebsiteTrafficReportDto(until, days, null, 0, 0, 0, [], []));
        }
    }
    private sealed class ProfileRepository(UserProfile? profile) : IUserProfileRepository
    {
        public Task<UserProfile?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(profile);
        public Task<UserProfile> UpsertAsync(UserProfile value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<UserProfile> SaveAsync(UserProfile value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<UserProfile>> GetByIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> GetAllUserIdsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<UserProfile>> GetMatchingRentersAsync(string address, decimal price, Guid excludedUserId, int take, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
