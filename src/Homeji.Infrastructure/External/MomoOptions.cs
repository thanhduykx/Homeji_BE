namespace Homeji.Infrastructure.External;

public sealed class MomoOptions
{
    public string Endpoint { get; set; } = "https://test-payment.momo.vn/v2/gateway/api/create";

    public string PartnerCode { get; set; } = string.Empty;

    public string AccessKey { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    public string RedirectUrl { get; set; } = string.Empty;

    public string IpnUrl { get; set; } = string.Empty;

    public string RequestType { get; set; } = "captureWallet";

    public string Lang { get; set; } = "vi";
}
