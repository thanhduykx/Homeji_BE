using System.ComponentModel.DataAnnotations;
namespace Homeji.Application.DTOs.Payments;

public sealed record WalletPremiumPaymentDto(
    Guid RequestId,
    [Range(1, 1000000000)] decimal ExpectedAmount, Guid? VoucherId = null);
