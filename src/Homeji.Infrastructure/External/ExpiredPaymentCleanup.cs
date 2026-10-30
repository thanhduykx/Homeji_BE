using System.Text.Json;
using Homeji.Application.IServices.Payments;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.Context;
using Homeji.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Homeji.Infrastructure.External;

/// <summary>Deletes only expired attempts confirmed unpaid by their provider, under the wallet lock.</summary>
public sealed class ExpiredPaymentCleanup(ApplicationDbContext db, HttpClient http,
    IOptions<PayOsOptions> options, TimeProvider clock, ILogger<ExpiredPaymentCleanup> logger) : IExpiredPaymentCleanup
{
    private static long _scanPosition;
    private static readonly Action<ILogger, Exception?> LogFailure = LoggerMessage.Define(
        LogLevel.Warning, new EventId(1, nameof(ExpiredPaymentCleanup)),
        "An expired payment was retained because its unpaid state could not be verified.");

    public async Task<int> DeleteConfirmedUnpaidAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = clock.GetUtcNow().Subtract(PaymentTransaction.PaymentLifetime);
        var eligibleCount = await Eligible(cutoff).CountAsync(cancellationToken);
        if (eligibleCount == 0) return 0;
        // Rotate through retained/unknown attempts so they cannot block later rows forever.
        var offset = (int)((Interlocked.Add(ref _scanPosition, 25) - 25) % eligibleCount);
        var candidates = await Eligible(cutoff).AsNoTracking().OrderBy(payment => payment.CreatedAt).ThenBy(payment => payment.Id).Skip(offset)
            .Select(payment => new { payment.Id, payment.UserId }).Take(25).ToListAsync(cancellationToken);
        var deleted = 0;
        foreach (var candidate in candidates)
        {
            try
            {
                deleted += await FinancialTransaction.ExecuteAsync(db, candidate.UserId, async () =>
                {
                    var payment = await Eligible(cutoff).AsNoTracking().SingleOrDefaultAsync(p => p.Id == candidate.Id, cancellationToken);
                    if (payment is null || !await ConfirmUnpaidAsync(payment, cancellationToken)) return 0;
                    // Recheck all references and status in the DELETE statement, as well as holding the shared financial lock.
                    return await Eligible(cutoff).Where(p => p.Id == payment.Id).ExecuteDeleteAsync(cancellationToken);
                }, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception) when (exception is HttpRequestException or JsonException or OperationCanceledException)
            {
                LogFailure(logger, exception);
            }
        }
        return deleted;
    }

    private IQueryable<PaymentTransaction> Eligible(DateTimeOffset cutoff) => db.PaymentTransactions.Where(payment =>
        payment.Status == PaymentStatus.Cancelled && payment.ProviderMessage == PaymentTransaction.ExpirationMessage
        && payment.Method == PaymentMethod.PayOs && payment.CreatedAt <= cutoff
        && payment.PaidAt == null && payment.ExternalTransactionId == null
        && !db.WalletTransactions.Any(entry => entry.ReferenceId == payment.Id)
        && !db.TransactionReceipts.Any(receipt => receipt.ReferenceId == payment.Id || receipt.ReferenceCode == payment.OrderCode)
        && !db.UserSubscriptions.Any(subscription => subscription.PaymentTransactionId == payment.Id)
        && !db.ReferralVouchers.Any(voucher => voucher.ReferenceId == payment.Id));

    private async Task<bool> ConfirmUnpaidAsync(PaymentTransaction payment, CancellationToken cancellationToken)
    {
        var configuration = options.Value;
        if (string.IsNullOrWhiteSpace(configuration.ClientId) || string.IsNullOrWhiteSpace(configuration.ApiKey)) return false;
        using var request = new HttpRequestMessage(HttpMethod.Get,
            configuration.Endpoint.TrimEnd('/') + "/" + Uri.EscapeDataString(payment.OrderCode));
        request.Headers.Add("x-client-id", configuration.ClientId);
        request.Headers.Add("x-api-key", configuration.ApiKey);
        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return false;
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        return IsConfirmedUnpaid(document.RootElement, payment.OrderCode, payment.Amount);
    }

    public static bool IsConfirmedUnpaid(JsonElement root, string orderCode, decimal amount)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("code", out var code) || code.ValueKind != JsonValueKind.String || code.GetString() != "00"
            || !root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object
            || !data.TryGetProperty("orderCode", out var order) || order.ToString() != orderCode
            || !data.TryGetProperty("amount", out var total) || total.ValueKind != JsonValueKind.Number || !total.TryGetDecimal(out var providerAmount) || providerAmount != amount
            || !data.TryGetProperty("amountPaid", out var paid) || paid.ValueKind != JsonValueKind.Number || !paid.TryGetDecimal(out var paidAmount) || paidAmount != 0
            || !data.TryGetProperty("status", out var status) || status.ValueKind != JsonValueKind.String
            || status.GetString() is not ("CANCELLED" or "EXPIRED")
            || !data.TryGetProperty("transactions", out var transactions)) return false;
        return transactions.ValueKind == JsonValueKind.Array && transactions.GetArrayLength() == 0
            || transactions.ValueKind == JsonValueKind.Object && !transactions.EnumerateObject().Any();
    }
}
