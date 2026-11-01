using Homeji.Application.Abstractions.Authentication;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Chatbot;
using Homeji.Application.DTOs.Conversations;
using Homeji.Application.DTOs.Upload;
using Homeji.Application.IServices.Chatbot;
using Homeji.Application.IServices.Upload;
using Homeji.Application.Services.Common;
using Homeji.Application.Services.Marketplace;
using Homeji.Application.Services.Moderation;
using Homeji.Domain.Entities;
using Homeji.Infrastructure.Context;
using Homeji.Infrastructure.External;
using Homeji.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Homeji.Api.IntegrationTests;

public sealed class AgentPostingDatabaseTests
{
    private static readonly AgentPostDetails Details = new("Ghế sofa", "Ghế sofa trong ảnh, xem ảnh trước khi mua.",
        500000, "Đã sử dụng", "Nội thất", "Lê Văn Việt, Thủ Đức", 10.84m, 106.8m);

    [LocalMarketplaceDatabaseFact]
    public async Task Basic_users_are_offered_upgrade_without_calling_provider_or_posting()
    {
        await using var db = Open(); var user = await Seed(db, false);
        var result = await Service(db, user).PrepareAsync("Đăng bán ghế 500k", [new("image/png", [1])], null, default);
        Assert.True(result.UpgradeRequired); Assert.Null(result.Draft);
        Assert.False(await db.AgentPostDrafts.AnyAsync(row => row.UserId == user));
        Assert.False(await db.MarketplacePosts.AnyAsync(row => row.SellerId == user));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Service(db, user).ConfirmAsync(Guid.NewGuid(), Details, default));
    }

    [LocalMarketplaceDatabaseFact]
    public async Task Preview_does_not_post_and_concurrent_approval_retries_publish_once()
    {
        await using var setup = Open(); var user = await Seed(setup, true);
        var preview = await Service(setup, user).PrepareAsync("Tự đăng ghế trong ảnh 500k", [new("image/png", [1, 2, 3])], null, default);
        Assert.NotNull(preview.Draft); Assert.False(await setup.MarketplacePosts.AnyAsync(row => row.SellerId == user));
        await using var first = Open(); await using var second = Open();
        var outcomes = await Task.WhenAll(Service(first, user).ConfirmAsync(preview.Draft.Id, Details, default),
            Service(second, user).ConfirmAsync(preview.Draft.Id, Details, default));
        Assert.Equal(outcomes[0], outcomes[1]);
        await using var verify = Open();
        var post = Assert.Single(await verify.MarketplacePosts.Where(row => row.SellerId == user).ToArrayAsync());
        Assert.Equal(500000, post.Price); Assert.Equal("Ghế sofa", post.Title);
        var draft = await verify.AgentPostDrafts.SingleAsync(row => row.Id == preview.Draft.Id);
        Assert.Equal("{}", draft.Payload); Assert.Equal(post.Id, draft.PublishedPostId);
        await Assert.ThrowsAsync<RequestValidationException>(() => Service(verify, user).ConfirmAsync(draft.Id, Details with { Price = 1 }, default));
    }

    [LocalMarketplaceDatabaseFact]
    public async Task Ownership_cancel_expiry_and_invalid_price_prevent_publication()
    {
        await using var db = Open(); var user = await Seed(db, true); var other = await Seed(db, true);
        var preview = (await Service(db, user).PrepareAsync("Đăng ghế", [new("image/png", [1])], null, default)).Draft!;
        await Assert.ThrowsAsync<NotFoundException>(() => Service(db, other).ConfirmAsync(preview.Id, Details, default));
        await Assert.ThrowsAsync<RequestValidationException>(() => Service(db, user).ConfirmAsync(preview.Id, Details with { Price = 0 }, default));
        await Service(db, user).CancelAsync(preview.Id, default);
        await Assert.ThrowsAsync<RequestValidationException>(() => Service(db, user).ConfirmAsync(preview.Id, Details, default));
        db.ChangeTracker.Clear();
        var expired = new AgentPostDraft(user, "{}", DateTimeOffset.UtcNow.AddHours(-2));
        db.AgentPostDrafts.Add(expired); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<RequestValidationException>(() => Service(db, user).ConfirmAsync(expired.Id, Details, default));
        Assert.False(await db.MarketplacePosts.AnyAsync(row => row.SellerId == user));
        Assert.True(await db.Database.SqlQueryRaw<bool>("SELECT relrowsecurity AS \"Value\" FROM pg_class WHERE oid='homeji.agent_post_drafts'::regclass").SingleAsync());
    }

    private static ApplicationDbContext Open()
    {
        var connection = Environment.GetEnvironmentVariable("HOMEJI_TEST_DATABASE")!;
        var settings = new NpgsqlConnectionStringBuilder(connection);
        if (settings.Host != "127.0.0.1" || settings.Database != "homeji_quality") throw new InvalidOperationException("Isolated local database required.");
        return new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection, options => options.EnableRetryOnFailure()).Options);
    }
    private static async Task<Guid> Seed(ApplicationDbContext db, bool premium)
    {
        var user = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO auth.users(id) VALUES ({user})");
        db.UserProfiles.Add(UserProfile.Create(user, "Agent local fixture", now));
        var wallet = WalletAccount.Create(user, now); wallet.CreditTopUp(100000, now); db.WalletAccounts.Add(wallet);
        if (premium) db.UserSubscriptions.Add(UserSubscription.CreatePremium(user, "PREMIUM_MONTHLY", "Fixture Premium", null, now, 30, now));
        await db.SaveChangesAsync(); return user;
    }
    private static ChatbotPostingAgent Service(ApplicationDbContext db, Guid user)
    {
        var profiles = new UserProfileRepository(db); var context = new UserContext(new CurrentUser(user), profiles);
        var marketplace = new MarketplacePostService(context, new MarketplacePostRepository(db), new RentalPostRepository(db),
            profiles, new ContentModerationService(new BadWordRepository(db)), TimeProvider.System,
            new WalletRepository(db), Options.Create(new MarketplaceFinanceOptions()));
        return new(db, context, new UserSubscriptionRepository(db), new Provider(), new Processor(), new Upload(), marketplace, TimeProvider.System);
    }
    private sealed record CurrentUser(Guid? UserId) : ICurrentUser;
    private sealed class Provider : IChatbotAiClient
    {
        public Task<string> GenerateMediaReplyAsync(IReadOnlyCollection<ChatbotMessageDto> conversationMessages, string latestUserMessage, IReadOnlyCollection<ChatbotMediaContent> attachments, CancellationToken cancellationToken = default)
            => Task.FromResult("{\"title\":\"Ghế sofa\",\"description\":\"Ghế sofa trong ảnh\",\"price\":500000,\"condition\":\"Đã sử dụng\",\"category\":\"Nội thất\"}");
        public Task<string> GenerateReplyAsync(IReadOnlyCollection<ChatbotMessageDto> conversationMessages, string latestUserMessage, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> TranscribeAsync(ChatbotMediaContent audio, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class Processor : IConversationImageProcessor
    {
        public Task<ProcessedConversationImage> ProcessAsync(ConversationImageUpload upload, CancellationToken cancellationToken = default)
            => Task.FromResult(new ProcessedConversationImage("image/webp", upload.Content, 100, 100, "fixture"));
    }
    private sealed class Upload : IImageUploadService
    {
        public Task<UploadImageResultDto> UploadAsync(string fileName, string contentType, Stream fileStream, string? folder, CancellationToken cancellationToken)
            => Task.FromResult(new UploadImageResultDto("https://res.cloudinary.com/demo/image/upload/fixture.webp", "fixture", 100, 100, 100, "webp"));
    }
}
