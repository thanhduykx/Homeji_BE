using System.Net;
using System.Text.Json;
using Homeji.Infrastructure.External;
using Homeji.Infrastructure;
using Homeji.Application.IServices.Emails;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Homeji.Api.IntegrationTests.Infrastructure;

public sealed class BrevoAccountEmailSenderTests
{
    [Theory]
    [InlineData("Brevo", typeof(BrevoAccountEmailSender))]
    [InlineData("Smtp", typeof(SmtpAccountEmailSender))]
    public void Configuration_SelectsExpectedSender(string provider, Type expectedType)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=unused",
            ["Email:Provider"] = provider,
        };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        using var container = services.BuildServiceProvider();
        Assert.IsType(expectedType, container.GetRequiredService<IAccountEmailSender>());
        if (provider == "Brevo")
        {
            Assert.Throws<OptionsValidationException>(() => container.GetRequiredService<IOptions<BrevoOptions>>().Value);
            Assert.Throws<OptionsValidationException>(() => container.GetRequiredService<IOptions<SupaBaseAuthOptions>>().Value);
        }
    }

    [Fact]
    public async Task Registration_UsesHttpsApiAndEscapesUntrustedContent()
    {
        using var handler = new RecordingHandler(HttpStatusCode.Created);
        using var client = new HttpClient(handler);
        var sender = CreateSender(client);
        var result = await sender.SendRegistrationConfirmationAsync(
            "user@example.com", "<script>alert(1)</script>", "https://project.supabase.co/verify?a=1&b=2");

        Assert.True(result.Sent);
        Assert.Equal("https://api.brevo.com/v3/smtp/email", handler.Url);
        Assert.Equal("test-brevo-key", handler.ApiKey);
        using var body = JsonDocument.Parse(handler.Body!);
        Assert.Equal("sender@example.com", body.RootElement.GetProperty("sender").GetProperty("email").GetString());
        Assert.Equal("user@example.com", body.RootElement.GetProperty("to")[0].GetProperty("email").GetString());
        var html = body.RootElement.GetProperty("htmlContent").GetString()!;
        Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
        Assert.Contains("a=1&amp;b=2", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task ProviderFailure_IsReturnedWithoutRetry(HttpStatusCode status)
    {
        using var handler = new RecordingHandler(status);
        using var client = new HttpClient(handler);
        var result = await CreateSender(client).SendPasswordResetAsync("user@example.com", "https://example.com/reset");
        Assert.False(result.Sent);
        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public async Task MissingApiKey_DoesNotCallProvider()
    {
        using var handler = new RecordingHandler(HttpStatusCode.Created);
        using var client = new HttpClient(handler);
        var sender = new BrevoAccountEmailSender(client, Options.Create(new BrevoOptions()), NullLogger<BrevoAccountEmailSender>.Instance);
        Assert.False((await sender.SendPasswordResetAsync("user@example.com", "https://example.com/reset")).Sent);
        Assert.Equal(0, handler.Count);
    }

    [Fact]
    public async Task CallerCancellation_Propagates()
    {
        using var handler = new RecordingHandler(HttpStatusCode.Created, cancel: true);
        using var client = new HttpClient(handler);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateSender(client)
            .SendPasswordResetAsync("user@example.com", "https://example.com/reset", cancellation.Token));
    }

    [Fact]
    public async Task NetworkTimeout_ReturnsFailure()
    {
        using var handler = new RecordingHandler(HttpStatusCode.Created, cancel: true);
        using var client = new HttpClient(handler);
        Assert.False((await CreateSender(client).SendPasswordResetAsync("user@example.com", "https://example.com/reset")).Sent);
    }

    private static BrevoAccountEmailSender CreateSender(HttpClient client) => new(client,
        Options.Create(new BrevoOptions { ApiKey = "test-brevo-key", FromEmail = "sender@example.com" }),
        NullLogger<BrevoAccountEmailSender>.Instance);

    [Fact]
    public async Task Receipt_uses_stable_json_idempotency_key_and_duplicate_is_accepted()
    {
        using var handler = new DuplicateHandler();
        using var client = new HttpClient(handler);
        var key = Guid.NewGuid();
        var result = await CreateSender(client).SendAsync("user@example.com", "Receipt", "<p>Test</p>", "Test", key, CancellationToken.None);
        Assert.True(result.Sent);
        Assert.Equal(key.ToString("D"), handler.Key);
    }

    private sealed class DuplicateHandler : HttpMessageHandler
    {
        public string? Key { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            Key = json.RootElement.GetProperty("headers").GetProperty("idempotencyKey").GetString();
            return new HttpResponseMessage(HttpStatusCode.BadRequest)
            { Content = new StringContent("{\"code\":\"duplicate_parameter\",\"message\":\"Duplicate idempotency key\"}") };
        }
    }

    [Fact]
    public async Task Receipt_attachment_preserves_utf8_html()
    {
        using var handler = new RecordingHandler(HttpStatusCode.Created);
        using var client = new HttpClient(handler);
        const string html = "<html><meta charset=\"utf-8\"><p>Biên nhận nạp tiền</p></html>";
        var attachment = new Homeji.Application.DTOs.Emails.EmailAttachmentDto("Homeji.html", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(html)));
        var pdf = System.Text.Encoding.ASCII.GetBytes("%PDF-1.7\nserializer-fixture\n%%EOF");
        var pdfAttachment = new Homeji.Application.DTOs.Emails.EmailAttachmentDto("Homeji.pdf", Convert.ToBase64String(pdf));
        await CreateSender(client).SendAsync("user@example.com", "Receipt", html, "Receipt", Guid.NewGuid(), CancellationToken.None, [attachment, pdfAttachment]);
        using var json = JsonDocument.Parse(handler.Body!);
        var attached = json.RootElement.GetProperty("attachment")[0];
        Assert.Equal("Homeji.html", attached.GetProperty("name").GetString());
        Assert.Equal(html, System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(attached.GetProperty("content").GetString()!)));
        Assert.Equal(2, json.RootElement.GetProperty("attachment").GetArrayLength());
        Assert.Equal(pdf, Convert.FromBase64String(json.RootElement.GetProperty("attachment")[1].GetProperty("content").GetString()!));
    }

    private sealed class RecordingHandler(HttpStatusCode status, bool cancel = false) : HttpMessageHandler
    {
        public string? Url { get; private set; }
        public string? ApiKey { get; private set; }
        public string? Body { get; private set; }
        public int Count { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (cancel)
            {
                throw new OperationCanceledException();
            }

            Count++;
            Url = request.RequestUri!.AbsoluteUri;
            ApiKey = request.Headers.GetValues("api-key").Single();
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent("{\"messageId\":\"test-id\"}") };
        }
    }
}
