using Homeji.Application.Abstractions.Authentication;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Profiles;
using Homeji.Application.Services.Profiles;
using Homeji.Application.Services.Profiles.Validation;
using Homeji.Domain.Entities;
using Homeji.Infrastructure.Context;
using Homeji.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace Homeji.Api.IntegrationTests;

public sealed class ProfileSocialContactsDatabaseTests
{
    [LocalMarketplaceDatabaseFact]
    public async Task Contacts_persist_belong_to_the_signed_in_user_and_disappear_publicly_after_expiry()
    {
        await using var db = Open(); var id = Guid.NewGuid(); var other = Guid.NewGuid();
        var created = new DateTimeOffset(2022, 6, 1, 0, 0, 0, TimeSpan.Zero);
        foreach (var user in new[] { id, other })
        {
            var email = user.ToString("N") + "@example.invalid";
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO auth.users(id,email,email_confirmed_at) VALUES ({user},{email},now())");
            db.UserProfiles.Add(UserProfile.Create(user, "Fixture contact", created));
        }
        db.UserSubscriptions.Add(UserSubscription.CreatePremium(id, "PREMIUM_MONTHLY", "Premium", null, DateTimeOffset.UtcNow.AddHours(-1), 30, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var service = Service(db, id, TimeProvider.System);
        await service.UpdateMySocialContactsAsync(new UpdateSocialContactsDto("https://www.facebook.com/homeji.test", "+84 901 234 567"));
        await using var reopened = Open();
        var saved = await Service(reopened, id, TimeProvider.System).GetMySocialContactsAsync();
        Assert.Equal("0901234567", saved.ZaloPhone); Assert.Equal(created, saved.CreatedAt);
        Assert.Null((await Service(reopened, other, TimeProvider.System).GetMySocialContactsAsync()).ZaloPhone);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Service(reopened, other, TimeProvider.System).UpdateMySocialContactsAsync(new UpdateSocialContactsDto(saved.FacebookUrl, saved.ZaloPhone)));
        var expired = Service(reopened, id, new FutureClock());
        var publicProfile = await expired.GetPublicProfileAsync(id);
        Assert.False(publicProfile.IsPremium); Assert.Null(publicProfile.FacebookUrl); Assert.Null(publicProfile.ZaloPhone);
        Assert.Equal("0901234567", (await expired.GetMySocialContactsAsync()).ZaloPhone);
        Assert.True(await reopened.Database.SqlQueryRaw<bool>("SELECT relrowsecurity AS \"Value\" FROM pg_class WHERE oid='homeji.user_profiles'::regclass").SingleAsync());
    }
    private static UserProfileService Service(ApplicationDbContext db, Guid id, TimeProvider clock) => new(new CurrentUser(id), new UserProfileRepository(db), new UserSubscriptionRepository(db), new UpdateMyProfileDtoValidator(), new UpdateLifestyleDtoValidator(), clock);
    private static ApplicationDbContext Open()
    {
        var connection = Environment.GetEnvironmentVariable("HOMEJI_TEST_DATABASE") ?? throw new InvalidOperationException("Local database required");
        var parsed = new NpgsqlConnectionStringBuilder(connection);
        if (parsed.Host != "127.0.0.1" || parsed.Database != "homeji_quality") throw new InvalidOperationException("Disposable loopback database only");
        return new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
    }
    private sealed record CurrentUser(Guid? UserId) : ICurrentUser;
    private sealed class FutureClock : TimeProvider { public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow.AddDays(31); }
}
