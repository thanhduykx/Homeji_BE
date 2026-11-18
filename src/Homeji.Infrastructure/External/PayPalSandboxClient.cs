using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Payments;
using Homeji.Application.IServices.Payments;
using Microsoft.Extensions.Options;

namespace Homeji.Infrastructure.External;

internal sealed class PayPalSandboxCredentials
{
    public string ClientId { get; init; } = "";
    public string ClientSecret { get; init; } = "";
}

internal sealed class PayPalSandboxClient(HttpClient http, IOptions<PayPalSandboxCredentials> settings) : IPayPalSandboxClient
{
    private const string Currency = "USD";
    private const string Amount = "1.00";
    private readonly PayPalSandboxCredentials _credentials = settings.Value;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_credentials.ClientId)
        && !string.IsNullOrWhiteSpace(_credentials.ClientSecret);

    public async Task<(string OrderId, Uri ApprovalUrl)> CreateAsync(
        Uri returnUrl, Uri cancelUrl, CancellationToken cancellationToken)
    {
        var token = await GetTokenAsync(cancellationToken);
        using var request = OrderRequest(HttpMethod.Post, "v2/checkout/orders", token);
        request.Headers.Add("PayPal-Request-Id", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(new
        {
            intent = "CAPTURE",
            purchase_units = new[]
            {
                new { description = "Homeji Sandbox", amount = new { currency_code = Currency, value = Amount } },
            },
            payment_source = new
            {
                paypal = new
                {
                    experience_context = new
                    {
                        return_url = returnUrl.AbsoluteUri,
                        cancel_url = cancelUrl.AbsoluteUri,
                        shipping_preference = "NO_SHIPPING",
                        user_action = "PAY_NOW",
                    },
                },
            },
        });

        using var response = await SendAsync(request, cancellationToken);
        var order = await ReadAsync<Order>(response, cancellationToken);
        var approval = order.Links?.FirstOrDefault(link => link.Rel is "payer-action" or "approve");
        if (!IsOrderId(order.Id)
            || !Uri.TryCreate(approval?.Href, UriKind.Absolute, out var url)
            || url.Scheme != Uri.UriSchemeHttps || url.Host != "www.sandbox.paypal.com"
            || !url.IsDefaultPort || !string.IsNullOrEmpty(url.UserInfo))
        {
            throw ProviderError();
        }

        return (order.Id!, url);
    }

    public async Task<PayPalSandboxCapture> CaptureAsync(string orderId, CancellationToken cancellationToken)
    {
        if (!IsOrderId(orderId))
        {
            throw ProviderError();
        }

        var token = await GetTokenAsync(cancellationToken);
        var orderPath = $"v2/checkout/orders/{orderId}";
        using var request = OrderRequest(HttpMethod.Post, $"{orderPath}/capture", token);
        request.Headers.Add("PayPal-Request-Id", $"capture-{orderId}");
        request.Headers.Add("Prefer", "return=representation");
        request.Content = JsonContent.Create(new { });
        using var response = await SendAsync(request, cancellationToken);

        Order order;
        if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            // An earlier capture can succeed even when Homeji never receives its response.
            using var lookup = OrderRequest(HttpMethod.Get, orderPath, token);
            using var current = await SendAsync(lookup, cancellationToken);
            order = await ReadAsync<Order>(current, cancellationToken);
        }
        else
        {
            order = await ReadAsync<Order>(response, cancellationToken);
        }

        if (order.Id != orderId)
        {
            throw ProviderError();
        }

        var capture = order.PurchaseUnits is [{ Payments.Captures: [var singleCapture] }]
            ? singleCapture : null;
        if (capture is null)
        {
            return new("unconfirmed", null);
        }

        if (capture.Amount?.Currency != Currency
            || !decimal.TryParse(capture.Amount.Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount)
            || amount != 1m || string.IsNullOrWhiteSpace(capture.Id))
        {
            throw ProviderError();
        }

        return capture.Status switch
        {
            "COMPLETED" when order.Status == "COMPLETED" => new("completed", capture.Id),
            "PENDING" => new("pending", null),
            "DECLINED" or "FAILED" or "REFUNDED" or "PARTIALLY_REFUNDED" => new("failed", null),
            _ => new("unconfirmed", null),
        };
    }

    private async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/oauth2/token");
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_credentials.ClientId}:{_credentials.ClientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
        });
        using var response = await SendAsync(request, cancellationToken);
        var token = await ReadAsync<OAuthToken>(response, cancellationToken);
        return !string.IsNullOrWhiteSpace(token.AccessToken) ? token.AccessToken : throw ProviderError();
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new ExternalDependencyException("Không kết nối được với PayPal Sandbox.", exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ExternalDependencyException("PayPal Sandbox phản hồi quá chậm. Vui lòng thử lại.", exception);
        }
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw ProviderError();
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
                ?? throw ProviderError();
        }
        catch (JsonException)
        {
            throw ProviderError();
        }
    }

    private static HttpRequestMessage OrderRequest(HttpMethod method, string path, string token)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static bool IsOrderId(string? id) => id is { Length: > 0 and <= 32 } && id.All(char.IsAsciiLetterOrDigit);

    private static ExternalDependencyException ProviderError() =>
        new("PayPal Sandbox chưa trả về kết quả hợp lệ. Vui lòng kiểm tra cấu hình hoặc thử lại.");

    private sealed record OAuthToken([property: JsonPropertyName("access_token")] string? AccessToken);
    private sealed record Order(string? Id, string? Status, Link[]? Links,
        [property: JsonPropertyName("purchase_units")] PurchaseUnit[]? PurchaseUnits);
    private sealed record Link(string? Rel, string? Href);
    private sealed record PurchaseUnit(Payments? Payments);
    private sealed record Payments(Capture[]? Captures);
    private sealed record Capture(string? Id, string? Status, Money? Amount);
    private sealed record Money([property: JsonPropertyName("currency_code")] string? Currency, string? Value);
}
