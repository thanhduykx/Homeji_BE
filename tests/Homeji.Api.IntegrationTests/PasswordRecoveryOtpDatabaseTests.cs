using System.Net;
using System.Text.Json;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Accounts;
using Homeji.Application.DTOs.Emails;
using Homeji.Application.IServices.Emails;
using Homeji.Infrastructure.Context;
using Homeji.Infrastructure.External;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Homeji.Api.IntegrationTests;

public sealed class PasswordRecoveryOtpDatabaseTests
{
    [LocalMarketplaceDatabaseFact]
    public async Task Recovery_limits_attempts_resends_without_password_and_resets_only_after_recovery_verification()
    {
        var settings = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("HOMEJI_TEST_DATABASE"));
        if (settings.Host != "127.0.0.1" || settings.Database != "homeji_quality") throw new InvalidOperationException("Disposable database required.");
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(settings.ConnectionString).Options);
        var user = Guid.NewGuid();
        var email = $"recovery-{user:N}@example.com";
        var clock = new Clock();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO auth.users(id, email, email_confirmed_at) VALUES ({user}, {email}, {clock.Now})");
        using var handler = new RecoveryHandler(user, email);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://project.supabase.co/") };
        var sender = new Sender();
        var service = new SupabaseAccountService(http, Options.Create(new SupaBaseAuthOptions
        { ProjectUrl = "https://project.supabase.co", ApiKey = "public-test", ServiceRoleKey = "secret-test" }),
            null!, null!, NullLogger<SupabaseAccountService>.Instance,
            Options.Create(new EmailDeliveryOptions { Provider = "Brevo", RegistrationOtpEnabled = true }), db, sender, clock);

        var unknown = await service.RequestPasswordOtpAsync(new RequestPasswordOtpDto($"unknown-{user:N}@example.com"));
        var known = await service.RequestPasswordOtpAsync(new RequestPasswordOtpDto(email));
        Assert.Equal(unknown.Message, known.Message);
        Assert.Equal(1, handler.Generated);
        Assert.Equal(1, sender.Sent);
        await service.RequestPasswordOtpAsync(new RequestPasswordOtpDto(email));
        Assert.Equal(1, sender.Sent);
        for (var index = 0; index < 6; index++)
            await Assert.ThrowsAsync<RequestValidationException>(() => service.ResetPasswordWithOtpAsync(new ResetPasswordOtpDto(email, "000000", "new-password")));
        Assert.Equal(5, handler.Verified);
        Assert.Equal(0, handler.PasswordUpdates);
        clock.Now = clock.Now.AddSeconds(61);
        await service.RequestPasswordOtpAsync(new RequestPasswordOtpDto(email));
        Assert.Equal(2, sender.Sent);
        clock.Now = clock.Now.AddMinutes(11);
        await Assert.ThrowsAsync<RequestValidationException>(() => service.ResetPasswordWithOtpAsync(new ResetPasswordOtpDto(email, "123456", "new-password")));
        Assert.Equal(5, handler.Verified);
        await service.RequestPasswordOtpAsync(new RequestPasswordOtpDto(email));
        await service.ResetPasswordWithOtpAsync(new ResetPasswordOtpDto(email, "123456", "new-password"));
        Assert.Equal(1, handler.PasswordUpdates);
        Assert.False(await db.Set<PasswordRecoveryChallenge>().AnyAsync(x => x.Email == email));
        await Assert.ThrowsAsync<RequestValidationException>(() => service.ResetPasswordWithOtpAsync(new ResetPasswordOtpDto(email, "123456", "new-password")));
        await Assert.ThrowsAsync<RequestValidationException>(() => service.ResetPasswordWithOtpAsync(new ResetPasswordOtpDto(email, "123456", "short")));
        clock.Now = clock.Now.AddSeconds(61);
        await service.RequestPasswordOtpAsync(new RequestPasswordOtpDto(email));
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM auth.users WHERE id = {user}");
        Assert.False(await db.Set<PasswordRecoveryChallenge>().AnyAsync(x => x.Email == email));
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Sender : ITransactionalEmailSender
    {
        public int Sent { get; private set; }
        public Task<EmailSendResultDto> SendAsync(string email, string subject, string html, string text, Guid idempotencyKey, CancellationToken cancellationToken, IReadOnlyList<EmailAttachmentDto>? attachments = null)
        {
            Sent++;
            Assert.Contains("123456", html, StringComparison.Ordinal);
            Assert.Contains("đặt lại mật khẩu", subject, StringComparison.OrdinalIgnoreCase);
            return Task.FromResult(new EmailSendResultDto(true, "test"));
        }
    }
    private sealed class RecoveryHandler(Guid user, string email) : HttpMessageHandler
    {
        public int Generated { get; private set; }
        public int Verified { get; private set; }
        public int PasswordUpdates { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            var root = document.RootElement;
            if (request.Method == HttpMethod.Put)
            {
                Assert.Equal("recovery-only-access", request.Headers.Authorization?.Parameter);
                Assert.Equal("new-password", root.GetProperty("password").GetString());
                PasswordUpdates++;
                return new(HttpStatusCode.OK) { Content = new StringContent("{}") };
            }
            Assert.Equal("recovery", root.GetProperty("type").GetString());
            Assert.False(root.TryGetProperty("password", out _));
            Assert.False(root.TryGetProperty("data", out _));
            if (request.RequestUri!.AbsolutePath.EndsWith("generate_link", StringComparison.Ordinal))
            {
                Generated++;
                return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new
                { id = user, email, email_otp = "123456", action_link = "https://project.supabase.co/auth/v1/verify?type=recovery" })) };
            }
            Verified++;
            var valid = root.GetProperty("token").GetString() == "123456";
            return new(valid ? HttpStatusCode.OK : HttpStatusCode.BadRequest)
            { Content = new StringContent(valid ? JsonSerializer.Serialize(new { access_token = "recovery-only-access", user = new { id = user, email } }) : "{\"msg\":\"Invalid token\"}") };
        }
    }
}
