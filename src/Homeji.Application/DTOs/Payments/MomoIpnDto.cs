namespace Homeji.Application.DTOs.Payments;

public sealed record MomoIpnDto(
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
