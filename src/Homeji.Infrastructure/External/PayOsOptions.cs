namespace Homeji.Infrastructure.External;

public sealed class PayOsOptions
{
    public string Endpoint { get; set; } = "https://api-merchant.payos.vn/v2/payment-requests";

    public string ClientId { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string ChecksumKey { get; set; } = string.Empty;

    public string ReturnUrl { get; set; } = string.Empty;

    public string CancelUrl { get; set; } = string.Empty;
}
