using System.Net;
using System.Text;
using System.Text.Json;
using Homeji.Api.Controllers;
using Homeji.Application.Abstractions.Authentication;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Payments;
using Homeji.Application.IRepositories.Profiles;
using Homeji.Application.Services.Common;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.External;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;

namespace Homeji.Api.IntegrationTests.Infrastructure;

public sealed class PayPalSandboxTests
{
    private const string OrderId = "5O190127TN364715T";
    private static readonly IOptions<PayPalSandboxOptions> Settings = Options.Create(new PayPalSandboxOptions
    {
        Enabled = true,
    });
    private static readonly IOptions<PayPalSandboxCredentials> Credentials = Options.Create(new PayPalSandboxCredentials
    {
        ClientId = "fixture", ClientSecret = "fixture",
    });

    [Theory]
    [InlineData("USD", "1.00", "COMPLETED", "completed")]
    [InlineData("USD", "1.00", "PENDING", "pending")]
    [InlineData("EUR", "1.00", "COMPLETED", null)]
    [InlineData("USD", "1.01", "COMPLETED", null)]
    public async Task Capture_verifies_the_amount_currency_and_provider_status(
        string currency, string amount, string status, string? expected)
    {
        using var handler = new SandboxHandler(currency, amount, status);
        using var http = CreateHttp(handler);
        var client = new PayPalSandboxClient(http, Credentials);

        if (expected is null)
        {
            await Assert.ThrowsAsync<ExternalDependencyException>(() => client.CaptureAsync(OrderId, default));
            return;
        }

        var capture = await client.CaptureAsync(OrderId, default);
        Assert.Equal(expected, capture.Status);
        Assert.Equal(expected == "completed" ? "CAPTUREID" : null, capture.CaptureId);
    }

    [Fact]
    public async Task Ticket_is_bound_to_the_buyer_and_capture_retry_is_idempotent()
    {
        using var handler = new SandboxHandler("USD", "1.00", "COMPLETED");
        using var http = CreateHttp(handler);
        var client = new PayPalSandboxClient(http, Credentials);
        var protection = new EphemeralDataProtectionProvider();
        var owner = new UserContext(new CurrentUser(Guid.NewGuid()), null!);
        var otherUser = new UserContext(new CurrentUser(Guid.NewGuid()), null!);
        var controller = new PayPalSandboxController(
            new HostingEnvironment { EnvironmentName = Environments.Development }, Settings, protection);

        var created = Assert.IsType<OkObjectResult>(await controller.Create(owner, client, default));
        var order = JsonSerializer.SerializeToElement(created.Value);
        var request = new PayPalSandboxController.CaptureRequest { Ticket = order.GetProperty("ticket").GetString()! };
        Assert.Equal(OrderId, order.GetProperty("orderId").GetString());
        Assert.Equal(404, Assert.IsType<ObjectResult>(await controller.Capture(request, otherUser, client, default)).StatusCode);
        Assert.Empty(handler.CaptureKeys);

        var first = Assert.IsType<OkObjectResult>(await controller.Capture(request, owner, client, default));
        var retry = Assert.IsType<OkObjectResult>(await controller.Capture(request, owner, client, default));
        Assert.Equal(new PayPalSandboxCapture("completed", "CAPTUREID"), first.Value);
        Assert.Equal(first.Value, retry.Value);
        Assert.Equal(2, handler.CaptureKeys.Count);
        Assert.Equal(handler.CaptureKeys[0], handler.CaptureKeys[1]);
        Assert.Equal(1, handler.OrderLookups);
    }

    [Theory]
    [InlineData(false, "https://homeji.onrender.com", UserRole.Admin, false)]
    [InlineData(true, "http://localhost:5173", UserRole.Admin, false)]
    [InlineData(true, "http://homeji.onrender.com", UserRole.Admin, false)]
    [InlineData(true, "https://homeji.onrender.com", UserRole.Renter, false)]
    [InlineData(true, "https://homeji.onrender.com", UserRole.Admin, true)]
    public async Task Production_sandbox_requires_enablement_https_and_admin(
        bool enabled, string frontendOrigin, UserRole role, bool allowed)
    {
        using var handler = new SandboxHandler("USD", "1.00", "COMPLETED", frontendOrigin);
        using var http = CreateHttp(handler);
        var client = new PayPalSandboxClient(http, Credentials);
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var profile = UserProfile.Create(userId, "Sandbox test", now);
        profile.SetRole(role, now);
        var profiles = Stub<IUserProfileRepository>.Create((method, _) => method.Name == nameof(IUserProfileRepository.GetByIdAsync)
            ? Task.FromResult<UserProfile?>(profile)
            : throw new InvalidOperationException("Unexpected profile repository call."));
        var user = new UserContext(new CurrentUser(userId), profiles);
        var controller = new PayPalSandboxController(
            new HostingEnvironment { EnvironmentName = Environments.Production },
            Options.Create(new PayPalSandboxOptions { Enabled = enabled, FrontendOrigin = frontendOrigin }),
            new EphemeralDataProtectionProvider());

        var availability = await controller.GetAvailability(user, client, default);
        var create = await controller.Create(user, client, default);
        if (!allowed)
        {
            Assert.IsType<NotFoundResult>(availability);
            Assert.IsType<NotFoundResult>(create);
            Assert.IsType<NotFoundResult>(await controller.Capture(new(), user, client, default));
            Assert.Empty(handler.CaptureKeys);
            return;
        }

        Assert.IsType<NoContentResult>(availability);
        var order = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(create).Value);
        var request = new PayPalSandboxController.CaptureRequest { Ticket = order.GetProperty("ticket").GetString()! };
        var captured = Assert.IsType<OkObjectResult>(await controller.Capture(request, user, client, default));
        Assert.Equal(new PayPalSandboxCapture("completed", "CAPTUREID"), captured.Value);
    }

    private static HttpClient CreateHttp(HttpMessageHandler handler) =>
        new(handler) { BaseAddress = new Uri("https://api-m.sandbox.paypal.com/") };

    private sealed class CurrentUser(Guid id) : ICurrentUser { public Guid? UserId => id; }

    private sealed class SandboxHandler(
        string currency, string amount, string status, string frontendOrigin = "http://localhost:5173") : HttpMessageHandler
    {
        public List<string> CaptureKeys { get; } = [];
        public int OrderLookups { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("api-m.sandbox.paypal.com", request.RequestUri!.Host);
            if (request.RequestUri.AbsolutePath == "/v1/oauth2/token")
            {
                Assert.Equal("Basic", request.Headers.Authorization?.Scheme);
                return JsonResponse("""{"access_token":"fixture-token"}""");
            }

            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            if (request.RequestUri.AbsolutePath == "/v2/checkout/orders")
            {
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
                var expected = body.RootElement.GetProperty("purchase_units")[0].GetProperty("amount");
                Assert.Equal("USD", expected.GetProperty("currency_code").GetString());
                Assert.Equal("1.00", expected.GetProperty("value").GetString());
                var experience = body.RootElement.GetProperty("payment_source").GetProperty("paypal").GetProperty("experience_context");
                Assert.Equal(frontendOrigin + "/payments/sandbox?paypal=return", experience.GetProperty("return_url").GetString());
                Assert.Equal(frontendOrigin + "/payments/sandbox?paypal=cancel", experience.GetProperty("cancel_url").GetString());
                return JsonResponse($$"""{"id":"{{OrderId}}","links":[{"rel":"payer-action","href":"https://www.sandbox.paypal.com/checkoutnow?token={{OrderId}}"}]}""");
            }

            if (request.Method == HttpMethod.Post)
            {
                Assert.Equal($"/v2/checkout/orders/{OrderId}/capture", request.RequestUri.AbsolutePath);
                CaptureKeys.Add(Assert.Single(request.Headers.GetValues("PayPal-Request-Id")));
                if (CaptureKeys.Count > 1)
                {
                    return new HttpResponseMessage(HttpStatusCode.UnprocessableEntity);
                }
            }
            else
            {
                Assert.Equal($"/v2/checkout/orders/{OrderId}", request.RequestUri.AbsolutePath);
                OrderLookups++;
            }

            return JsonResponse($$$"""{"id":"{{{OrderId}}}","status":"COMPLETED","purchase_units":[{"payments":{"captures":[{"id":"CAPTUREID","status":"{{{status}}}","amount":{"currency_code":"{{{currency}}}","value":"{{{amount}}}"}}]}}]}""");
        }

        private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }
}
