using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Homeji.Application.Common.Exceptions;
using Homeji.Infrastructure.External;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Homeji.Api.IntegrationTests.Infrastructure;

public sealed class GeminiChatbotClientTests
{
    [Fact]
    public async Task GenerateReplyAsync_WhenGeminiTemporarilyRateLimits_RetriesAndReturnsReply()
    {
        var rateLimited = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("""{"error":{"code":429,"status":"RESOURCE_EXHAUSTED"}}"""),
        };
        rateLimited.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
        var handler = new SequenceHttpMessageHandler(
            rateLimited,
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"candidates":[{"content":{"parts":[{"text":"Chatbot đang hoạt động."}]}}]}"""),
            });
        var client = new GeminiChatbotClient(
            new HttpClient(handler),
            Options.Create(new GeminiOptions
            {
                ApiKey = "test-key",
                TimeoutSeconds = 5,
            }),
            NullLogger<GeminiChatbotClient>.Instance);

        var reply = await client.GenerateReplyAsync([], "Xin chào");

        Assert.Equal("Chatbot đang hoạt động.", reply);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task GenerateReplyAsync_WhenGeminiKeepsRateLimiting_ThrowsServiceUnavailable()
    {
        var handler = new SequenceHttpMessageHandler(
            CreateRateLimitedResponse(),
            CreateRateLimitedResponse(),
            CreateRateLimitedResponse());
        var client = new GeminiChatbotClient(
            new HttpClient(handler),
            Options.Create(new GeminiOptions
            {
                ApiKey = "test-key",
                TimeoutSeconds = 5,
            }),
            NullLogger<GeminiChatbotClient>.Instance);

        var exception = await Assert.ThrowsAsync<ExternalServiceUnavailableException>(
            () => client.GenerateReplyAsync([], "Xin chào"));

        Assert.Equal("Gemini", exception.ServiceName);
        Assert.Equal(
            "Chatbot AI quota is temporarily exhausted. Please try again later.",
            exception.Message);
        Assert.Equal(3, handler.RequestCount);
    }

    [Fact]
    public async Task GenerateReplyAsync_IncludesVerifiedThuDucLandmarkKnowledge()
    {
        var handler = new SequenceHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"candidates":[{"content":{"parts":[{"text":"Đã hiểu khu vực."}]}}]}"""),
            });
        var client = new GeminiChatbotClient(
            new HttpClient(handler),
            Options.Create(new GeminiOptions
            {
                ApiKey = "test-key",
                TimeoutSeconds = 5,
            }),
            NullLogger<GeminiChatbotClient>.Instance);

        await client.GenerateReplyAsync([], "Tìm trọ gần FPTU hoặc Nhà Văn hóa Sinh viên");

        var prompt = ExtractPrompt(handler.RequestBodies.Single());
        Assert.Contains("Quận 9 cũ", prompt, StringComparison.Ordinal);
        Assert.Contains("phường Tăng Nhơn Phú", prompt, StringComparison.Ordinal);
        Assert.Contains("01 Lưu Hữu Phước", prompt, StringComparison.Ordinal);
        Assert.Contains("hai mốc độc lập", prompt, StringComparison.Ordinal);
    }

    private static HttpResponseMessage CreateRateLimitedResponse()
    {
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("""{"error":{"code":429,"status":"RESOURCE_EXHAUSTED"}}"""),
        };
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
        return response;
    }

    private static string ExtractPrompt(string requestBody)
    {
        using var document = JsonDocument.Parse(requestBody);
        return document.RootElement
            .GetProperty("contents")[0]
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString()!;
    }

    private sealed class SequenceHttpMessageHandler(params HttpResponseMessage[] responses)
        : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public int RequestCount { get; private set; }
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No fake Gemini response remains.");
            }

            return _responses.Dequeue();
        }
    }
}
