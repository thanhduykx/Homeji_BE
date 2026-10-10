using System.Net;
using Homeji.Application.Common.Exceptions;
using Homeji.Infrastructure.External;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Homeji.Api.IntegrationTests.Infrastructure;

public sealed class GeminiSearchTextParserTests
{
    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task TransientFailure_RetriesAndParsesProviderReply(HttpStatusCode status)
    {
        var handler = new Handler(status, HttpStatusCode.OK);
        var result = await Client(handler).ParseAsync("Phòng dưới 4 triệu");
        Assert.Equal(4_000_000, result.PriceMax);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task InvalidCredential_IsNotRetried()
    {
        var handler = new Handler(HttpStatusCode.Unauthorized);
        await Assert.ThrowsAsync<ExternalDependencyException>(() => Client(handler).ParseAsync("Phòng"));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task PersistentFailure_StopsAtConfiguredLimit()
    {
        var handler = new Handler(HttpStatusCode.ServiceUnavailable, HttpStatusCode.ServiceUnavailable);
        await Assert.ThrowsAsync<ExternalDependencyException>(() => Client(handler).ParseAsync("Phòng"));
        Assert.Equal(2, handler.Calls);
    }

    private static GeminiSearchTextParser Client(Handler handler) => new(new HttpClient(handler),
        Options.Create(new GeminiOptions { ApiKey = "test-key", MaxRetryAttempts = 1, RetryBaseDelayMilliseconds = 0 }),
        NullLogger<GeminiSearchTextParser>.Instance);

    private sealed class Handler(params HttpStatusCode[] statuses) : HttpMessageHandler
    {
        private readonly Queue<HttpStatusCode> _statuses = new(statuses);
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            var status = _statuses.Dequeue();
            var body = System.Text.Json.JsonSerializer.Serialize(new { candidates = new[] {
                new { content = new { parts = new[] { new { text = "{\"price_max\":4000000,\"criteria\":[]}" } } } }
            } });
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }
}
