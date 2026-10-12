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

    private static UserProfileService CreateService(InMemoryUserProfileRepository repository)
    {
        return new UserProfileService(
            new StubCurrentUser(UserId),
            repository,
            new EmptyUserSubscriptionRepository(),
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
        public Task<UserSubscription?> GetActivePremiumAsync(
            Guid userId,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<UserSubscription?>(null);
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
