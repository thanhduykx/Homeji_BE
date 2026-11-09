using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Homeji.Api.Security;
using Homeji.Application.DTOs.Chatbot;
using Homeji.Application.IServices.Chatbot;
using Homeji.Domain.Enums;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Homeji.Api.IntegrationTests;

public sealed class ChatbotCaptchaTests
{
    private const string Host = "homeji.onrender.com";
    private const string ChallengeSecret = "fixture-checkbox-secret";
    private const string ScoreSecret = "fixture-score-secret";

    [Fact]
    public async Task Ordinary_use_never_contacts_Google_and_the_browser_gets_no_v3_key()
    {
        var google = new FakeSiteVerify();
        using var factory = Factory(google, challenge: true, score: true);
        using var client = Client(factory);

        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.OK, (await Send(client)).StatusCode);

        Assert.Equal(0, google.Calls);
        var config = await client.GetFromJsonAsync<ChatbotPopupConfigDto>(new Uri("/api/chatbot/popup-config", UriKind.Relative));
        Assert.Null(config!.RecaptchaSiteKey);
    }

    [Fact]
    public async Task The_request_past_the_burst_limit_asks_for_the_checkbox()
    {
        var google = new FakeSiteVerify();
        var chatbot = new CountingChatbot();
        using var factory = Factory(google, challenge: true, chatbot: chatbot);
        using var client = Client(factory);
        for (var i = 0; i < 5; i++) await Send(client);

        using var blocked = await Send(client);

        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        using var body = JsonDocument.Parse(await blocked.Content.ReadAsStringAsync());
        Assert.Equal("chatbot_captcha_required", body.RootElement.GetProperty("code").GetString());
        Assert.Equal("checkbox-site-key", body.RootElement.GetProperty("captchaSiteKey").GetString());
        Assert.Equal(5, chatbot.Messages);
        Assert.Equal(0, google.Calls);
    }

    [Fact]
    public async Task Passing_the_checkbox_lets_the_request_through_and_restarts_the_count()
    {
        var google = new FakeSiteVerify();
        var chatbot = new CountingChatbot();
        using var factory = Factory(google, challenge: true, chatbot: chatbot);
        using var client = Client(factory);
        for (var i = 0; i < 5; i++) await Send(client);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(client)).StatusCode);

        using var verified = await Send(client, token: "checkbox-token");

        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        Assert.Equal(1, google.Calls);
        Assert.Equal(ChallengeSecret, google.LastSecret);
        for (var i = 0; i < 4; i++)
            Assert.Equal(HttpStatusCode.OK, (await Send(client)).StatusCode);
        Assert.Equal(10, chatbot.Messages);
    }

    [Theory]
    [InlineData("""{"success":false,"error-codes":["invalid-input-response"]}""")]
    [InlineData("""{"success":true,"hostname":"evil.example"}""")]
    public async Task A_token_Google_does_not_accept_keeps_the_request_out(string verdict)
    {
        var google = new FakeSiteVerify { Body = verdict };
        var chatbot = new CountingChatbot();
        using var factory = Factory(google, challenge: true, chatbot: chatbot);
        using var client = Client(factory);
        for (var i = 0; i < 5; i++) await Send(client);

        using var response = await Send(client, token: "forged");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(5, chatbot.Messages);
    }

    [Fact]
    public async Task Google_being_down_answers_503_without_running_the_request()
    {
        var google = new FakeSiteVerify { Status = HttpStatusCode.InternalServerError };
        var chatbot = new CountingChatbot();
        using var factory = Factory(google, challenge: true, chatbot: chatbot);
        using var client = Client(factory);
        for (var i = 0; i < 5; i++) await Send(client);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Send(client, token: "checkbox-token")).StatusCode);
        Assert.Equal(5, chatbot.Messages);
    }

    [Fact]
    public async Task Each_user_has_a_separate_count_and_transcriptions_share_it()
    {
        var google = new FakeSiteVerify();
        using var factory = Factory(google, challenge: true);
        using var client = Client(factory);
        for (var i = 0; i < 4; i++) await Send(client);
        using var transcription = await client.PostAsJsonAsync(new Uri("/api/chatbot/transcriptions", UriKind.Relative),
            new { audio = new { mimeType = "audio/wav", data = "AQID" } });
        Assert.Equal(HttpStatusCode.OK, transcription.StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await Send(client)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, user: "22222222-2222-4222-8222-222222222222")).StatusCode);
    }

    [Fact]
    public async Task Without_checkbox_keys_every_request_still_needs_a_v3_token()
    {
        var google = new FakeSiteVerify { Body = $$"""{"success":true,"score":0.9,"action":"chatbot_message","hostname":"{{Host}}"}""" };
        using var factory = Factory(google, score: true);
        using var client = Client(factory);

        Assert.Equal(HttpStatusCode.Forbidden, (await Send(client)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, token: "score-token")).StatusCode);
        Assert.Equal(ScoreSecret, google.LastSecret);
        var config = await client.GetFromJsonAsync<ChatbotPopupConfigDto>(new Uri("/api/chatbot/popup-config", UriKind.Relative));
        Assert.Equal("score-site-key", config!.RecaptchaSiteKey);
    }

    [Fact]
    public void The_count_forgets_requests_older_than_the_window()
    {
        var clock = new ManualClock();
        var guard = new ChatbotBurstGuard(Options.Create(new ChatbotRecaptchaOptions { BurstLimit = 2, BurstWindowSeconds = 30 }), clock);

        Assert.False(guard.RecordAndCheck("user:a"));
        Assert.False(guard.RecordAndCheck("user:a"));
        Assert.True(guard.RecordAndCheck("user:a"));
        clock.Advance(TimeSpan.FromSeconds(31));
        Assert.False(guard.RecordAndCheck("user:a"));
    }

    [Fact]
    public void Continued_spam_stays_challenged_without_growing_the_history()
    {
        var clock = new ManualClock();
        var guard = new ChatbotBurstGuard(Options.Create(new ChatbotRecaptchaOptions { BurstLimit = 2, BurstWindowSeconds = 30 }), clock);
        for (var i = 0; i < 2; i++) guard.RecordAndCheck("user:a");

        for (var i = 0; i < 50; i++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(100));
            Assert.True(guard.RecordAndCheck("user:a"));
        }
    }

    [Theory]
    [InlineData("checkbox-site-key", null)]
    [InlineData(null, "checkbox-secret")]
    public void A_checkbox_key_without_its_pair_is_invalid(string? site, string? secret)
    {
        var options = new ChatbotRecaptchaOptions { ChallengeSiteKey = site, ChallengeSecretKey = secret, AllowedHostnames = [Host] };
        Assert.False(options.IsValid());
    }

    private static Task<HttpResponseMessage> Send(HttpClient client, string? token = null, string? user = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/chatbot/messages", UriKind.Relative))
        {
            Content = JsonContent.Create(new { message = "Phòng gần FPT" }),
        };
        if (token is not null) request.Headers.Add(ChatbotRecaptcha.TokenHeader, token);
        if (user is not null) request.Headers.Add(TestAuthentication.UserHeader, user);
        return client.SendAsync(request);
    }

    private static HttpClient Client(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    private static WebApplicationFactory<Program> Factory(FakeSiteVerify google, bool challenge = false, bool score = false, CountingChatbot? chatbot = null) =>
        new HomejiApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("BackgroundJobs:Enabled", "false");
            builder.UseSetting("Database:ApplyMigrationsOnStartup", "false");
            builder.UseSetting("RateLimiting:CostlyOperations:PermitLimit", "100");
            builder.UseSetting("Chatbot:Recaptcha:AllowedHostnames:0", Host);
            if (challenge)
            {
                builder.UseSetting("Chatbot:Recaptcha:ChallengeSiteKey", "checkbox-site-key");
                builder.UseSetting("Chatbot:Recaptcha:ChallengeSecretKey", ChallengeSecret);
            }
            if (score)
            {
                builder.UseSetting("Chatbot:Recaptcha:SiteKey", "score-site-key");
                builder.UseSetting("Chatbot:Recaptcha:SecretKey", ScoreSecret);
            }
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IChatbotService>();
                services.AddSingleton<IChatbotService>(chatbot ?? new CountingChatbot());
                services.AddHttpClient<ChatbotRecaptcha>().ConfigurePrimaryHttpMessageHandler(() => google);
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthentication.SchemeName;
                    options.DefaultChallengeScheme = TestAuthentication.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions, TestAuthentication>(TestAuthentication.SchemeName, _ => { });
            });
        });

    private sealed class FakeSiteVerify : HttpMessageHandler
    {
        private int _calls;
        public int Calls => _calls;
        public string? LastSecret { get; private set; }
        public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;
        public string Body { get; init; } = $$"""{"success":true,"hostname":"{{Host}}"}""";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            var form = await request.Content!.ReadAsStringAsync(cancellationToken);
            LastSecret = Uri.UnescapeDataString(form.Split('&').First(part => part.StartsWith("secret=", StringComparison.Ordinal))["secret=".Length..]);
            return new HttpResponseMessage(Status) { Content = new StringContent(Body, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 10, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }

    private sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "ChatbotCaptchaTest";
        public const string UserHeader = "X-Test-User";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var user = Request.Headers[UserHeader].ToString();
            if (string.IsNullOrEmpty(user)) user = "11111111-1111-4111-8111-111111111111";
            var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", user)], SchemeName));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
        }
    }

    private sealed class CountingChatbot : IChatbotService
    {
        private int _messages;
        public int Messages => _messages;

        public Task<ChatbotReplyDto> SendMessageAsync(SendChatbotMessageDto request, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _messages);
            var id = Guid.NewGuid();
            return Task.FromResult(new ChatbotReplyDto(id,
                new(Guid.NewGuid(), id, ChatMessageSender.User, request.Message!, DateTimeOffset.UtcNow),
                new(Guid.NewGuid(), id, ChatMessageSender.Assistant, "Reply", DateTimeOffset.UtcNow), null, []));
        }

        public Task<string> TranscribeAsync(ChatbotTranscriptionDto request, CancellationToken cancellationToken = default) =>
            Task.FromResult("Bản chép lời kiểm thử.");

        public Task<ChatbotPopupConfigDto> GetPopupConfigAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatbotPopupConfigDto(true, "Homeji", "Xin chào", []));

        public Task DeleteConversationAsync(Guid conversationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ChatbotConversationDto>> GetMyConversationsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ChatbotMessageDto>> GetMessagesAsync(Guid conversationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
