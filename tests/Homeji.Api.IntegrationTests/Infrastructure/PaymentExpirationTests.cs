using Homeji.Application.Abstractions.Authentication;
using Homeji.Application.IRepositories.Payments;
using Homeji.Application.Services.Common;
using Homeji.Application.Services.Subscriptions;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.External;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Api.IntegrationTests.Infrastructure;

public sealed class PaymentExpirationTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Reading_an_unpaid_order_after_fifteen_minutes_returns_cancelled(int method)
    {
        var createdAt = DateTimeOffset.Parse("2026-10-03T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var payment = new PaymentTransaction(Guid.NewGuid(), (PaymentMethod)method, 100_000, "order", "Premium", createdAt);
        var repository = new MemoryPayments(payment);
        using var client = new HttpClient();
        var service = new PaymentService(client,
            new UserContext(new CurrentUser(payment.UserId), null!), repository, null!, null!,
            Options.Create(new MomoOptions()), Options.Create(new PayOsOptions()),
            Options.Create(new PremiumSubscriptionOptions()), new FixedTime(createdAt.AddMinutes(16)));

        var result = await service.GetPaymentByIdAsync(payment.Id);

        Assert.Equal(PaymentStatus.Cancelled, result.Status);
        Assert.Equal(createdAt.AddMinutes(15), result.ExpiresAt);
    }

    [Theory]
    [InlineData(899, PaymentStatus.Pending)]
    [InlineData(900, PaymentStatus.Cancelled)]
    [InlineData(901, PaymentStatus.Cancelled)]
    public async Task History_and_order_lookup_apply_the_same_deadline(int ageSeconds, PaymentStatus expected)
    {
        var createdAt = DateTimeOffset.UnixEpoch;
        var payment = new PaymentTransaction(Guid.NewGuid(), PaymentMethod.Momo, 100_000, "order", "Premium", createdAt);
        var repository = new MemoryPayments(payment);
        using var client = new HttpClient();
        var service = new PaymentService(client,
            new UserContext(new CurrentUser(payment.UserId), null!), repository, null!, null!,
            Options.Create(new MomoOptions()), Options.Create(new PayOsOptions()),
            Options.Create(new PremiumSubscriptionOptions()), new FixedTime(createdAt.AddSeconds(ageSeconds)));

        var history = await service.GetMyPaymentHistoryAsync(expected, 30);
        Assert.Equal(expected, Assert.Single(history).Status);
        Assert.Equal(expected, (await service.GetPaymentByOrderCodeAsync(payment.OrderCode)).Status);
        Assert.Equal(0, await repository.CancelOverdueAsync(createdAt.AddSeconds(ageSeconds)));
    }

    [Fact]
    public void Expiration_preserves_paid_and_failed_payments()
    {
        var now = DateTimeOffset.UnixEpoch;
        var paid = new PaymentTransaction(Guid.NewGuid(), PaymentMethod.Momo, 100_000, "paid", "Premium", now);
        paid.MarkPaid("receipt", "success", null, now.AddMinutes(1));
        Assert.False(paid.CancelIfOverdue(now.AddMinutes(20)));
        Assert.Equal(PaymentStatus.Paid, paid.Status);

        var failed = new PaymentTransaction(Guid.NewGuid(), PaymentMethod.PayOs, 100_000, "failed", "Premium", now);
        failed.MarkFailed("declined", null, now.AddMinutes(1));
        Assert.False(failed.CancelIfOverdue(now.AddMinutes(20)));
        Assert.Equal(PaymentStatus.Failed, failed.Status);
    }

    [Fact]
    public async Task User_scoped_expiration_does_not_change_another_users_order()
    {
        var payment = new PaymentTransaction(Guid.NewGuid(), PaymentMethod.Momo, 100_000, "other", "Premium", DateTimeOffset.UnixEpoch);
        var repository = new MemoryPayments(payment);
        Assert.Equal(0, await repository.CancelOverdueAsync(DateTimeOffset.UnixEpoch.AddMinutes(20), Guid.NewGuid()));
        Assert.Equal(PaymentStatus.Pending, payment.Status);
    }

    [Fact]
    public void A_verified_success_after_expiration_still_records_received_money()
    {
        var now = DateTimeOffset.UnixEpoch;
        var payment = new PaymentTransaction(Guid.NewGuid(), PaymentMethod.Momo, 100_000, "late", "Premium", now);
        Assert.True(payment.CancelIfOverdue(now.AddMinutes(15)));
        payment.MarkFailed("delayed failure", null, now.AddMinutes(16));
        Assert.Equal(PaymentStatus.Cancelled, payment.Status);
        payment.MarkPaid("receipt", "verified success", null, now.AddMinutes(17));
        Assert.Equal(PaymentStatus.Paid, payment.Status);
        Assert.Equal("receipt", payment.ExternalTransactionId);
    }

    [Fact]
    public async Task Creating_a_payos_link_sets_the_provider_deadline_to_fifteen_minutes()
    {
        var now = DateTimeOffset.UnixEpoch.AddYears(56);
        var owner = Guid.NewGuid();
        var repository = new MemoryPayments(new PaymentTransaction(owner, PaymentMethod.PayOs, 99_000, "existing", "Premium", now));
        using var handler = new PayOsHandler();
        using var client = new HttpClient(handler);
        var service = new PaymentService(client,
            new UserContext(new CurrentUser(owner), null!), repository, null!, null!,
            Options.Create(new MomoOptions()), Options.Create(new PayOsOptions
            {
                ClientId = "test-client", ApiKey = "test-key", ChecksumKey = "test-checksum",
                ReturnUrl = "https://example.test/return", CancelUrl = "https://example.test/cancel",
            }), Options.Create(new PremiumSubscriptionOptions()), new FixedTime(now));

        var result = await service.CreatePremiumPayOsPaymentAsync("PREMIUM_MONTHLY");

        Assert.Equal(now.AddMinutes(15).ToUnixTimeSeconds(), handler.ExpiresAt);
        Assert.Equal(PaymentStatus.Pending, result.Status);
    }

    [Fact]
    public void Status_is_a_concurrency_token_so_stale_webhooks_cannot_overwrite_the_sweep()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=unused;Username=unused;Password=unused").Options;
        using var db = new ApplicationDbContext(options);
        var entity = db.Model.FindEntityType(typeof(PaymentTransaction));
        Assert.True(entity?.FindProperty(nameof(PaymentTransaction.Status))?.IsConcurrencyToken);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    private sealed class PayOsHandler : HttpMessageHandler
    {
        public long ExpiresAt { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            var payload = document.RootElement;
            ExpiresAt = payload.GetProperty("expiredAt").GetInt64();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    code = "00", desc = "success",
                    data = new
                    {
                        orderCode = payload.GetProperty("orderCode").GetInt64(),
                        amount = payload.GetProperty("amount").GetInt64(), status = "PENDING",
                        checkoutUrl = "https://pay.payos.vn/test", qrCode = "test-qr",
                    },
                })),
            };
        }
    }

    private sealed class CurrentUser(Guid userId) : ICurrentUser
    {
        public Guid? UserId => userId;
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class MemoryPayments(PaymentTransaction payment) : IPaymentTransactionRepository
    {
        public Task<int> CancelOverdueAsync(DateTimeOffset now, Guid? userId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult((!userId.HasValue || userId == payment.UserId) && payment.CancelIfOverdue(now) ? 1 : 0);
        public Task<PaymentTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<PaymentTransaction?>(payment.Id == id ? payment : null);
        public Task<PaymentTransaction?> GetByOrderCodeAsync(string orderCode, CancellationToken cancellationToken = default) => Task.FromResult<PaymentTransaction?>(payment.OrderCode == orderCode ? payment : null);
        public Task<PaymentTransaction?> GetByRequestIdAsync(string requestId, CancellationToken cancellationToken = default) => Task.FromResult<PaymentTransaction?>(null);
        public Task<IReadOnlyList<PaymentTransaction>> GetForUserAsync(Guid userId, PaymentStatus? status, int take, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PaymentTransaction>>(payment.UserId == userId && (!status.HasValue || status == payment.Status) ? [payment] : []);
        public Task AddAsync(PaymentTransaction newPayment, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
