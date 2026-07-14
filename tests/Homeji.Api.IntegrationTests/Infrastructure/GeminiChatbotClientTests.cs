using System.Net;
using System.Net.Http.Headers;
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

    private static HttpResponseMessage CreateRateLimitedResponse()
    {
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("""{"error":{"code":429,"status":"RESOURCE_EXHAUSTED"}}"""),
        };
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
        return response;
    }

    private sealed class SequenceHttpMessageHandler(params HttpResponseMessage[] responses)
        : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No fake Gemini response remains.");
            }

            return Task.FromResult(_responses.Dequeue());
        }
    }
}
