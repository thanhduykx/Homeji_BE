using Homeji.Application.Abstractions.Authentication;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Conversations;
using Homeji.Application.IRepositories.Conversations;
using Homeji.Application.IRepositories.Profiles;
using Homeji.Application.IRepositories.WantedPosts;
using Homeji.Application.Services.Common;
using Homeji.Application.Services.Conversations;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;

namespace Homeji.Application.UnitTests.Conversations;

public sealed class PostConversationServiceTests
{
    private static readonly DateTimeOffset UtcNow = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(UserRole.Renter)]
    [InlineData(UserRole.Landlord)]
    public async Task StartWantedPostConversationAsync_WhenAnotherUserContactsRequester_CreatesConversation(
        UserRole senderRole)
    {
        var requesterId = Guid.NewGuid();
        var otherRenterId = Guid.NewGuid();
        var wantedPost = CreateWantedPost(requesterId);
        var profiles = new StubUserProfileRepository(
            CreateProfile(requesterId, UserRole.Renter),
            CreateProfile(otherRenterId, senderRole));
        var conversations = new StubPostConversationRepository();
        var service = CreateService(otherRenterId, wantedPost, profiles, conversations);

        var result = await service.StartWantedPostConversationAsync(wantedPost.Id);

        Assert.Equal(ConversationSubjectType.WantedPost, result.SubjectType);
        Assert.Equal(wantedPost.Id, result.SubjectId);
        Assert.Equal(requesterId, result.OtherParticipantId);
        Assert.NotNull(conversations.AddedConversation);
        Assert.True(conversations.AddedConversation.Includes(otherRenterId));
        Assert.True(conversations.AddedConversation.Includes(requesterId));
    }

    [Fact]
    public async Task StartWantedPostConversationAsync_WhenRequesterContactsOwnPost_ThrowsForbidden()
    {
        var requesterId = Guid.NewGuid();
        var wantedPost = CreateWantedPost(requesterId);
        var profiles = new StubUserProfileRepository(CreateProfile(requesterId, UserRole.Renter));
        var conversations = new StubPostConversationRepository();
        var service = CreateService(requesterId, wantedPost, profiles, conversations);

        var exception = await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            service.StartWantedPostConversationAsync(wantedPost.Id));

        Assert.Equal("You cannot start a conversation with yourself.", exception.Message);
        Assert.Null(conversations.AddedConversation);
    }

    [Fact]
    public async Task StartWantedPostConversationAsync_WhenUserIsNotAuthenticated_ThrowsUnauthorized()
    {
        var requesterId = Guid.NewGuid();
        var wantedPost = CreateWantedPost(requesterId);
        var profiles = new StubUserProfileRepository(CreateProfile(requesterId, UserRole.Renter));
        var conversations = new StubPostConversationRepository();
        var service = CreateService(null, wantedPost, profiles, conversations);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.StartWantedPostConversationAsync(wantedPost.Id));

        Assert.Null(conversations.AddedConversation);
    }

    private static PostConversationService CreateService(
        Guid? currentUserId,
        RentalWantedPost wantedPost,
        IUserProfileRepository profiles,
        IPostConversationRepository conversations)
    {
        return new PostConversationService(
            new UserContext(new StubCurrentUser(currentUserId), profiles),
            conversations,
            rentalPosts: null!,
            marketplacePosts: null!,
            new StubRentalWantedPostRepository(wantedPost),
            profiles,
            notifications: null!,
            realtimePublisher: null!,
            new StubTimeProvider(UtcNow),
            imageProcessor: null!);
    }

    private static RentalWantedPost CreateWantedPost(Guid requesterId)
    {
        return new RentalWantedPost(
            requesterId,
            "Tìm bạn ở ghép",
            "Cần tìm một bạn sinh viên ở ghép.",
            "Thạch Hòa",
            4_000_000,
            2,
            ["AIR_CONDITIONER"],
            new DateOnly(2026, 8, 1),
            UtcNow);
    }

    private static UserProfile CreateProfile(Guid id, UserRole role)
    {
        var profile = UserProfile.Create(id, $"User {id:N}", UtcNow);
        profile.UpdateLifestyle(
            role,
            SleepHabit.EarlyBird,
            PetPreference.NoPets,
            SmokingPreference.NonSmoking,
            4_000_000,
            "Thạch Hòa",
            UtcNow);
        return profile;
    }

    private sealed record StubCurrentUser(Guid? UserId) : ICurrentUser;

    private sealed class StubTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class StubRentalWantedPostRepository(RentalWantedPost wantedPost)
        : IRentalWantedPostRepository
    {
        public Task<RentalWantedPost?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<RentalWantedPost?>(id == wantedPost.Id ? wantedPost : null);
        }

        public Task<IReadOnlyList<RentalWantedPost>> SearchActiveAsync(
            string? area,
            decimal? maxBudget,
            int skip,
            int take,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task AddAsync(RentalWantedPost post, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubPostConversationRepository : IPostConversationRepository
    {
        public PostConversation? AddedConversation { get; private set; }

        public Task<PostConversation?> FindAsync(
            ConversationSubjectType subjectType,
            Guid subjectId,
            Guid participantAId,
            Guid participantBId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<PostConversation?>(null);
        }

        public Task AddConversationAsync(
            PostConversation conversation,
            CancellationToken cancellationToken = default)
        {
            AddedConversation = conversation;
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<PostConversation?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<PostConversation>> GetForUserAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<PostMessage>> GetMessagesAsync(
            Guid conversationId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<PostMessageAttachment?> GetAttachmentAsync(
            Guid conversationId,
            Guid messageId,
            Guid attachmentId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<int> CountAttachmentsByUploaderSinceAsync(
            Guid uploaderId,
            DateTimeOffset since,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyDictionary<Guid, ConversationLastMessageDto>> GetLatestByConversationIdsAsync(
            IReadOnlyCollection<Guid> conversationIds,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyDictionary<Guid, int>> CountUnreadByConversationIdsAsync(
            Guid userId,
            IReadOnlyCollection<PostConversation> conversations,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyDictionary<Guid, int>> CountBySubjectsAsync(
            ConversationSubjectType subjectType,
            IReadOnlyCollection<Guid> subjectIds,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task AddMessageAsync(PostMessage message, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubUserProfileRepository(params UserProfile[] profiles) : IUserProfileRepository
    {
        private readonly IReadOnlyDictionary<Guid, UserProfile> _profiles =
            profiles.ToDictionary(profile => profile.Id);

        public Task<UserProfile?> GetByIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_profiles.GetValueOrDefault(userId));
        }

        public Task<IReadOnlyList<UserProfile>> GetByIdsAsync(
            IReadOnlyCollection<Guid> userIds,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<UserProfile> result = userIds
                .Select(id => _profiles.GetValueOrDefault(id))
                .Where(profile => profile is not null)
                .Cast<UserProfile>()
                .ToArray();
            return Task.FromResult(result);
        }

        public Task<UserProfile> UpsertAsync(
            UserProfile profile,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<UserProfile> SaveAsync(
            UserProfile profile,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<UserProfile>> GetMatchingRentersAsync(
            string address,
            decimal price,
            Guid excludedUserId,
            int take,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
