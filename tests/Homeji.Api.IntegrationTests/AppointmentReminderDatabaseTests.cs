using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
﻿using System.Collections.Concurrent;
using Homeji.Application.Abstractions.Authentication;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Appointments;
using Homeji.Application.DTOs.Emails;
using Homeji.Application.IServices.Emails;
using Homeji.Application.Services.Appointments;
using Homeji.Application.Services.Common;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.Context;
using Homeji.Infrastructure.External;
using Homeji.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Homeji.Api.IntegrationTests;

public sealed class AppointmentReminderDatabaseTests
{
    private static ApplicationDbContext Open()
    {
        var connection = Environment.GetEnvironmentVariable("HOMEJI_TEST_DATABASE")!;
        var settings = new NpgsqlConnectionStringBuilder(connection);
        if (settings.Host != "127.0.0.1" || settings.Database != "homeji_quality")
            throw new InvalidOperationException("Disposable local database required.");
        return new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed record CurrentUser(Guid? UserId) : ICurrentUser;
    private sealed class Sender : ITransactionalEmailSender
    {
        public ConcurrentBag<Guid> Keys { get; } = [];
        public ConcurrentBag<string> Recipients { get; } = [];
        public EmailSendResultDto Result { get; set; } = new(true, "accepted");
        public Task<EmailSendResultDto> SendAsync(string email, string subject, string html, string text,
            Guid idempotencyKey, CancellationToken cancellationToken, IReadOnlyList<EmailAttachmentDto>? attachments = null)
        {
            Keys.Add(idempotencyKey);
            Recipients.Add(email);
            Assert.Contains("UTC+7", text, StringComparison.Ordinal);
            Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
            Assert.Equal(AppointmentReminderEmail.Subject, subject);
            return Task.FromResult(Result);
        }
    }

    private static async Task<ViewingAppointment> SeedAsync(ApplicationDbContext db, Clock clock, bool verified = true)
    {
        var renter = Guid.NewGuid();
        var owner = Guid.NewGuid();
        DateTimeOffset? confirmedAt = verified ? clock.Now : null;
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO auth.users(id,email,email_confirmed_at) VALUES ({renter},{"reminder-" + renter + "@example.com"},{confirmedAt})");
        var post = RentalPost.CreateDraft(owner, RentalPostType.VacantRoom, clock.Now.AddDays(-2));
        post.UpdateDetails(RentalPostType.VacantRoom, "Phòng <script>test</script>", "Phòng kiểm thử", 3000000, 1000000, 20, "Thủ Đức", 10.8m, 106.7m, [], clock.Now);
        for (var i = 0; i < 3; i++) post.AddMedia(MediaType.Image, "rentals", $"test/{Guid.NewGuid()}.jpg", i == 0, i, clock.Now);
        post.Submit(clock.Now);
        post.Approve(clock.Now);
        db.RentalPosts.Add(post);
        var item = new ViewingAppointment(post.Id, renter, owner, clock.Now.AddHours(2), null, clock.Now);
        db.ViewingAppointments.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    private static ViewingAppointmentService Service(ApplicationDbContext db, Guid user, Clock clock) => new(
        new UserContext(new CurrentUser(user), null!), new ViewingAppointmentRepository(db),
        new RentalPostRepository(db), null!, null!, clock);

    [LocalMarketplaceDatabaseFact]
    public async Task Only_requester_can_set_or_cancel_and_verified_email_is_required()
    {
        await using var db = Open();
        var clock = new Clock();
        var item = await SeedAsync(db, clock);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Service(db, item.OwnerId, clock).SetReminderAsync(item.Id, new(30)));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Service(db, Guid.NewGuid(), clock).SetReminderAsync(item.Id, new(null)));
        var service = Service(db, item.RequesterId, clock);
        var saved = await service.SetReminderAsync(item.Id, new(30));
        Assert.Equal(30, saved.ReminderMinutesBefore);
        Assert.NotNull((await service.GetMineAsync()).Single().ReminderDueAt);
        Assert.Null((await service.SetReminderAsync(item.Id, new(null))).ReminderDueAt);
        var unverified = await SeedAsync(db, clock, false);
        await Assert.ThrowsAsync<RequestValidationException>(() => Service(db, unverified.RequesterId, clock).SetReminderAsync(unverified.Id, new(30)));
    }

    [LocalMarketplaceDatabaseFact]
    public async Task Due_jobs_survive_new_contexts_and_concurrent_workers_send_once()
    {
        var clock = new Clock();
        var ids = new List<Guid>();
        await using (var setup = Open())
        {
            for (var i = 0; i < 6; i++)
            {
                var item = await SeedAsync(setup, clock);
                item.SetEmailReminder(30, clock.Now);
                ids.Add(item.Id);
            }
            await setup.SaveChangesAsync();
        }
        var sender = new Sender();
        await using var first = Open();
        await using var second = Open();
        await new AppointmentReminderDispatcher(first, sender, clock).DispatchBatchAsync(CancellationToken.None);
        Assert.Empty(sender.Keys);
        clock.Now = clock.Now.AddMinutes(91);
        await Task.WhenAll(new AppointmentReminderDispatcher(first, sender, clock).DispatchBatchAsync(CancellationToken.None),
            new AppointmentReminderDispatcher(second, sender, clock).DispatchBatchAsync(CancellationToken.None));
        await new AppointmentReminderDispatcher(first, sender, clock).DispatchBatchAsync(CancellationToken.None);
        Assert.Equal(6, sender.Keys.Count);
        Assert.Equal(6, sender.Keys.Distinct().Count());
        Assert.Equal(6, sender.Recipients.Distinct().Count());
        Assert.Equal(6, await first.ViewingAppointments.CountAsync(x => ids.Contains(x.Id) && x.ReminderSentAt != null));
    }

    [LocalMarketplaceDatabaseFact]
    public async Task Cancelled_rescheduled_expired_and_unavailable_posts_do_not_send()
    {
        await using var db = Open();
        var clock = new Clock();
        var cancelled = await SeedAsync(db, clock);
        var rescheduled = await SeedAsync(db, clock);
        var expired = await SeedAsync(db, clock);
        var archived = await SeedAsync(db, clock);
        foreach (var item in new[] { cancelled, rescheduled, expired, archived }) item.SetEmailReminder(30, clock.Now);
        cancelled.Cancel(clock.Now);
        rescheduled.Reschedule(clock.Now.AddDays(1), clock.Now);
        (await db.RentalPosts.SingleAsync(x => x.Id == archived.RentalPostId)).Archive(clock.Now);
        await db.SaveChangesAsync();
        clock.Now = clock.Now.AddMinutes(91);
        await db.ViewingAppointments.Where(x => x.Id == expired.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.ScheduledAt, clock.Now.AddMinutes(-1)));
        var sender = new Sender();
        await new AppointmentReminderDispatcher(db, sender, clock).DispatchBatchAsync(CancellationToken.None);
        Assert.Empty(sender.Keys);
    }

    [LocalMarketplaceDatabaseFact]
    public async Task Known_failure_retries_but_uncertain_delivery_and_expired_claim_never_resend()
    {
        await using var db = Open();
        var clock = new Clock();
        var item = await SeedAsync(db, clock);
        item.SetEmailReminder(30, clock.Now);
        await db.SaveChangesAsync();
        clock.Now = clock.Now.AddMinutes(91);
        var sender = new Sender { Result = new(false, "known rejection") };
        var dispatcher = new AppointmentReminderDispatcher(db, sender, clock);
        await dispatcher.DispatchBatchAsync(CancellationToken.None);
        await dispatcher.DispatchBatchAsync(CancellationToken.None);
        Assert.Single(sender.Keys);
        clock.Now = clock.Now.AddMinutes(2);
        sender.Result = new(false, "uncertain", true);
        await dispatcher.DispatchBatchAsync(CancellationToken.None);
        clock.Now = clock.Now.AddMinutes(2);
        await dispatcher.DispatchBatchAsync(CancellationToken.None);
        Assert.Equal(2, sender.Keys.Count);
        Assert.Single(sender.Keys.Distinct());
        db.ChangeTracker.Clear();
        var failed = await db.ViewingAppointments.SingleAsync(x => x.Id == item.Id);
        Assert.Equal(5, failed.ReminderAttempts);
        Assert.True((await Service(db, item.RequesterId, clock).GetMineAsync()).Single().ReminderFailed);
        var crashed = await SeedAsync(db, clock);
        crashed.SetEmailReminder(30, clock.Now);
        await db.SaveChangesAsync();
        clock.Now = clock.Now.AddMinutes(91);
        await db.ViewingAppointments.Where(x => x.Id == crashed.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.ReminderLeaseUntil, clock.Now.AddMinutes(-1)));
        await dispatcher.DispatchBatchAsync(CancellationToken.None);
        Assert.Equal(2, sender.Keys.Count);
    }

    [LocalMarketplaceDatabaseFact]
    public async Task Http_endpoint_persists_reminder_and_enforces_authentication_and_ownership()
    {
        await using var db = Open();
        var clock = new Clock();
        var item = await SeedAsync(db, clock);
        using var factory = new HomejiApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:DefaultConnection", Environment.GetEnvironmentVariable("HOMEJI_TEST_DATABASE"));
            builder.UseSetting("BackgroundJobs:Enabled", "false");
            builder.UseSetting("Database:ApplyMigrationsOnStartup", "false");
            builder.ConfigureTestServices(services => services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "ReminderTest";
                options.DefaultChallengeScheme = "ReminderTest";
            }).AddScheme<AuthenticationSchemeOptions, ReminderAuthenticationHandler>("ReminderTest", _ => { }));
        });
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var endpoint = new Uri($"/api/viewing-appointments/{item.Id}/reminder", UriKind.Relative);
        using var anonymous = await client.PutAsJsonAsync(endpoint, new SetViewingReminderDto(30));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        client.DefaultRequestHeaders.Add("X-Test-User", item.OwnerId.ToString());
        using var owner = await client.PutAsJsonAsync(endpoint, new SetViewingReminderDto(30));
        Assert.Equal(HttpStatusCode.Forbidden, owner.StatusCode);
        client.DefaultRequestHeaders.Remove("X-Test-User");
        client.DefaultRequestHeaders.Add("X-Test-User", item.RequesterId.ToString());
        using var saved = await client.PutAsJsonAsync(endpoint, new SetViewingReminderDto(30));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal(30, (await saved.Content.ReadFromJsonAsync<ViewingAppointmentDto>())!.ReminderMinutesBefore);
        using var invalid = await client.PutAsJsonAsync(endpoint, new SetViewingReminderDto(2));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
        using var cleared = await client.PutAsJsonAsync(endpoint, new SetViewingReminderDto(null));
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        Assert.Null((await cleared.Content.ReadFromJsonAsync<ViewingAppointmentDto>())!.ReminderDueAt);
    }

    private sealed class ReminderAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Guid.TryParse(Request.Headers["X-Test-User"], out var id))
                return Task.FromResult(AuthenticateResult.NoResult());
            var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", id.ToString())], Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }

    [Fact]
    public void Email_escapes_listing_and_uses_Vietnam_time()
    {
        var now = new DateTimeOffset(2026, 10, 30, 1, 0, 0, TimeSpan.Zero);
        var post = RentalPost.CreateDraft(Guid.NewGuid(), RentalPostType.VacantRoom, now);
        post.UpdateDetails(RentalPostType.VacantRoom, "<script>x</script>", "test", 100, 0, 10, "<b>Address</b>", 10, 106, [], now);
        var item = new ViewingAppointment(post.Id, Guid.NewGuid(), post.OwnerId, now.AddHours(2), null, now);
        var content = AppointmentReminderEmail.Render(item, post);
        Assert.Contains("10:00", content.Text, StringComparison.Ordinal);
        Assert.Contains("30/10/2026", content.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", content.Html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", content.Html, StringComparison.Ordinal);
        Assert.Contains("chờ chủ trọ xác nhận", content.Text, StringComparison.Ordinal);
    }
}
