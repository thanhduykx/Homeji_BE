using Homeji.Domain.Enums;
using Homeji.Application.Services.RentalPosts;
using Homeji.Application.Services.RentalPosts.Validation;
using Homeji.Application.Services.WantedPosts;
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
    [LocalMarketplaceDatabaseFact]
    public async Task A_numeric_confirmation_reuses_the_pending_draft_without_calling_ai_or_publishing()
    {
        await using var db = Open(); var user = await Seed(db, true);
        var initial = (await Service(db, user).PrepareAsync("đăng pas cái ghế 500k", [new("image/png", [1])], null)).Draft!;
        var result = await Service(db, user, new NoGeneration()).PrepareAsync("1", [], initial.Id);
        Assert.Equal(initial.Id, result.Draft!.Id); Assert.Equal(System.Text.Json.JsonSerializer.Serialize(initial.Details), System.Text.Json.JsonSerializer.Serialize(result.Draft.Details));
        Assert.Equal(initial.Images, result.Draft.Images); Assert.Contains("chưa có bài nào được đăng", result.Message);
        Assert.Equal(1, await db.AgentPostDrafts.CountAsync(row => row.UserId == user));
        Assert.False(await db.MarketplacePosts.AnyAsync(row => row.SellerId == user));
    }
    private sealed class NoGeneration : IChatbotAiClient
    {
        public Task<string> GenerateMediaReplyAsync(IReadOnlyCollection<ChatbotMessageDto> messages, string text, IReadOnlyCollection<ChatbotMediaContent> media, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Numeric approval is not an AI generation request");
        public Task<string> GenerateReplyAsync(IReadOnlyCollection<ChatbotMessageDto> messages, string text, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> TranscribeAsync(ChatbotMediaContent audio, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    [Homeji.Api.IntegrationTests.Infrastructure.LiveAgentProviderFact]
    public async Task Reported_request_with_real_provider_creates_a_located_approval_draft_not_instructions()
    {
        await using var db = Open(); var user = await Seed(db, true);
        var root = Environment.GetEnvironmentVariable("HOMEJI_AGENT_LIVE_ROOT")!;
        using var settings = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, "src/Homeji.Api/appsettings.Local.json")));
        var config = settings.RootElement.GetProperty("Ai").GetProperty("Gemini");
        var options = new GeminiOptions { ApiKey = config.GetProperty("ApiKey").GetString(), MaxRetryAttempts = 1 };
        if (config.TryGetProperty("Endpoint", out var endpoint)) options.Endpoint = endpoint.GetString()!;
        var processor = new ConversationImageProcessor(); using var http = new HttpClient();
        var provider = new GeminiChatbotClient(http, Options.Create(options), Microsoft.Extensions.Logging.Abstractions.NullLogger<GeminiChatbotClient>.Instance, processor);
        var context = new UserContext(new CurrentUser(user), new UserProfileRepository(db));
        var chatbot = new Homeji.Application.Services.Chatbot.ChatbotService(context, new ChatConversationRepository(db), provider,
            null!, null!, null!, Options.Create(new Homeji.Application.Services.Chatbot.ChatbotOptions { Enabled = true, HistoryStorageEnabled = true }), TimeProvider.System,
            Service(db, user, provider, processor));
        var png = await File.ReadAllBytesAsync(Environment.GetEnvironmentVariable("HOMEJI_AGENT_LIVE_IMAGE")!);
        var response = await chatbot.SendMessageAsync(new(null, "đăng pas lại cái ghế giá 500k", SaveHistory: true,
            Attachments: [new("image/png", Convert.ToBase64String(png))], Location: new(10.84m, 106.8m, "Lê Văn Việt, Thủ Đức", DateTimeOffset.UtcNow, 15)));
        Assert.NotNull(response.AgentDraft); Assert.True(response.AgentRequested);
        Assert.Equal(500000, response.AgentDraft.Details.Price); Assert.Equal("Lê Văn Việt, Thủ Đức", response.AgentDraft.Details.Address);
        Assert.Equal(2, (await chatbot.GetMessagesAsync(response.ConversationId)).Count);
        Assert.False(await db.MarketplacePosts.AnyAsync(row => row.SellerId == user));
        var refined = await chatbot.SendMessageAsync(new(response.ConversationId,
            "1. tình trạng mới, kick thước 500mm*200mm, chất liệu da", SaveHistory: true, AgentDraftId: response.AgentDraft.Id));
        Assert.NotNull(refined.AgentDraft); Assert.Equal(500000, refined.AgentDraft.Details.Price);
        Assert.Single(refined.AgentDraft.Images);
        Assert.Contains("500", refined.AgentDraft.Details.Description); Assert.Contains("200", refined.AgentDraft.Details.Description);
        Assert.Contains("da", Homeji.Application.Services.AI.RentalSearchIntent.Normalize(refined.AgentDraft.Details.Description));
        Assert.Contains("moi", Homeji.Application.Services.AI.RentalSearchIntent.Normalize(refined.AgentDraft.Details.Condition));
        var confirmation = await chatbot.SendMessageAsync(new(response.ConversationId, "1", SaveHistory: true, AgentDraftId: refined.AgentDraft.Id));
        Assert.Equal(refined.AgentDraft.Id, confirmation.AgentDraft!.Id);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(refined.AgentDraft.Details), System.Text.Json.JsonSerializer.Serialize(confirmation.AgentDraft.Details));
        Assert.Contains("chưa có bài nào được đăng", confirmation.AssistantMessage.Content);
        Assert.False(await db.MarketplacePosts.AnyAsync(row => row.SellerId == user));
        Assert.Equal(6, (await chatbot.GetMessagesAsync(response.ConversationId)).Count);
        await File.WriteAllTextAsync(Path.Combine(root, "output/verification/agent-real-full-flow.json"), System.Text.Json.JsonSerializer.Serialize(new {
            verified = true, exactTypoRequest = true, realProvider = true, price = 500000, imageRetainedAcrossTurns = true,
            dimensionsAndLeatherRetained = true, numericReplyKeepsApprovalDraft = true, locationFilled = true,
            optedInHistoryKept = true, approvalRequired = true, published = false, isolatedDatabase = true }));
    }
    [Homeji.Api.IntegrationTests.Infrastructure.LiveAgentProviderFact]
    public async Task Reported_rental_uses_real_provider_and_preserves_rent_deposit_and_minimum_lease()
    {
        await using var db = Open(); var user = await Seed(db, true);
        var author = await db.UserProfiles.SingleAsync(row => row.Id == user); author.SetRole(UserRole.Landlord, DateTimeOffset.UtcNow); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var root = Environment.GetEnvironmentVariable("HOMEJI_AGENT_LIVE_ROOT")!;
        using var settings = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, "src/Homeji.Api/appsettings.Local.json")));
        var config = settings.RootElement.GetProperty("Ai").GetProperty("Gemini");
        var options = new GeminiOptions { ApiKey = config.GetProperty("ApiKey").GetString(), MaxRetryAttempts = 1 };
        if (config.TryGetProperty("Endpoint", out var endpoint)) options.Endpoint = endpoint.GetString()!;
        var processor = new ConversationImageProcessor(); using var http = new HttpClient();
        var provider = new GeminiChatbotClient(http, Options.Create(options), Microsoft.Extensions.Logging.Abstractions.NullLogger<GeminiChatbotClient>.Instance, processor);
        var result = await Service(db, user, provider, processor).PrepareAsync("tôi muốn đăng cho thuê phòng này giá 8triệu / tháng cọc 16 tr, bếp rèm, máy lạnh đầy đủ, ký ít nhất 12 tháng", [], null);
        Assert.NotNull(result.Draft); Assert.Equal("rental", result.Draft.Details.Kind);
        Assert.Equal(8000000, result.Draft.Details.Price); Assert.Equal(16000000, result.Draft.Details.Deposit); Assert.Equal(12, result.Draft.Details.MinimumLeaseMonths);
        Assert.Null(result.Draft.Details.Area); Assert.False(result.Draft.Details.OwnerConsentConfirmed);
        Assert.False(await db.MarketplacePosts.AnyAsync(row => row.SellerId == user)); Assert.False(await db.RentalPosts.AnyAsync(row => row.OwnerId == user));
        await File.WriteAllTextAsync(Path.Combine(root, "output/verification/agent-real-rental-flow.json"), "{\"verified\":true,\"kind\":\"rental\",\"rent\":8000000,\"deposit\":16000000,\"minimumLeaseMonths\":12,\"unknownAreaLeftBlank\":true,\"published\":false}");
    }

    [LocalMarketplaceDatabaseFact]
    public async Task Preparing_a_draft_does_not_detach_the_enclosing_opted_in_chat_history()
    {
        await using var db = Open(); var user = await Seed(db, true); var now = DateTimeOffset.UtcNow;
        var conversation = ChatConversation.Create(user, "Đăng ghế", now);
        conversation.AddUserMessage("đăng pass lại cái ghế này 500k", now);
        db.ChatConversations.Add(conversation);
        await Service(db, user).PrepareAsync("đăng pass lại cái ghế này 500k", [new("image/png", [1])], null);
        conversation.AddAssistantMessage("Đã soạn bản nháp để bạn duyệt", now.AddSeconds(1));
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var saved = await db.ChatConversations.Include(row => row.Messages).SingleAsync(row => row.Id == conversation.Id);
        Assert.Equal(2, saved.Messages.Count);
    }
    [LocalMarketplaceDatabaseFact]
    public async Task Shared_location_fills_first_post_and_can_fill_an_initially_empty_draft()
    {
        await using var db = Open(); var user = await Seed(db, true);
        var location = new ChatbotLocationDto(10.84m, 106.8m, "Lê Văn Việt, Thủ Đức", DateTimeOffset.UtcNow, 15);
        var first = (await Service(db, user).PrepareAsync("đăng pass lại cái ghế này 500k", [new("image/png", [1])], null, location)).Draft!;
        Assert.Equal(location.Address, first.Details.Address); Assert.Equal(location.Latitude, first.Details.Latitude);
        Assert.False(await db.MarketplacePosts.AnyAsync(row => row.SellerId == user));
        var noLocation = (await Service(db, user).PrepareAsync("đăng pass ghế", [new("image/png", [1])], null)).Draft!;
        Assert.Equal(0, noLocation.Details.Latitude);
        var revised = (await Service(db, user).PrepareAsync("dùng vị trí vừa chọn", [], noLocation.Id, location)).Draft!;
        Assert.Equal(location.Address, revised.Details.Address); Assert.Equal(location.Longitude, revised.Details.Longitude);
    }

    [LocalMarketplaceDatabaseFact]
    public async Task Nearby_search_uses_actual_coordinates_budget_and_excludes_expired_and_unlocated_rooms()
    {
        await using var db = Open();
        async Task<Guid> Add(decimal price, decimal? lat, bool expired = false)
        {
            var key = Random.Shared.NextInt64(1000000000000000, 9000000000000000).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var row = RentalSourceListing.Create(key, "https://mogi.vn/room-id" + key, "Phòng kiểm thử vị trí", "Lê Văn Việt", "quan-9", price, 26, ["https://cloud.mogi.vn/fixture.jpg"], null, DateTimeOffset.UtcNow, "mogi");
            row.RecordSourceExpiry(DateTimeOffset.UtcNow.AddDays(expired ? -1 : 1), DateTimeOffset.UtcNow);
            db.RentalSourceListings.Add(row); await db.SaveChangesAsync();
            if (lat.HasValue) await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE homeji.rental_source_listings SET latitude={lat},longitude={106.8m} WHERE id={row.Id}");
            return row.Id;
        }
        var near = await Add(2500000, 10.845m); var far = await Add(2500000, 10.90m);
        var overBudget = await Add(5000000, 10.841m); var expired = await Add(2500000, 10.841m, true); var unlocated = await Add(2500000, null);
        var service = new ChatbotNearbyService(db, TimeProvider.System);
        Assert.Equal("locationRequired", (await service.SearchAsync("Tìm phòng gần tôi", null, default)).Status);
        var result = await service.SearchAsync("Tìm phòng gần tôi dưới 3 triệu", new(10.84m, 106.8m, null, DateTimeOffset.UtcNow), default);
        Assert.Contains(result.Items, item => item.Id == near && item.DistanceKm is > 0 and < 1);
        Assert.DoesNotContain(result.Items, item => item.Id == far || item.Id == overBudget || item.Id == expired || item.Id == unlocated);
    }
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

    [LocalMarketplaceDatabaseFact]
    public async Task Rental_pass_roommate_wanted_and_food_publish_to_their_own_business_models()
    {
        foreach (var kind in new[] { "rental", "roomTransfer", "roommate", "wanted", "food" })
        {
            await using var db = Open(); var user = await Seed(db, true);
            if (kind is "rental" or "roommate")
            {
                var author = await db.UserProfiles.SingleAsync(row => row.Id == user);
                author.SetRole(UserRole.Landlord, DateTimeOffset.UtcNow); await db.SaveChangesAsync();
            }
            db.ChangeTracker.Clear();
            var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
            var details = Details with { Kind = kind, Deposit = 16000000, Area = 54, MaxOccupants = 2, AvailableSlots = 1,
                MinimumLeaseMonths = 12, AvailableFrom = tomorrow, OriginalLeaseEndsOn = tomorrow.AddYears(1),
                TransferKind = 1, PassFee = 0, TransferReason = "Chuyển nơi làm việc", OwnerConsentConfirmed = true, OwnerConsentContact = "0901234567",
                AvailableQuantity = 20, Unit = "phần", PreparationMinutes = 15 };
            var text = kind switch { "rental" => "tôi muốn đăng cho thuê phòng này giá 8 triệu cọc 16 triệu", "roomTransfer" => "đăng tin pass phòng", "roommate" => "đăng tin tìm người ở ghép", "wanted" => "đăng tin tìm trọ", _ => "đăng bán cơm gà" };
            var service = Service(db, user, new BusinessProvider(details));
            var response = await service.PrepareAsync(text, kind == "wanted" ? [] : [new("image/png", [1]), new("image/png", [2]), new("image/png", [3])], null,
                new(10.84m, 106.8m, "Lê Văn Việt", DateTimeOffset.UtcNow));
            var draft = response.Draft!;
            Assert.Equal(kind, draft.Details.Kind); Assert.False(draft.Details.OwnerConsentConfirmed);
            Assert.False(await db.RentalPosts.AnyAsync(row => row.OwnerId == user)); Assert.False(await db.MarketplacePosts.AnyAsync(row => row.SellerId == user));
            await Assert.ThrowsAsync<RequestValidationException>(() => service.ConfirmAsync(draft.Id, details with { Kind = "secondHand" }, default));
            var id = await service.ConfirmAsync(draft.Id, details, default);
            Assert.Equal(id, await service.ConfirmAsync(draft.Id, details, default));
            if (kind is "rental" or "roomTransfer" or "roommate")
            {
                var room = await db.RentalPosts.AsNoTracking().Include(row => row.Media).SingleAsync(row => row.Id == id);
                Assert.Equal(kind == "rental" ? RentalPostType.VacantRoom : kind == "roommate" ? RentalPostType.RoommateShare : RentalPostType.RoomTransfer, room.Type);
                Assert.Equal(RentalPostStatus.Pending, room.Status); Assert.Equal(16000000, room.Deposit); Assert.Equal(3, room.Media.Count);
                Assert.Contains("12 tháng", room.HouseRules!, StringComparison.Ordinal);
                Assert.False(await db.MarketplacePosts.AnyAsync(row => row.SellerId == user));
            }
            else if (kind == "wanted") Assert.True(await db.RentalWantedPosts.AnyAsync(row => row.Id == id && row.RequesterId == user));
            else { var food = await db.MarketplacePosts.SingleAsync(row => row.Id == id); Assert.Equal(MarketplaceListingType.Food, food.ListingType); Assert.Equal(20, food.AvailableQuantity); Assert.Equal("phần", food.Unit); }
        }
    }
    [LocalMarketplaceDatabaseFact]
    public async Task Wrong_role_and_missing_rental_fields_never_publish_marketplace_posts()
    {
        await using var db = Open(); var user = await Seed(db, true);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Service(db, user).PrepareAsync("đăng cho thuê phòng", [], null));
        Assert.False(await db.AgentPostDrafts.AnyAsync(row => row.UserId == user));
    }
    [LocalMarketplaceDatabaseFact]
    public async Task Old_real_estate_draft_cannot_be_confirmed_as_secondhand_goods()
    {
        await using var db = Open(); var user = await Seed(db, true);
        var oldDetails = Details with { Title = "Phòng 54m²", Category = "Bất động sản" };
        var payload = System.Text.Json.JsonSerializer.Serialize(new { details = oldDetails, images = new[] { new { mimeType = "image/webp", data = "AQ==" } } });
        var draft = new AgentPostDraft(user, payload, DateTimeOffset.UtcNow); db.AgentPostDrafts.Add(draft); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<RequestValidationException>(() => Service(db, user).ConfirmAsync(draft.Id, oldDetails, default));
        Assert.False(await db.MarketplacePosts.AnyAsync(row => row.SellerId == user));
    }

    [LocalMarketplaceDatabaseFact]
    public async Task Explicit_property_address_does_not_inherit_coordinates_from_a_different_shared_location()
    {
        await using var db = Open(); var user = await Seed(db, true);
        var author = await db.UserProfiles.SingleAsync(row => row.Id == user); author.SetRole(UserRole.Landlord, DateTimeOffset.UtcNow); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var details = Details with { Kind = "rental", Address = "123 Lê Văn Việt, Thủ Đức" };
        var response = await Service(db, user, new BusinessProvider(details)).PrepareAsync("đăng cho thuê phòng tại 123 Lê Văn Việt", [], null,
            new(10.84m, 106.8m, "Linh Trung, Thủ Đức", DateTimeOffset.UtcNow));
        Assert.Equal(details.Address, response.Draft!.Details.Address);
        Assert.Equal(0, response.Draft.Details.Latitude); Assert.Equal(0, response.Draft.Details.Longitude);
    }

    private sealed class BusinessProvider(AgentPostDetails details) : IChatbotAiClient
    {
        private static readonly System.Text.Json.JsonSerializerOptions Json = new(System.Text.Json.JsonSerializerDefaults.Web);
        public Task<string> GenerateMediaReplyAsync(IReadOnlyCollection<ChatbotMessageDto> messages, string text, IReadOnlyCollection<ChatbotMediaContent> images, CancellationToken cancellationToken = default) => GeneratePostDraftAsync(text, images, cancellationToken);
        public Task<string> GeneratePostDraftAsync(string prompt, IReadOnlyCollection<ChatbotMediaContent> images, CancellationToken cancellationToken = default)
            => Task.FromResult(System.Text.Json.JsonSerializer.Serialize(details, Json));
        public Task<string> GenerateReplyAsync(IReadOnlyCollection<ChatbotMessageDto> messages, string text, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> TranscribeAsync(ChatbotMediaContent audio, CancellationToken cancellationToken = default) => throw new NotSupportedException();
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
    private static ChatbotPostingAgent Service(ApplicationDbContext db, Guid user, IChatbotAiClient? provider = null, IConversationImageProcessor? processor = null)
    {
        var profiles = new UserProfileRepository(db); var context = new UserContext(new CurrentUser(user), profiles);
        var marketplace = new MarketplacePostService(context, new MarketplacePostRepository(db), new RentalPostRepository(db),
            profiles, new ContentModerationService(new BadWordRepository(db)), TimeProvider.System,
            new WalletRepository(db), Options.Create(new MarketplaceFinanceOptions()));
        var rental = new RentalPostService(context, new RentalPostRepository(db), new UserSubscriptionRepository(db), new UpdateRentalPostDtoValidator(), new AddRentalPostMediaDtoValidator(),
            new ContentModerationService(new BadWordRepository(db)), null!, null!, profiles, null!, null!, TimeProvider.System);
        var wanted = new RentalWantedPostService(context, new RentalWantedPostRepository(db), profiles, new ContentModerationService(new BadWordRepository(db)), TimeProvider.System);
        return new(db, context, new UserSubscriptionRepository(db), provider ?? new Provider(), processor ?? new Processor(), new Upload(), marketplace, TimeProvider.System, rental, wanted);
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
            => Task.FromResult(new UploadImageResultDto($"https://res.cloudinary.com/demo/image/upload/{folder}/{Guid.NewGuid():N}.webp", "fixture", 100, 100, 100, "webp"));
    }
}
