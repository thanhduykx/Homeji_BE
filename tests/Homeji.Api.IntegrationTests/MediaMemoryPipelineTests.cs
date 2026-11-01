using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Homeji.Application.DTOs.Chatbot;
using Homeji.Application.IServices.Chatbot;
using Homeji.Application.Services.Chatbot;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.External;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace Homeji.Api.IntegrationTests;

[Collection("NativeImageProcessing")]
public sealed class MediaMemoryPipelineTests
{
    [Fact]
    public async Task FullHttpPipeline_FiveImagesRejectsExcessLoadWithoutLosingHealth()
    {
        var jpeg = CreateFixture();
        var attachments = Enumerable.Range(0, 5).Select(_ => new ChatbotAttachmentDto("image/jpeg", Convert.ToBase64String(jpeg))).ToArray();
        var service = new ImageChatbot();
        using var factory = new HomejiApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("BackgroundJobs:Enabled", "false");
            builder.UseSetting("Database:ApplyMigrationsOnStartup", "false");
            builder.UseSetting("RateLimiting:CostlyOperations:PermitLimit", "1000");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IChatbotService>();
                services.AddSingleton<IChatbotService>(service);
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "MediaMemoryFixture";
                    options.DefaultChallengeScheme = "MediaMemoryFixture";
                }).AddScheme<AuthenticationSchemeOptions, FixtureAuthentication>("MediaMemoryFixture", _ => { });
            });
        });
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var succeeded = 0;
        var rejected = 0;
        for (var round = 0; round < 8; round++)
        {
            var requests = Enumerable.Range(0, 4).Select(async _ =>
            {
                using var response = await client.PostAsJsonAsync("/api/chatbot/messages", new { message = "Ảnh thử nghiệm", attachments });
                if (response.StatusCode == HttpStatusCode.OK) Interlocked.Increment(ref succeeded);
                else
                {
                    Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
                    Assert.NotNull(response.Headers.RetryAfter);
                    Interlocked.Increment(ref rejected);
                }
            });
            await Task.WhenAll(requests);
            using var health = await client.GetAsync("/health/live");
            Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        }
        Assert.True(succeeded >= 8);
        Assert.True(rejected > 0);
        Assert.Equal(32, succeeded + rejected);
        using var process = Process.GetCurrentProcess();
        var peakPath = "/sys/fs/cgroup/memory.peak";
        var maxPath = "/sys/fs/cgroup/memory.max";
        if (File.Exists(maxPath) && File.ReadAllText(maxPath).Trim() == "536870912")
            Assert.True(long.Parse(File.ReadAllText(peakPath).Trim(), CultureInfo.InvariantCulture) <= 536870912);
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
        {
            scenario = "full-http-pipeline-fixture", succeeded, rejected, process.PeakWorkingSet64,
            cgroupPeak = File.Exists(peakPath) ? File.ReadAllText(peakPath).Trim() : null,
            cgroupLimit = File.Exists(maxPath) ? File.ReadAllText(maxPath).Trim() : null,
        }));
    }

    private static byte[] CreateFixture()
    {
        return File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "large-20mp.jpg"));
    }

    private sealed class FixtureAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "media-fixture")], Scheme.Name)), Scheme.Name)));
    }

    private sealed class ImageChatbot : IChatbotService
    {
        public async Task<ChatbotReplyDto> SendMessageAsync(SendChatbotMessageDto request, CancellationToken cancellationToken = default)
        {
            var content = ChatbotMediaValidation.Decode(request.Attachments);
            Assert.Equal(5, content.Count);
            using var http = new HttpClient(new FixtureProvider());
            var ai = new GeminiChatbotClient(http, Options.Create(new GeminiOptions
            { ApiKey = "fixture", Endpoint = "https://fixture.invalid/generate", MaxRetryAttempts = 0 }),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<GeminiChatbotClient>.Instance, new ConversationImageProcessor());
            var text = await ai.GenerateMediaReplyAsync([], "Ảnh thử nghiệm", content, cancellationToken);
            var id = Guid.NewGuid();
            return new(id, new(Guid.NewGuid(), id, ChatMessageSender.User, request.Message!, DateTimeOffset.UtcNow),
                new(Guid.NewGuid(), id, ChatMessageSender.Assistant, text, DateTimeOffset.UtcNow), null, []);
        }
        public Task<string> TranscribeAsync(ChatbotTranscriptionDto request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteConversationAsync(Guid conversationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ChatbotPopupConfigDto> GetPopupConfigAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ChatbotConversationDto>> GetMyConversationsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ChatbotMessageDto>> GetMessagesAsync(Guid conversationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FixtureProvider : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await request.Content!.CopyToAsync(Stream.Null, cancellationToken);
            await Task.Delay(200, cancellationToken);
            return new(HttpStatusCode.OK) { Content = new StringContent("{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"Ảnh thử nghiệm\"}]}}]}") };
        }
    }
}
