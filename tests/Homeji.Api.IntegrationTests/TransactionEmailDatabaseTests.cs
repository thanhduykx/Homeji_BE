using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Homeji.Application.Abstractions.Authentication;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Accounts;
using Homeji.Application.DTOs.Emails;
using Homeji.Application.IRepositories.Accounts;
using Homeji.Application.IServices.Emails;
using Homeji.Application.Services.Common;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.Context;
using Homeji.Infrastructure.External;
using Homeji.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Homeji.Application.DTOs.Payments;
using Homeji.Application.DTOs.Wallets;
using Homeji.Application.Services.Wallets;
using Homeji.Application.Services.Subscriptions;
using System.Security.Cryptography;
using System.Text;

namespace Homeji.Api.IntegrationTests;

public sealed class TransactionEmailDatabaseTests
{
    [LocalMarketplaceDatabaseFact]
    public async Task Paid_webhook_and_withdrawal_lifecycle_record_receipts_with_the_actual_wallet_changes()
    {
        await using var db = Open();
        var user = Guid.NewGuid();
        var admin = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO auth.users(id) VALUES ({user}),({admin})");
        db.UserProfiles.Add(UserProfile.Create(user, "Wallet Test", DateTimeOffset.UtcNow));
        var adminProfile = UserProfile.Create(admin, "Admin Test", DateTimeOffset.UtcNow);
        adminProfile.SetRole(UserRole.Admin, DateTimeOffset.UtcNow);
        db.UserProfiles.Add(adminProfile);
        var order = RandomNumberGenerator.GetInt32(100000000, 2000000000);
        var payment = new PaymentTransaction(user, PaymentMethod.PayOs, 100000,
            order.ToString(System.Globalization.CultureInfo.InvariantCulture), "Nạp ví", DateTimeOffset.UtcNow, PaymentPurpose.WalletTopUp);
        db.PaymentTransactions.Add(payment);
        await db.SaveChangesAsync();
        var receipts = Receipts(db, user);
        var wallets = new WalletRepository(db);
        using var http = new HttpClient();
        var paymentService = new PaymentService(http, new UserContext(new CurrentUser(user), new UserProfileRepository(db)),
            new PaymentTransactionRepository(db), null!, wallets, Options.Create(new MomoOptions()),
            Options.Create(new PayOsOptions { ClientId = "test", ApiKey = "test", ChecksumKey = "test", ReturnUrl = "https://homeji.example/return", CancelUrl = "https://homeji.example/cancel" }),
            Options.Create(new PremiumSubscriptionOptions()), TimeProvider.System, receipts);
        var data = new PayOsWebhookDataDto(order, 100000, "Nạp ví", null, "bank-test-reference", null, "VND", null, "00", "Success", null, null, null, null, null, null);
        var raw = $"accountNumber=&amount=100000&code=00&counterAccountBankId=&counterAccountBankName=&counterAccountName=&counterAccountNumber=&currency=VND&desc=Success&description=Nạp ví&orderCode={order}&paymentLinkId=&reference=bank-test-reference&transactionDateTime=&virtualAccountName=&virtualAccountNumber=";
        var signature = Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes("test"), Encoding.UTF8.GetBytes(raw)));
        var webhook = new PayOsWebhookDto("00", "Success", true, data, signature);
        await paymentService.HandlePayOsWebhookAsync(webhook);
        await paymentService.HandlePayOsWebhookAsync(webhook);
        Assert.Equal(100000, (await wallets.GetAsync(user, CancellationToken.None))!.Balance);
        Assert.Single((await receipts.GetMineAsync(1, 20, CancellationToken.None)).Items);

        var withdrawals = new WalletWithdrawalRepository(db);
        var requester = new WalletWithdrawalService(new UserContext(new CurrentUser(user), new UserProfileRepository(db)), wallets, withdrawals, TimeProvider.System, receipts);
        var requested = await requester.CreateAsync(new CreateWalletWithdrawalDto(20000, "Test Bank", "1234567890", "TEST USER", Guid.NewGuid()));
        Assert.Equal(80000, (await wallets.GetAsync(user, CancellationToken.None))!.Balance);
        var reviewer = new WalletWithdrawalService(new UserContext(new CurrentUser(admin), new UserProfileRepository(db)), wallets, withdrawals, TimeProvider.System, receipts);
        await reviewer.RejectAsync(requested.Id, new ReviewWalletWithdrawalDto("Test refund"));
        Assert.Equal(100000, (await wallets.GetAsync(user, CancellationToken.None))!.Balance);
        var second = await requester.CreateAsync(new CreateWalletWithdrawalDto(20000, "Test Bank", "1234567890", "TEST USER", Guid.NewGuid()));
        await reviewer.CompleteAsync(second.Id, new ReviewWalletWithdrawalDto("Test transfer reference"));
        var history = (await receipts.GetMineAsync(1, 20, CancellationToken.None)).Items;
        Assert.Equal(5, history.Count);
        Assert.Contains(history, x => x.Event == ReceiptEvent.WithdrawalRejected);
        Assert.Contains(history, x => x.Event == ReceiptEvent.WithdrawalCompleted);
        Assert.DoesNotContain(history, x => x.Description.Contains("1234567890", StringComparison.Ordinal));
        Assert.Equal(80000, (await wallets.GetAsync(user, CancellationToken.None))!.Balance);
        await Assert.ThrowsAsync<Homeji.Domain.Exceptions.DomainException>(() => reviewer.CompleteAsync(second.Id, new ReviewWalletWithdrawalDto("Duplicate")));
        Assert.Equal(5, await db.TransactionReceipts.CountAsync(x => x.UserId == user));
    }

    [LocalMarketplaceDatabaseFact]
    public async Task Receipt_and_outbox_rollback_together_and_duplicate_callbacks_enqueue_once()
    {
        await using var db = Open();
        var user = Guid.NewGuid();
        var service = Receipts(db, user);
        var payment = Paid(user);
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            db.PaymentTransactions.Add(payment);
            await service.RecordPaymentAsync(payment, CancellationToken.None);
            await db.SaveChangesAsync();
            await transaction.RollbackAsync();
        }
        db.ChangeTracker.Clear();
        Assert.False(await db.TransactionReceipts.AnyAsync(x => x.ReferenceId == payment.Id));
        Assert.False(await db.PaymentTransactions.AnyAsync(x => x.Id == payment.Id));
        await service.RecordPaymentAsync(payment, CancellationToken.None);
        await service.RecordPaymentAsync(payment, CancellationToken.None);
        await db.SaveChangesAsync();
        await service.RecordPaymentAsync(payment, CancellationToken.None);
        await db.SaveChangesAsync();
        var receipt = Assert.Single((await service.GetMineAsync(1, 20, CancellationToken.None)).Items);
        Assert.Equal(ReceiptEvent.DepositSucceeded, receipt.Event);
        Assert.Equal(1, await db.ReceiptEmailDeliveries.CountAsync(x => x.ReceiptId == receipt.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => Receipts(db, Guid.NewGuid()).GetMineByIdAsync(receipt.Id, CancellationToken.None));
        await Assert.ThrowsAsync<RequestValidationException>(() => service.GetMineAsync(0, 20, CancellationToken.None));
    }

    [LocalMarketplaceDatabaseFact]
    public async Task Concurrent_workers_claim_distinct_jobs_and_uncertain_results_do_not_retry()
    {
        await using var setup = Open();
        await setup.Database.ExecuteSqlRawAsync("ALTER TABLE auth.users ADD COLUMN IF NOT EXISTS email text; ALTER TABLE auth.users ADD COLUMN IF NOT EXISTS email_confirmed_at timestamptz;");
        var user = Guid.NewGuid();
        await setup.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO auth.users(id,email,email_confirmed_at) VALUES ({user},{"receipts@example.com"},{DateTimeOffset.UtcNow})");
        var service = Receipts(setup, user);
        var referenceIds = new List<Guid>();
        for (var i = 0; i < 10; i++)
        {
            var payment = Paid(user);
            referenceIds.Add(payment.Id);
            await service.RecordPaymentAsync(payment, CancellationToken.None);
        }
        await setup.SaveChangesAsync();
        var keys = await setup.ReceiptEmailDeliveries.Where(x => setup.TransactionReceipts.Any(r => r.Id == x.ReceiptId && r.UserId == user)).Select(x => x.Id).ToArrayAsync();
        // Prioritize these ten jobs without changing unrelated test data.
        await setup.ReceiptEmailDeliveries.Where(x => keys.Contains(x.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.NextAttemptAt, DateTimeOffset.UnixEpoch));
        var sender = new Sender();
        await using var first = Open();
        await using var second = Open();
        await Task.WhenAll(Dispatcher(first, sender).DispatchBatchAsync(CancellationToken.None), Dispatcher(second, sender).DispatchBatchAsync(CancellationToken.None));
        Assert.Equal(10, sender.Keys.Count);
        Assert.Equal(10, sender.Keys.Distinct().Count());
        Assert.Equal(10, await setup.ReceiptEmailDeliveries.CountAsync(x => keys.Contains(x.Id) && x.SentAt != null));

        var uncertainPayment = Paid(user);
        await service.RecordPaymentAsync(uncertainPayment, CancellationToken.None);
        await setup.SaveChangesAsync();
        await setup.ReceiptEmailDeliveries.Where(x => setup.TransactionReceipts.Any(r => r.Id == x.ReceiptId && r.ReferenceId == uncertainPayment.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.NextAttemptAt, DateTimeOffset.UnixEpoch));
        var uncertainSender = new Sender { Uncertain = true };
        await Dispatcher(first, uncertainSender).DispatchBatchAsync(CancellationToken.None);
        await Dispatcher(second, uncertainSender).DispatchBatchAsync(CancellationToken.None);
        Assert.Single(uncertainSender.Keys);
        var failedReceipt = Assert.Single((await service.GetMineAsync(1, 20, CancellationToken.None)).Items, x => x.ReferenceId == uncertainPayment.Id);
        Assert.True(failedReceipt.EmailFailed);
        Assert.Null(failedReceipt.EmailSentAt);
    }

    [LocalMarketplaceDatabaseFact]
    public async Task Signup_otp_enforces_cooldown_attempt_limit_and_purpose_without_changing_password_on_resend()
    {
        await using var db = Open();
        var clock = new Clock();
        using var handler = new AuthHandler();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO auth.users(id) VALUES ({handler.UserId})");
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://project.supabase.co/") };
        var sender = new Sender();
        var service = new SupabaseAccountService(client, Options.Create(new SupaBaseAuthOptions
        {
            ProjectUrl = "https://project.supabase.co", ApiKey = "test-public", ServiceRoleKey = "test-secret",
            RegistrationRedirectUrl = "https://homeji.example/auth/callback",
        }), null!, new NoExistingEmail(), NullLogger<SupabaseAccountService>.Instance,
            Options.Create(new EmailDeliveryOptions { Provider = "Brevo", RegistrationOtpEnabled = true }), db, sender, clock);
        var email = $"otp-{Guid.NewGuid():N}@example.com";
        var registered = await service.RegisterAsync(new RegisterAccountDto(email, "original-password", "Test User", null));
        Assert.Equal("otp", registered.ConfirmationMethod);
        Assert.Contains("123456", sender.LastHtml, StringComparison.Ordinal);
        await Assert.ThrowsAsync<RequestValidationException>(() => service.ResendSignupOtpAsync(new ResendSignupOtpDto(email)));
        for (var attempt = 0; attempt < 6; attempt++)
            await Assert.ThrowsAsync<RequestValidationException>(() => service.VerifySignupOtpAsync(new SignupOtpDto(email, "000000")));
        Assert.Equal(5, handler.Verifications);
        clock.Now = clock.Now.AddSeconds(61);
        await service.ResendSignupOtpAsync(new ResendSignupOtpDto(email));
        using var resend = JsonDocument.Parse(handler.LastGeneratedBody!);
        Assert.False(resend.RootElement.TryGetProperty("password", out _));
        Assert.False(resend.RootElement.TryGetProperty("data", out _));
        var session = await service.VerifySignupOtpAsync(new SignupOtpDto(email, "123456"));
        Assert.Equal("test-access-token", session.AccessToken);
        Assert.False(session.EmailConfirmationRequired);
        await Assert.ThrowsAsync<RequestValidationException>(() => service.VerifySignupOtpAsync(new SignupOtpDto(email, "123456")));
        Assert.False(await db.Set<SignupVerificationChallenge>().AnyAsync(x => x.Email == email));
        db.ChangeTracker.Clear();
        await service.RegisterAsync(new RegisterAccountDto(email, "original-password", "Test User", null));
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM auth.users WHERE id = {handler.UserId}");
        Assert.False(await db.Set<SignupVerificationChallenge>().AnyAsync(x => x.Email == email));
    }

    private static ApplicationDbContext Open()
    {
        var connection = Environment.GetEnvironmentVariable("HOMEJI_TEST_DATABASE")!;
        var settings = new NpgsqlConnectionStringBuilder(connection);
        if (settings.Host != "127.0.0.1" || settings.Database != "homeji_quality") throw new InvalidOperationException("Disposable local database required.");
        return new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
    }

    private static TransactionReceiptService Receipts(ApplicationDbContext db, Guid user) => new(db, new UserContext(new CurrentUser(user), null!));
    private static PaymentTransaction Paid(Guid user)
    {
        var payment = new PaymentTransaction(user, PaymentMethod.PayOs, 100000, Guid.NewGuid().ToString("N"), "Nạp ví", DateTimeOffset.UtcNow, PaymentPurpose.WalletTopUp);
        payment.MarkPaid("external", "paid", null, DateTimeOffset.UtcNow);
        return payment;
    }
    private static ReceiptEmailDispatcher Dispatcher(ApplicationDbContext db, Sender sender) =>
        new(db, sender, Options.Create(new ReceiptEmailOptions { Enabled = true }), TimeProvider.System);
    private sealed record CurrentUser(Guid? UserId) : ICurrentUser;
    private sealed class NoExistingEmail : IAccountEmailRepository
    {
        public Task<bool> ExistsAsync(string normalizedEmail, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Sender : ITransactionalEmailSender
    {
        public ConcurrentBag<Guid> Keys { get; } = [];
        public bool Uncertain { get; init; }
        public string LastHtml { get; private set; } = string.Empty;
        public Task<EmailSendResultDto> SendAsync(string email, string subject, string html, string text, Guid idempotencyKey, CancellationToken cancellationToken, IReadOnlyList<EmailAttachmentDto>? attachments = null)
        {
            if (attachments is not null)
            {
                Assert.Equal(2, attachments.Count);
                Assert.Contains(attachments, file => file.Name.EndsWith(".pdf", StringComparison.Ordinal));
                var logoFile = Assert.Single(attachments, file => file.Name == "Homeji-logo.png");
                Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, Convert.FromBase64String(logoFile.Content)[..8]);
                var pdfFile = Assert.Single(attachments, file => file.Name.EndsWith(".pdf", StringComparison.Ordinal));
                Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(Convert.FromBase64String(pdfFile.Content)), StringComparison.Ordinal);
            }
            Keys.Add(idempotencyKey);
            LastHtml = html;
            return Task.FromResult(new EmailSendResultDto(!Uncertain, "test", Uncertain));
        }
    }
    private sealed class AuthHandler : HttpMessageHandler
    {
        private readonly Guid _userId = Guid.NewGuid();
        public Guid UserId => _userId;
        private string? _email;
        public int Verifications { get; private set; }
        public string? LastGeneratedBody { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            using var payload = JsonDocument.Parse(body);
            var root = payload.RootElement;
            Assert.Equal("signup", root.GetProperty("type").GetString());
            _email = root.GetProperty("email").GetString();
            if (request.RequestUri!.AbsolutePath.EndsWith("generate_link", StringComparison.Ordinal))
            {
                LastGeneratedBody = body;
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new
                {
                    action_link = "https://project.supabase.co/auth/v1/verify?type=signup", email_otp = "123456",
                    id = _userId, email = _email,
                })) };
            }
            Verifications++;
            var valid = root.GetProperty("token").GetString() == "123456";
            return new HttpResponseMessage(valid ? HttpStatusCode.OK : HttpStatusCode.BadRequest)
            {
                Content = new StringContent(valid ? JsonSerializer.Serialize(new
                { access_token = "test-access-token", user = new { id = _userId, email = _email } }) : "{\"msg\":\"Invalid token\"}"),
            };
        }
    }
}
