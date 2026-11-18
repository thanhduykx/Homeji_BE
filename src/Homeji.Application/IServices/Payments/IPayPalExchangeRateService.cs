using Homeji.Application.DTOs.Payments;

namespace Homeji.Application.IServices.Payments;

public interface IPayPalExchangeRateService
{
    Task<PayPalCurrencyQuoteDto> QuoteUsdAsync(decimal amountVnd, CancellationToken cancellationToken = default);
}
