using System.Globalization;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Payments;
using Homeji.Application.IServices.Payments;
using Microsoft.Extensions.Options;

namespace Homeji.Infrastructure.External;

public sealed class PayPalExchangeRateService(
    HttpClient http,
    IOptions<PayPalOptions> options,
    TimeProvider clock) : IPayPalExchangeRateService
{
    private const decimal MinValidVndPerUsd = 15_000m;
    private const decimal MaxValidVndPerUsd = 50_000m;

    private readonly object _cacheSync = new();
    private CachedRate? _cached;

    public async Task<PayPalCurrencyQuoteDto> QuoteUsdAsync(
        decimal amountVnd,
        CancellationToken cancellationToken = default)
    {
        if (amountVnd <= 0)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["amount"] = ["Số tiền quy đổi phải lớn hơn 0 VND."],
            });
        }

        var settings = options.Value;
        var rate = await GetExchangeRateAsync(settings, cancellationToken);
        var bankFeePercent = Math.Max(0m, settings.BankFeePercent);
        var gatewayFeePercent = Math.Max(0m, settings.GatewayFeePercent);
        var gatewayFixedFeeUsd = Math.Max(0m, settings.GatewayFixedFeeUsd);

        var baseUsd = Math.Round(amountVnd / rate.RateVnd, 2, MidpointRounding.AwayFromZero);
        if (baseUsd <= 0m)
        {
            baseUsd = 0.01m;
        }

        var bankFeeUsd = Math.Round(baseUsd * bankFeePercent, 2, MidpointRounding.AwayFromZero);
        var gatewayFeeUsd = Math.Round(
            (baseUsd + bankFeeUsd) * gatewayFeePercent + gatewayFixedFeeUsd,
            2,
            MidpointRounding.AwayFromZero);
        var totalFeeUsd = bankFeeUsd + gatewayFeeUsd;
        var totalUsd = baseUsd + totalFeeUsd;

        return new PayPalCurrencyQuoteDto(
            amountVnd,
            rate.RateVnd,
            rate.Source,
            rate.UpdatedAt,
            baseUsd,
            bankFeePercent,
            bankFeeUsd,
            gatewayFeePercent,
            gatewayFixedFeeUsd,
            gatewayFeeUsd,
            totalFeeUsd,
            totalUsd);
    }

    private async Task<CachedRate> GetExchangeRateAsync(
        PayPalOptions settings,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var cacheDuration = TimeSpan.FromMinutes(Math.Max(0, settings.RateCacheMinutes));
        if (cacheDuration > TimeSpan.Zero)
        {
            lock (_cacheSync)
            {
                if (_cached is not null && now - _cached.FetchedAt < cacheDuration)
                {
                    return _cached;
                }
            }
        }

        var resolved = await TryFetchVietcombankRateAsync(settings.VietcombankRateUrl, now, cancellationToken)
            ?? await TryFetchOpenExchangeRateAsync(settings.OpenExchangeRateUrl, now, cancellationToken)
            ?? BuildFallbackRate(settings.FallbackExchangeRateVnd, now);

        if (cacheDuration > TimeSpan.Zero)
        {
            lock (_cacheSync)
            {
                _cached = resolved;
            }
        }

        return resolved;
    }

    private async Task<CachedRate?> TryFetchVietcombankRateAsync(
        string url,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var xml = await TryGetStringAsync(url, cancellationToken);
        if (string.IsNullOrWhiteSpace(xml))
        {
            return null;
        }

        try
        {
            var document = XDocument.Parse(xml, LoadOptions.None);
            var usdElement = document.Descendants()
                .FirstOrDefault(element =>
                    string.Equals(element.Name.LocalName, "Exrate", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(GetAttributeValue(element, "CurrencyCode"), "USD", StringComparison.OrdinalIgnoreCase));

            if (usdElement is null)
            {
                return null;
            }

            if (!TryParseVndRate(GetAttributeValue(usdElement, "Sell"), out var rate)
                && !TryParseVndRate(GetAttributeValue(usdElement, "Transfer"), out rate))
            {
                return null;
            }

            return new CachedRate(rate, "Vietcombank (Tỷ giá bán USD)", now, now);
        }
        catch (XmlException)
        {
            return null;
        }
    }

    private async Task<CachedRate?> TryFetchOpenExchangeRateAsync(
        string url,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var json = await TryGetStringAsync(url, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("rates", out var rates)
                || rates.ValueKind != JsonValueKind.Object
                || !rates.TryGetProperty("VND", out var vnd)
                || vnd.ValueKind != JsonValueKind.Number
                || !vnd.TryGetDecimal(out var rate)
                || !IsRateInRange(rate))
            {
                return null;
            }

            var rounded = Math.Round(rate, 2, MidpointRounding.AwayFromZero);
            return new CachedRate(rounded, "ExchangeRate-API (USD/VND quốc tế)", now, now);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<string?> TryGetStringAsync(string url, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("https" or "http"))
        {
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private static CachedRate BuildFallbackRate(decimal configuredFallback, DateTimeOffset now)
    {
        var clamped = Math.Clamp(
            configuredFallback > 0m ? configuredFallback : 25_500m,
            MinValidVndPerUsd,
            MaxValidVndPerUsd);
        return new CachedRate(clamped, "Tỷ giá tham chiếu dự phòng", now, now);
    }

    private static string? GetAttributeValue(XElement element, string attributeName) =>
        element.Attributes()
            .FirstOrDefault(attribute => string.Equals(attribute.Name.LocalName, attributeName, StringComparison.OrdinalIgnoreCase))
            ?.Value;

    private static bool TryParseVndRate(string? raw, out decimal rate)
    {
        rate = 0m;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var cleaned = raw.Trim().Replace(",", string.Empty, StringComparison.Ordinal);
        return decimal.TryParse(cleaned, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out rate)
            && IsRateInRange(rate);
    }

    private static bool IsRateInRange(decimal rate) =>
        rate >= MinValidVndPerUsd && rate <= MaxValidVndPerUsd;

    private sealed record CachedRate(decimal RateVnd, string Source, DateTimeOffset UpdatedAt, DateTimeOffset FetchedAt);
}
