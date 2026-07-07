namespace Homeji.Api.Views.Payments;

public sealed record MomoIpnViewModel(
    string? PartnerCode,
    string? OrderId,
    string? RequestId,
    long Amount,
    string? OrderInfo,
    string? OrderType,
    long TransId,
    int ResultCode,
    string? Message,
    string? PayType,
    long ResponseTime,
    string? ExtraData,
    string? Signature);
