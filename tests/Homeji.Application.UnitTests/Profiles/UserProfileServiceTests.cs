using Homeji.Application.Abstractions.Authentication;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Profiles;
using Homeji.Application.IRepositories.Profiles;
using Homeji.Application.IRepositories.Subscriptions;
using Homeji.Application.Services.Profiles;
using Homeji.Application.Services.Profiles.Validation;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;

namespace Homeji.Application.UnitTests.Profiles;

public sealed class UserProfileServiceTests
{
    private static readonly Guid UserId = Guid.Parse("bd2528a7-1b9b-4ac8-b7cb-12baa8693472");
    private static readonly DateTimeOffset UtcNow = new(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Public_profile_excludes_private_contact_and_lifestyle_fields()
    {
        var repository = new InMemoryUserProfileRepository();
        var profile = UserProfile.Create(UserId, "Public Name", UtcNow);
        profile.UpdateProfile("Public Name", "0901234567", null, "Private school", "Private area", "Private address", "Private rental need", UtcNow);
        await repository.UpsertAsync(profile);
        var result = await CreateService(repository).GetPublicProfileAsync(UserId);
        var json = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.Equal("Public Name", result.DisplayName);
        Assert.DoesNotContain("0901234567", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Private", json, StringComparison.Ordinal);
        Assert.False(result.IsPremium);
    }

    [Fact]
    public async Task Missing_public_profile_returns_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateService(new InMemoryUserProfileRepository()).GetPublicProfileAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task UpsertMyProfileAsync_WhenProfileDoesNotExist_CreatesProfile()
    {
        var repository = new InMemoryUserProfileRepository();
        var service = CreateService(repository);

        var result = await service.UpsertMyProfileAsync(new UpdateMyProfileDto("  Thanh Duy  "));

        Assert.Equal(UserId, result.Id);
        Assert.Equal("Thanh Duy", result.DisplayName);
        Assert.Equal(UtcNow, result.CreatedAt);
        Assert.Same(repository.Profile, repository.UpsertedProfile);
    }

    [Fact]
    public async Task UpsertMyProfileAsync_WhenRequestIsInvalid_DoesNotPersist()
    {
        var repository = new InMemoryUserProfileRepository();
        var service = CreateService(repository);

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() =>
            service.UpsertMyProfileAsync(new UpdateMyProfileDto(" ")));

        Assert.Contains(nameof(UpdateMyProfileDto.DisplayName), exception.Errors.Keys);
        Assert.Null(repository.UpsertedProfile);
    }

    [Theory]
    [InlineData("1234567890")]
    [InlineData("090123456")]
    [InlineData("09012abc67")]
    [InlineData("0123456789")]
    [InlineData("0551234567")]
    public async Task UpsertMyProfileAsync_WhenPhoneIsInvalid_DoesNotPersist(string phone)
    {
        var repository = new InMemoryUserProfileRepository();
        var service = CreateService(repository);

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() =>
            service.UpsertMyProfileAsync(new UpdateMyProfileDto("Thanh Duy", phone)));

        Assert.Contains(nameof(UpdateMyProfileDto.Phone), exception.Errors.Keys);
        Assert.Null(repository.UpsertedProfile);
    }

    [Fact]
    public async Task GetMyProfileAsync_WhenProfileDoesNotExist_ThrowsNotFoundException()
    {
        var service = CreateService(new InMemoryUserProfileRepository());

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetMyProfileAsync());
    }

    [Fact]
    public async Task Profile_and_lifestyle_updates_preserve_existing_administrator()
    {
        var repository = new InMemoryUserProfileRepository();
        var admin = UserProfile.Create(UserId, "Admin", UtcNow);
        admin.SetRole(UserRole.Admin, UtcNow);
        await repository.UpsertAsync(admin);
        var service = CreateService(repository);
        var profile = await service.UpsertMyProfileAsync(new UpdateMyProfileDto("Admin renamed"));
        Assert.Equal(UserRole.Admin, profile.Role);
        var lifestyle = await service.UpdateMyLifestyleAsync(new(UserRole.Renter, SleepHabit.EarlyBird,
            PetPreference.NoPets, SmokingPreference.NonSmoking, 3_000_000, "Linh Trung"));
        Assert.Equal(UserRole.Admin, lifestyle.Role);
        Assert.Equal(UserRole.Admin, repository.Profile!.Role);
        Assert.Equal("Linh Trung", lifestyle.PreferredArea);
        Assert.True(lifestyle.OnboardingCompleted);
    }

    [Theory]
    [InlineData(UserRole.Renter)]
    [InlineData(UserRole.Landlord)]
    public async Task Lifestyle_api_service_rejects_self_promotion_to_administrator(UserRole role)
    {
        var repository = new InMemoryUserProfileRepository();
        var profile = UserProfile.Create(UserId, "User", UtcNow);
        profile.SetRole(role, UtcNow);
        await repository.UpsertAsync(profile);
        var service = CreateService(repository);
        await Assert.ThrowsAsync<RequestValidationException>(() => service.UpdateMyLifestyleAsync(new(UserRole.Admin,
            SleepHabit.EarlyBird, PetPreference.NoPets, SmokingPreference.NonSmoking, null, null)));
        Assert.Equal(role, repository.Profile!.Role);
        Assert.False(repository.Profile.OnboardingCompleted);
    }

    [Fact]
    public async Task Premium_social_contacts_are_opt_in_and_hidden_after_expiry()
    {
        var repository = new InMemoryUserProfileRepository();
        var profile = UserProfile.Create(UserId, "VIP", UtcNow.AddYears(-2));
        profile.UpdateProfile("VIP", "0911111111", null, null, null, null, null, UtcNow);
        await repository.UpsertAsync(profile);
        var subscriptions = new EmptyUserSubscriptionRepository { Active = UserSubscription.CreatePremium(UserId, "PREMIUM_MONTHLY", "Premium", null, UtcNow, 30, UtcNow) };
        var service = CreateService(repository, subscriptions);
        var before = await service.GetPublicProfileAsync(UserId);
        Assert.Null(before.FacebookUrl); Assert.Null(before.ZaloPhone);
        var saved = await service.UpdateMySocialContactsAsync(new UpdateSocialContactsDto(" https://www.facebook.com/homeji.test ", "+84 901 234 567"));
        Assert.Equal("0901234567", saved.ZaloPhone);
        Assert.Equal(UtcNow.AddYears(-2), saved.CreatedAt);
        var published = await service.GetPublicProfileAsync(UserId);
        Assert.Equal(saved.FacebookUrl, published.FacebookUrl); Assert.Equal(saved.ZaloPhone, published.ZaloPhone);
        Assert.DoesNotContain("0911111111", System.Text.Json.JsonSerializer.Serialize(published), StringComparison.Ordinal);
        subscriptions.Active = null;
        var expired = await service.GetPublicProfileAsync(UserId);
        Assert.Null(expired.FacebookUrl); Assert.Null(expired.ZaloPhone);
        Assert.Equal(saved.ZaloPhone, (await service.GetMySocialContactsAsync()).ZaloPhone);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.UpdateMySocialContactsAsync(new UpdateSocialContactsDto(saved.FacebookUrl, saved.ZaloPhone)));
        var cleared = await service.UpdateMySocialContactsAsync(new UpdateSocialContactsDto(null, null));
        Assert.Null(cleared.FacebookUrl); Assert.Null(cleared.ZaloPhone);
    }

    [Theory]
    [InlineData("javascript:alert(1)", "0901234567")]
    [InlineData("https://facebook.com.evil.test/profile", "0901234567")]
    [InlineData("https://user:pass@facebook.com/name", "0901234567")]
    [InlineData("https://facebook.com:8443/name", "0901234567")]
    [InlineData("https://facebook.com/l.php?u=https://evil.test", "0901234567")]
    [InlineData("http://facebook.com/name", "0901234567")]
    [InlineData("https://facebook.com/name", "1234567890")]
    [InlineData("https://facebook.com/name", "090123456789")]
    public void Invalid_social_contacts_do_not_mutate_the_profile(string facebook, string zalo)
    {
        var profile = UserProfile.Create(UserId, "VIP", UtcNow);
        Assert.Throws<Homeji.Domain.Exceptions.DomainException>(() => profile.UpdateSocialContacts(facebook, zalo, UtcNow));
        Assert.Null(profile.FacebookUrl); Assert.Null(profile.ZaloPhone);
    }

    private static UserProfileService CreateService(InMemoryUserProfileRepository repository, IUserSubscriptionRepository? subscriptions = null)
    {
        return new UserProfileService(
            new StubCurrentUser(UserId),
            repository,
            subscriptions ?? new EmptyUserSubscriptionRepository(),
            new UpdateMyProfileDtoValidator(),
            new UpdateLifestyleDtoValidator(),
            new StubTimeProvider(UtcNow));
    }

    private sealed record StubCurrentUser(Guid? UserId) : ICurrentUser;

    private sealed class StubTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public StubTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    private sealed class InMemoryUserProfileRepository : IUserProfileRepository
    {
        public UserProfile? Profile { get; private set; }

        public UserProfile? UpsertedProfile { get; private set; }

        public Task<UserProfile?> GetByIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Profile?.Id == userId ? Profile : null);
        }

        public Task<UserProfile> UpsertAsync(
            UserProfile profile,
            CancellationToken cancellationToken = default)
        {
            Profile = profile;
            UpsertedProfile = profile;
            return Task.FromResult(profile);
        }

        public Task<UserProfile> SaveAsync(
            UserProfile profile,
            CancellationToken cancellationToken = default)
        {
            Profile = profile;
            return Task.FromResult(profile);
        }

        public Task<IReadOnlyList<UserProfile>> GetByIdsAsync(
            IReadOnlyCollection<Guid> userIds,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<UserProfile> profiles = Profile is not null && userIds.Contains(Profile.Id)
                ? [Profile]
                : [];

            return Task.FromResult(profiles);
        }

        public Task<IReadOnlyList<Guid>> GetAllUserIdsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>(Profile is null ? [] : [Profile.Id]);

        public Task<IReadOnlyList<UserProfile>> GetMatchingRentersAsync(
            string address,
            decimal price,
            Guid excludedUserId,
            int take,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<UserProfile>>([]);
        }
    }

    private sealed class EmptyUserSubscriptionRepository : IUserSubscriptionRepository
    {
        public UserSubscription? Active { get; set; }
        public Task<UserSubscription?> GetPremiumTailAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken = default) => GetActivePremiumAsync(userId, now, cancellationToken);
        public Task<UserSubscription?> GetActivePremiumAsync(
            Guid userId,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Active);
        }

        public Task<IReadOnlyDictionary<Guid, UserSubscription>> GetActivePremiumByUserIdsAsync(
            IReadOnlyCollection<Guid> userIds,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyDictionary<Guid, UserSubscription>>(
                new Dictionary<Guid, UserSubscription>());
        }

        public Task<UserSubscription?> GetByPaymentTransactionIdAsync(
            Guid paymentTransactionId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<UserSubscription?>(null);
        }

        public Task AddAsync(UserSubscription subscription, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
