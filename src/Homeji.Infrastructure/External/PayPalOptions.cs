namespace Homeji.Infrastructure.External;

public sealed class PayPalOptions
{
    public const string SectionName = "Payments:PayPal";

    public bool Enabled { get; set; } = true;
    public string Mode { get; set; } = "Live";
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public bool GooglePayEnabled { get; set; } = true;
    public bool ApplePayEnabled { get; set; } = true;
    public string FrontendOrigin { get; set; } = "http://localhost:5173";
    public string VietcombankRateUrl { get; set; } = "https://portal.vietcombank.com.vn/Usercontrols/TVPortal.TyGia/pXML.aspx";
    public string OpenExchangeRateUrl { get; set; } = "https://open.er-api.com/v6/latest/USD";
    public decimal FallbackExchangeRateVnd { get; set; } = 25_500m;
    public int RateCacheMinutes { get; set; } = 30;
    public decimal BankFeePercent { get; set; } = 0.025m;
    public decimal GatewayFeePercent { get; set; } = 0.044m;
    public decimal GatewayFixedFeeUsd { get; set; } = 0.30m;

    public bool IsLive => string.Equals(Mode?.Trim(), "Live", StringComparison.OrdinalIgnoreCase);

    public Uri ApiBaseUri => new(IsLive ? "https://api-m.paypal.com/" : "https://api-m.sandbox.paypal.com/");

    public string CheckoutHost => IsLive ? "www.paypal.com" : "www.sandbox.paypal.com";

    public bool IsConfigured => Enabled
        && !string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(ClientSecret);
}
