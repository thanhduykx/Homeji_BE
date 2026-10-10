using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Homeji.Application.DTOs.RentalPosts;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.Context;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Homeji.Infrastructure.IntegrationTests;

public sealed class RentalPostingHttpTests
{
    [Theory]
    [InlineData(RentalPostType.VacantRoom)]
    [InlineData(RentalPostType.RoommateShare)]
    [InlineData(RentalPostType.RoomTransfer)]
    public async Task HttpDraft_EditMediaSubmit_PersistsLatestDetailsAndEnforcesOwnership(RentalPostType type)
    {
        await using var db = await PostingPersistenceTests.OpenTestDatabaseAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var now = DateTimeOffset.UtcNow;
        var owner = UserProfile.Create(Guid.NewGuid(), "Người thuê HTTP kiểm thử", now);
        if (type != RentalPostType.RoomTransfer) owner.SetRole(UserRole.Landlord, now);
        var other = UserProfile.Create(Guid.NewGuid(), "Người thuê HTTP khác", now);
        db.UserProfiles.AddRange(owner, other);
        await db.SaveChangesAsync();
        await using var factory = new PostingApiFactory(db);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });

        using var anonymous = await client.PostAsJsonAsync("/api/rental-posts/drafts",
            new CreateRentalPostDraftDto(type));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        client.DefaultRequestHeaders.Add(PostingApiFactory.UserHeader, owner.Id.ToString());
        using var created = await client.PostAsJsonAsync("/api/rental-posts/drafts",
            new CreateRentalPostDraftDto(type));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var draft = await created.Content.ReadFromJsonAsync<RentalPostDto>();
        Assert.NotNull(draft);
        Assert.Equal(owner.Id, draft.OwnerId);
        Assert.Equal(RentalPostStatus.Draft, draft.Status);
        var path = $"/api/rental-posts/{draft.Id:D}";
        Assert.EndsWith(path, created.Headers.Location?.ToString());

        var details = new UpdateRentalPostDto(
            type, "Tiêu đề trước chỉnh sửa", "Phòng có cửa sổ và chỗ để xe.",
            2_500_000, 2_500_000, 18, "Linh Trung, Thủ Đức", 10.87m, 106.80m, ["wifi"],
            AvailableFrom: DateOnly.FromDateTime(now.UtcDateTime.AddDays(1)),
            TransferKind: RoomTransferKind.LeaseAssignment,
            OriginalLeaseEndsOn: DateOnly.FromDateTime(now.UtcDateTime.AddMonths(3)),
            TransferReason: "Chuyển nơi học", OwnerConsentConfirmed: true,
            OwnerConsentContact: "owner@example.test");
        using var invalid = await client.PutAsJsonAsync(path, details with { Latitude = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        client.DefaultRequestHeaders.Remove(PostingApiFactory.UserHeader);
        client.DefaultRequestHeaders.Add(PostingApiFactory.UserHeader, other.Id.ToString());
        using var forbidden = await client.PutAsJsonAsync(path, details);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        client.DefaultRequestHeaders.Remove(PostingApiFactory.UserHeader);
        client.DefaultRequestHeaders.Add(PostingApiFactory.UserHeader, owner.Id.ToString());
        using var edited = await client.PutAsJsonAsync(path, details);
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        using var incomplete = await client.PostAsync($"{path}/submit", null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, incomplete.StatusCode);
        using var unownedMedia = await client.PostAsJsonAsync($"{path}/media",
            new AddRentalPostMediaDto(MediaType.Image, "cloudinary",
                $"https://res.cloudinary.com/homeji-qa/image/upload/rental-posts/{other.Id:D}/{draft.Id:D}/wrong.jpg", true, 0));
        Assert.Equal(HttpStatusCode.BadRequest, unownedMedia.StatusCode);

        for (var index = 0; index < RentalPost.MinimumImageCountForSubmit; index++)
        {
            var url = $"https://res.cloudinary.com/homeji-qa/image/upload/rental-posts/{owner.Id:D}/{draft.Id:D}/image-{index}.jpg";
            using var media = await client.PostAsJsonAsync($"{path}/media",
                new AddRentalPostMediaDto(MediaType.Image, "cloudinary", url, index == 0, index));
            Assert.Equal(HttpStatusCode.OK, media.StatusCode);
        }
        using var latest = await client.PutAsJsonAsync(path, details with { Title = "Nội dung mới nhất qua HTTP" });
        Assert.Equal(HttpStatusCode.OK, latest.StatusCode);
        using var submitted = await client.PostAsync($"{path}/submit", null);
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        var result = await submitted.Content.ReadFromJsonAsync<RentalPostDto>();
        Assert.NotNull(result);
        Assert.Equal(RentalPostStatus.Pending, result.Status);
        Assert.Equal("Nội dung mới nhất qua HTTP", result.Title);
        Assert.Equal(3, result.Media.Count);
        db.ChangeTracker.Clear();
        var persisted = await db.RentalPosts.Include(post => post.Media).SingleAsync(post => post.Id == draft.Id);
        Assert.Equal(result.Title, persisted.Title);
        Assert.Equal(RentalPostStatus.Pending, persisted.Status);
        Assert.Equal(3, persisted.Media.Count);
        Assert.Equal(10.87m, persisted.Latitude);
        Assert.Equal(106.80m, persisted.Longitude);
    }

    // In-process TestServer only. This scheme is never registered by production code.
    private sealed class PostingApiFactory(ApplicationDbContext database) : WebApplicationFactory<Program>
    {
        public const string UserHeader = "X-Homeji-QA-User";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:DefaultConnection", database.Database.GetConnectionString());
            builder.UseSetting("Database:ApplyMigrationsOnStartup", "false");
            builder.UseSetting("BackgroundJobs:Enabled", "false");
            builder.UseSetting("Api:EnableOpenApi", "false");
            builder.UseSetting("Api:UseHsts", "false");
            builder.UseSetting("Supabase:ProjectUrl", "https://test-project.supabase.co");
            builder.UseSetting("Supabase:Audience", "authenticated");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ApplicationDbContext>();
                services.AddScoped(_ =>
                {
                    var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                        .UseNpgsql(database.Database.GetDbConnection()).Options);
                    context.Database.UseTransaction(database.Database.CurrentTransaction!.GetDbTransaction());
                    return context;
                });
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "Homeji-QA";
                    options.DefaultChallengeScheme = "Homeji-QA";
                }).AddScheme<AuthenticationSchemeOptions, QaAuthenticationHandler>("Homeji-QA", _ => { });
            });
        }
    }

    private sealed class QaAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Guid.TryParse(Request.Headers[PostingApiFactory.UserHeader], out var userId))
                return Task.FromResult(AuthenticateResult.NoResult());
            var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", userId.ToString())], Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
}
