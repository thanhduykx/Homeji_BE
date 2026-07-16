using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Wallets;
using Homeji.Application.IRepositories.Wallets;
using Homeji.Application.IServices.Wallets;
using Homeji.Application.Services.Common;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;

namespace Homeji.Application.Services.Wallets;

public sealed class WalletWithdrawalService : IWalletWithdrawalService
{
    private readonly UserContext _userContext;
    private readonly IWalletRepository _wallets;
    private readonly IWalletWithdrawalRepository _withdrawals;
    private readonly TimeProvider _timeProvider;

    public WalletWithdrawalService(
        UserContext userContext,
        IWalletRepository wallets,
        IWalletWithdrawalRepository withdrawals,
        TimeProvider timeProvider)
    {
        _userContext = userContext;
        _wallets = wallets;
        _withdrawals = withdrawals;
        _timeProvider = timeProvider;
    }

    public async Task<WalletWithdrawalDto> CreateAsync(
        CreateWalletWithdrawalDto request,
        CancellationToken cancellationToken = default)
    {
        ValidateCreate(request);
        var userId = _userContext.GetRequiredUserId();
        var wallet = await _wallets.GetAsync(userId, cancellationToken)
            ?? throw Validation("amount", "Ví chưa được kích hoạt.");
        var now = _timeProvider.GetUtcNow();
        var withdrawal = new WalletWithdrawalRequest(
            userId,
            request.Amount,
            request.BankName,
            request.AccountNumber,
            request.AccountHolder,
            now);

        wallet.DebitWithdrawal(request.Amount, now);
        await _withdrawals.AddAsync(withdrawal, cancellationToken);
        await _wallets.AddTransactionAsync(new WalletTransaction(
            userId,
            WalletTransactionKind.Withdrawal,
            -request.Amount,
            wallet.Balance,
            withdrawal.Id,
            $"Yêu cầu rút tiền về {withdrawal.BankName} ••••{LastFour(withdrawal.AccountNumber)}",
            now), cancellationToken);
        await _withdrawals.SaveChangesAsync(cancellationToken);
        return ToDto(withdrawal);
    }

    public async Task<IReadOnlyList<WalletWithdrawalDto>> GetMineAsync(CancellationToken cancellationToken = default) =>
        (await _withdrawals.GetByUserAsync(_userContext.GetRequiredUserId(), cancellationToken))
        .Select(ToDto)
        .ToArray();

    public async Task<IReadOnlyList<WalletWithdrawalDto>> GetForAdminAsync(
        WalletWithdrawalStatus? status,
        CancellationToken cancellationToken = default)
    {
        await EnsureAdminAsync(cancellationToken);
        if (status is not null && !Enum.IsDefined(status.Value))
        {
            throw Validation("status", "Trạng thái yêu cầu rút tiền không hợp lệ.");
        }
        return (await _withdrawals.GetForAdminAsync(status, cancellationToken)).Select(ToDto).ToArray();
    }

    public async Task<WalletWithdrawalDto> CompleteAsync(
        Guid id,
        ReviewWalletWithdrawalDto request,
        CancellationToken cancellationToken = default)
    {
        ValidateReviewNote(request.Note, "Vui lòng nhập mã giao dịch chuyển khoản.");
        var admin = await EnsureAdminAsync(cancellationToken);
        var withdrawal = await GetRequiredAsync(id, cancellationToken);
        withdrawal.Complete(admin.Id, request.Note, _timeProvider.GetUtcNow());
        await _withdrawals.SaveChangesAsync(cancellationToken);
        return ToDto(withdrawal);
    }

    public async Task<WalletWithdrawalDto> RejectAsync(
        Guid id,
        ReviewWalletWithdrawalDto request,
        CancellationToken cancellationToken = default)
    {
        ValidateReviewNote(request.Note, "Vui lòng nhập lý do từ chối.");
        var admin = await EnsureAdminAsync(cancellationToken);
        var withdrawal = await GetRequiredAsync(id, cancellationToken);
        var wallet = await _wallets.GetAsync(withdrawal.UserId, cancellationToken)
            ?? throw new InvalidOperationException("Withdrawal wallet was not found.");
        var now = _timeProvider.GetUtcNow();

        withdrawal.Reject(admin.Id, request.Note, now);
        wallet.CreditWithdrawalRefund(withdrawal.Amount, now);
        await _wallets.AddTransactionAsync(new WalletTransaction(
            wallet.UserId,
            WalletTransactionKind.WithdrawalRefund,
            withdrawal.Amount,
            wallet.Balance,
            withdrawal.Id,
            "Hoàn tiền yêu cầu rút bị từ chối",
            now), cancellationToken);
        await _withdrawals.SaveChangesAsync(cancellationToken);
        return ToDto(withdrawal);
    }

    private async Task<Homeji.Domain.Entities.UserProfile> EnsureAdminAsync(CancellationToken cancellationToken)
    {
        var profile = await _userContext.GetRequiredProfileAsync(cancellationToken);
        UserContext.EnsureAdmin(profile);
        return profile;
    }

    private async Task<WalletWithdrawalRequest> GetRequiredAsync(Guid id, CancellationToken cancellationToken) =>
        await _withdrawals.GetByIdAsync(id, cancellationToken)
        ?? throw new NotFoundException(nameof(WalletWithdrawalRequest), id);

    private static RequestValidationException Validation(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });

    private static void ValidateCreate(CreateWalletWithdrawalDto request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.Amount <= 0 || decimal.Truncate(request.Amount) != request.Amount)
            errors["amount"] = ["Số tiền rút phải là số nguyên dương."];
        if (string.IsNullOrWhiteSpace(request.BankName) || request.BankName.Trim().Length > WalletWithdrawalRequest.MaxBankNameLength)
            errors["bankName"] = ["Tên ngân hàng là bắt buộc và không được vượt quá 120 ký tự."];
        var accountNumber = request.AccountNumber?.Trim() ?? string.Empty;
        if (accountNumber.Length is < 6 or > WalletWithdrawalRequest.MaxAccountNumberLength || accountNumber.Any(character => !char.IsDigit(character)))
            errors["accountNumber"] = ["Số tài khoản phải gồm từ 6 đến 40 chữ số."];
        if (string.IsNullOrWhiteSpace(request.AccountHolder) || request.AccountHolder.Trim().Length > WalletWithdrawalRequest.MaxAccountHolderLength)
            errors["accountHolder"] = ["Tên chủ tài khoản là bắt buộc và không được vượt quá 120 ký tự."];
        if (errors.Count > 0) throw new RequestValidationException(errors);
    }

    private static void ValidateReviewNote(string? note, string requiredMessage)
    {
        if (string.IsNullOrWhiteSpace(note)) throw Validation("note", requiredMessage);
        if (note.Trim().Length > WalletWithdrawalRequest.MaxAdminNoteLength)
            throw Validation("note", "Ghi chú không được vượt quá 300 ký tự.");
    }

    private static string LastFour(string value) => value.Length <= 4 ? value : value[^4..];

    private static WalletWithdrawalDto ToDto(WalletWithdrawalRequest request) => new(
        request.Id,
        request.UserId,
        request.Amount,
        request.BankName,
        request.AccountNumber,
        request.AccountHolder,
        request.Status,
        request.AdminNote,
        request.ProcessedBy,
        request.CreatedAt,
        request.ProcessedAt);
}
