using Homeji.Domain.Enums;
using Homeji.Domain.Exceptions;

namespace Homeji.Domain.Entities;

public sealed class PaymentTransaction
{
    public const int MaxOrderCodeLength = 80;
    public const int MaxRequestIdLength = 80;
    public const int MaxDescriptionLength = 500;
    public const int MaxUrlLength = 2_000;
    public const int MaxExternalTransactionIdLength = 120;
    public const int MaxProviderMessageLength = 500;
    public const int MaxPackageCodeLength = 80;

    private PaymentTransaction()
    {
        OrderCode = null!;
        Description = null!;
    }

    public PaymentTransaction(
        Guid userId,
        PaymentMethod method,
        decimal amount,
        string orderCode,
        string description,
        DateTimeOffset createdAt,
        PaymentPurpose purpose = PaymentPurpose.General,
        string? packageCode = null)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("Mã người dùng không được để trống.");
        }

        if (amount <= 0)
        {
            throw new DomainException("Số tiền thanh toán phải lớn hơn 0.");
        }

        if (!Enum.IsDefined(purpose))
        {
            throw new DomainException("Mục đích thanh toán không hợp lệ.");
        }

        Id = Guid.NewGuid();
        UserId = userId;
        Method = method;
        Amount = amount;
        OrderCode = NormalizeRequired(orderCode, MaxOrderCodeLength, nameof(OrderCode));
        Description = NormalizeRequired(description, MaxDescriptionLength, nameof(Description));
        Purpose = purpose;
        PackageCode = purpose == PaymentPurpose.PremiumSubscription
            ? NormalizeRequired(packageCode!, MaxPackageCodeLength, nameof(PackageCode))
            : NormalizeOptional(packageCode, MaxPackageCodeLength, nameof(PackageCode));
        Status = PaymentStatus.Pending;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public PaymentMethod Method { get; private set; }

    public PaymentStatus Status { get; private set; }

    public decimal Amount { get; private set; }

    public PaymentPurpose Purpose { get; private set; } = PaymentPurpose.General;

    public string? PackageCode { get; private set; }

    public string OrderCode { get; private set; }

    public string? RequestId { get; private set; }

    public string Description { get; private set; }

    public string? PaymentUrl { get; private set; }

    public string? Deeplink { get; private set; }

    public string? QrCodeUrl { get; private set; }

    public string? QrCode { get; private set; }

    public string? QrDataUrl { get; private set; }

    public string? ExternalTransactionId { get; private set; }

    public string? ProviderMessage { get; private set; }

    public string? RawProviderPayload { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? PaidAt { get; private set; }

    public void AttachMomoPayment(
        string requestId,
        string? paymentUrl,
        string? deeplink,
        string? qrCodeUrl,
        string? providerMessage,
        string? rawProviderPayload,
        DateTimeOffset updatedAt)
    {
        RequestId = NormalizeRequired(requestId, MaxRequestIdLength, nameof(RequestId));
        PaymentUrl = NormalizeOptional(paymentUrl, MaxUrlLength, nameof(paymentUrl));
        Deeplink = NormalizeOptional(deeplink, MaxUrlLength, nameof(deeplink));
        QrCodeUrl = NormalizeOptional(qrCodeUrl, MaxUrlLength, nameof(qrCodeUrl));
        ProviderMessage = NormalizeOptional(providerMessage, MaxProviderMessageLength, nameof(providerMessage));
        RawProviderPayload = rawProviderPayload;
        UpdatedAt = updatedAt;
    }

    public void AttachPayOsQrPayment(
        string? qrCode,
        string? qrDataUrl,
        string? rawProviderPayload,
        DateTimeOffset updatedAt)
    {
        QrCode = qrCode;
        QrDataUrl = NormalizeOptional(qrDataUrl, MaxUrlLength, nameof(qrDataUrl));
        RawProviderPayload = rawProviderPayload;
        UpdatedAt = updatedAt;
    }

    public void MarkPaid(
        string? externalTransactionId,
        string? providerMessage,
        string? rawProviderPayload,
        DateTimeOffset paidAt)
    {
        if (Status == PaymentStatus.Paid)
        {
            return;
        }

        Status = PaymentStatus.Paid;
        ExternalTransactionId = NormalizeOptional(externalTransactionId, MaxExternalTransactionIdLength, nameof(externalTransactionId));
        ProviderMessage = NormalizeOptional(providerMessage, MaxProviderMessageLength, nameof(providerMessage));
        RawProviderPayload = rawProviderPayload;
        PaidAt = paidAt;
        UpdatedAt = paidAt;
    }

    public void MarkFailed(
        string? providerMessage,
        string? rawProviderPayload,
        DateTimeOffset failedAt)
    {
        if (Status == PaymentStatus.Paid)
        {
            return;
        }

        Status = PaymentStatus.Failed;
        ProviderMessage = NormalizeOptional(providerMessage, MaxProviderMessageLength, nameof(providerMessage));
        RawProviderPayload = rawProviderPayload;
        UpdatedAt = failedAt;
    }

    private static string NormalizeRequired(string value, int maxLength, string fieldName)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new DomainException($"{fieldName} là bắt buộc.");
        }

        if (normalized.Length > maxLength)
        {
            throw new DomainException($"{fieldName} không được vượt quá {maxLength} ký tự.");
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength, string fieldName)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (normalized.Length > maxLength)
        {
            throw new DomainException($"{fieldName} không được vượt quá {maxLength} ký tự.");
        }

        return normalized;
    }
}
