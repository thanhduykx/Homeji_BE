namespace Homeji.Application.IServices.Promotions;
public sealed record VoucherDto(Guid Id, string Purpose, decimal Rate, decimal MaximumDiscount, decimal MinimumSpend, DateTimeOffset ExpiresAt, bool Used);
public sealed record ReferralSummary(string? Code, int SuccessfulInvites, int RemainingPairs, bool Received, IReadOnlyList<VoucherDto> Vouchers);
public interface IReferralService
{
    Task<ReferralSummary> GetAsync(CancellationToken cancellationToken = default);
    Task<ReferralSummary> CreateCodeAsync(CancellationToken cancellationToken = default);
    Task<ReferralSummary> ClaimAsync(string code, CancellationToken cancellationToken = default);
    Task<decimal> QuoteAsync(Guid voucherId, string purpose, decimal amount, CancellationToken cancellationToken = default);
    Task<decimal> UseAsync(Guid voucherId, string purpose, decimal amount, Guid referenceId, CancellationToken cancellationToken = default);
    Task RestoreAsync(Guid referenceId, CancellationToken cancellationToken = default);
}
