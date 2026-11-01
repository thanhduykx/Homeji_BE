using Homeji.Application.Abstractions.Authentication;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Admin;
using Homeji.Application.DTOs.Emails;
using Homeji.Application.IServices.Emails;
using Homeji.Application.Services.Admin;
using Homeji.Application.Services.Common;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.Context;
using Homeji.Infrastructure.External;
using Homeji.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Homeji.Api.IntegrationTests;

public sealed class AdminReportingDatabaseTests
{
    [LocalMarketplaceDatabaseFact]
    public async Task AccountsIncludeUnfinishedRegistrationsAndMembersRespectStatusAndPagination()
    {
        await using var db = Open();
        var user = await Seed(db, UserRole.Admin);
        var incomplete = Guid.NewGuid(); var email = incomplete.ToString("N") + "@example.invalid";
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO auth.users(id,email) VALUES ({incomplete},{email})");
        db.UserSubscriptions.AddRange(UserSubscription.CreatePremium(user, "test-active", "Gói thử đang dùng", null, DateTimeOffset.UtcNow.AddDays(-1), 30, DateTimeOffset.UtcNow),
            UserSubscription.CreatePremium(user, "test-expired", "Gói thử cũ", null, DateTimeOffset.UtcNow.AddDays(-60), 30, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        var repository = new AdminReportingRepository(db, TimeProvider.System);
        var accounts = await repository.GetAccountsAsync(email, 1, 25, default);
        Assert.Single(accounts.Items); Assert.False(accounts.Items[0].ProfileCompleted); Assert.Null(accounts.Items[0].Role);
        var active = await repository.GetMembersAsync(user.ToString("N"), "active", 1, 1, default);
        Assert.Single(active.Items); Assert.Equal("active", active.Items[0].State); Assert.Null(active.Items[0].PaymentStatus);
        Assert.Single((await repository.GetMembersAsync(user.ToString("N"), "expired", 1, 25, default)).Items);
        Assert.Empty((await repository.GetMembersAsync(user.ToString("N"), "active", 2, 1, default)).Items);
    }
    [LocalMarketplaceDatabaseFact]
    public async Task OnlyAdminsReadAccountsGenerateOrDownloadReportsAndRetriesReuseFile()
    {
        await using var db = Open();
        var admin = await Seed(db, UserRole.Admin); var renter = await Seed(db, UserRole.Renter);
        var denied = Service(db, renter);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => denied.GetAccountsAsync(null, 1, 25));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => denied.GetMembersAsync(null, "all", 1, 25));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => denied.GetExportsAsync());
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => denied.DownloadAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => denied.CreateAsync(new CreateAdminReportDto(Guid.NewGuid(), 30)));
        var service = Service(db, admin); var request = new CreateAdminReportDto(Guid.NewGuid(), 30);
        var first = await service.CreateAsync(request); var retry = await service.CreateAsync(request);
        Assert.Equal(first.Id, retry.Id); Assert.Equal("queued", first.EmailStatus); Assert.Equal("duongduy12314@gmail.com", first.Recipient);
        Assert.Single(await db.AdminReportExports.Where(row => row.RequestedById == admin && row.RequestId == request.RequestId).ToArrayAsync());
        Assert.NotEmpty((await service.DownloadAsync(first.Id)).Content);
        await Assert.ThrowsAsync<RequestValidationException>(() => service.CreateAsync(request with { Days = 7 }));
        Assert.True(await db.Database.SqlQueryRaw<bool>("SELECT relrowsecurity AS \"Value\" FROM pg_class WHERE oid='homeji.admin_report_exports'::regclass").SingleAsync());
    }
    [LocalMarketplaceDatabaseFact]
    public async Task EmailDispatcherUsesStoredAttachmentAndDoesNotRetryUncertainSends()
    {
        await using var db = Open(); var admin = await Seed(db, UserRole.Admin);
        var row = new AdminReportExport(admin, Guid.NewGuid(), 30, "fixture@example.invalid", [1, 2, 3], DateTimeOffset.UtcNow.AddDays(-1));
        db.AdminReportExports.Add(row); await db.SaveChangesAsync();
        var sender = new Sender(row.Id, new EmailSendResultDto(false, "fixture", true));
        var dispatcher = new AdminReportEmailDispatcher(db, sender, TimeProvider.System);
        for (var index = 0; index < 3; index++) await dispatcher.DispatchBatchAsync(default);
        db.ChangeTracker.Clear();
        var saved = await db.AdminReportExports.SingleAsync(report => report.Id == row.Id);
        Assert.Equal("uncertain", saved.EmailStatus); Assert.Null(saved.EmailAcceptedAt); Assert.Equal(1, saved.Attempts);
        Assert.Equal(Convert.ToBase64String(row.Content), sender.Attachment);
        Assert.Equal(1, sender.TargetCalls);
    }
    private static ApplicationDbContext Open()
    {
        var connection = Environment.GetEnvironmentVariable("HOMEJI_TEST_DATABASE") ?? throw new InvalidOperationException("Local test database required.");
        var parsed = new NpgsqlConnectionStringBuilder(connection);
        if (parsed.Host != "127.0.0.1" || parsed.Database != "homeji_quality") throw new InvalidOperationException("Disposable loopback database only.");
        return new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
    }
    private static async Task<Guid> Seed(ApplicationDbContext db, UserRole role)
    {
        var id = Guid.NewGuid(); var email = id.ToString("N") + "@example.invalid";
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO auth.users(id,email,email_confirmed_at) VALUES ({id},{email},now())");
        var profile = UserProfile.Create(id, "Fixture " + id.ToString("N"), DateTimeOffset.UtcNow); profile.SetRole(role, DateTimeOffset.UtcNow);
        db.UserProfiles.Add(profile); await db.SaveChangesAsync(); return id;
    }
    private static AdminReportingService Service(ApplicationDbContext db, Guid id)
    {
        var context = new UserContext(new CurrentUser(id), new UserProfileRepository(db));
        return new AdminReportingService(context, new AdminReportingRepository(db, TimeProvider.System),
            new AdminAnalyticsService(context, new AdminAnalyticsRepository(db), TimeProvider.System), new AdminReportWorkbookWriter(), Options.Create(new AdminReportOptions()), TimeProvider.System);
    }
    private sealed record CurrentUser(Guid? UserId) : ICurrentUser;
    private sealed class Sender(Guid target, EmailSendResultDto result) : ITransactionalEmailSender
    {
        public string? Attachment { get; private set; }
        public int TargetCalls { get; private set; }
        public Task<EmailSendResultDto> SendAsync(string email, string subject, string html, string text, Guid idempotencyKey, CancellationToken cancellationToken, IReadOnlyList<EmailAttachmentDto>? attachments = null)
        {
            if (idempotencyKey == target) { TargetCalls++; Attachment = attachments![0].Content; return Task.FromResult(result); }
            return Task.FromResult(new EmailSendResultDto(true, "fixture accepted"));
        }
    }
}
