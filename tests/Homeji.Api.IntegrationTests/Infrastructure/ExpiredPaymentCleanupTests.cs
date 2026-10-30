using System.Net;
using System.Text.Json;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.Context;
using Homeji.Infrastructure.External;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Homeji.Api.IntegrationTests.Infrastructure;

public sealed class ExpiredPaymentCleanupTests
{
    [Theory]
    [InlineData("CANCELLED", 0, true)]
    [InlineData("EXPIRED", 0, true)]
    [InlineData("PAID", 100000, false)]
    [InlineData("PENDING", 0, false)]
    [InlineData("CANCELLED", 50000, false)]
    public void Provider_must_confirm_terminal_status_and_zero_received_money(string status, decimal amountPaid, bool expected)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(new { code = "00", data = new { orderCode = 123, amount = 100000, amountPaid, status, transactions = Array.Empty<object>() } }));
        Assert.Equal(expected, ExpiredPaymentCleanup.IsConfirmedUnpaid(document.RootElement, "123", 100000));
        Assert.False(ExpiredPaymentCleanup.IsConfirmedUnpaid(document.RootElement, "another-order", 100000));
        Assert.False(ExpiredPaymentCleanup.IsConfirmedUnpaid(document.RootElement, "123", 200000));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"code\":\"00\",\"data\":{}}")]
    [InlineData("{\"code\":\"00\",\"data\":{\"orderCode\":123,\"amount\":100000,\"amountPaid\":0,\"status\":\"CANCELLED\",\"transactions\":[{\"amount\":100}]}}")]
    public void Unknown_or_conflicting_provider_evidence_never_allows_deletion(string json)
    {
        using var document = JsonDocument.Parse(json);
        Assert.False(ExpiredPaymentCleanup.IsConfirmedUnpaid(document.RootElement, "123", 100000));
    }

    [LocalMarketplaceDatabaseFact]
    public async Task Deletes_only_confirmed_expired_attempts_without_financial_links()
    {
        var connection = Environment.GetEnvironmentVariable("HOMEJI_TEST_DATABASE")!;
        var settings = new NpgsqlConnectionStringBuilder(connection);
        if (settings.Host != "127.0.0.1" || settings.Database != "homeji_quality") throw new InvalidOperationException("Disposable local database required.");
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        var now = DateTimeOffset.UtcNow;
        var user = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO auth.users(id) VALUES ({user})");
        db.UserProfiles.Add(UserProfile.Create(user, "Cleanup fixture", now));
        db.WalletAccounts.Add(WalletAccount.Create(user, now));
        PaymentTransaction Payment(PaymentMethod method = PaymentMethod.PayOs)
        {
            var payment = new PaymentTransaction(user, method, 100000, Guid.NewGuid().ToString("N"), "Cleanup fixture", now.AddHours(-1), PaymentPurpose.WalletTopUp);
            payment.CancelIfOverdue(now);
            db.PaymentTransactions.Add(payment);
            return payment;
        }
        var clean = Payment();
        var ledger = Payment();
        var receipt = Payment();
        var partial = Payment();
        var unknown = Payment();
        var momo = Payment(PaymentMethod.Momo);
        var paid = Payment(); paid.MarkPaid("received", "Paid", "{}", now);
        db.WalletTransactions.Add(new WalletTransaction(user, WalletTransactionKind.TopUp, 100000, 100000, ledger.Id, "Existing money movement", now));
        db.TransactionReceipts.Add(new TransactionReceipt(user, receipt.Id, ReceiptEvent.PaymentSucceeded, 100000, receipt.OrderCode, "Existing receipt", now));
        await db.SaveChangesAsync();
        using var handler = new ProviderHandler(partial.OrderCode, unknown.OrderCode);
        using var client = new HttpClient(handler);
        var service = new ExpiredPaymentCleanup(db, client, Options.Create(new PayOsOptions { ClientId = "fixture", ApiKey = "fixture", Endpoint = "https://example.test/payment-requests" }), TimeProvider.System, NullLogger<ExpiredPaymentCleanup>.Instance);
        await service.DeleteConfirmedUnpaidAsync();
        db.ChangeTracker.Clear();
        Assert.False(await db.PaymentTransactions.AnyAsync(p => p.Id == clean.Id));
        foreach (var payment in new[] { ledger, receipt, partial, unknown, momo, paid }) Assert.True(await db.PaymentTransactions.AnyAsync(p => p.Id == payment.Id));
        Assert.True(await db.WalletTransactions.AnyAsync(entry => entry.ReferenceId == ledger.Id));
    }

    private sealed class ProviderHandler(string partial, string unknown) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var order = request.RequestUri!.Segments[^1];
            if (order == unknown) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { code = "00", data = new { orderCode = order, amount = 100000, amountPaid = order == partial ? 50000 : 0, status = "CANCELLED", transactions = Array.Empty<object>() } })) });
        }
    }
}
