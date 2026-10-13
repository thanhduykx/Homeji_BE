using System.Diagnostics;
using System.Net;
using Homeji.Infrastructure.External;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Homeji.Api.IntegrationTests.Infrastructure;

public sealed class GeminiDeadlineTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Retry_attempts_share_one_deadline(bool chatbot)
    {
        var handler = new SlowTransientHandler();
        var options = Options.Create(new GeminiOptions
        {
            ApiKey = "fixture", TimeoutSeconds = 5, RetryBaseDelayMilliseconds = 0,
        });
        var stopwatch = Stopwatch.StartNew();
        Task Call(CancellationToken token) => chatbot
            ? new GeminiChatbotClient(new HttpClient(handler), options, NullLogger<GeminiChatbotClient>.Instance)
                .GenerateReplyAsync([], "Explain HTTPS", token)
            : new GeminiSearchTextParser(new HttpClient(handler), options, NullLogger<GeminiSearchTextParser>.Instance)
                .ParseAsync("Phòng", token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Call(CancellationToken.None));
        Assert.InRange(stopwatch.Elapsed.TotalSeconds, 4, 7);
        Assert.Equal(2, handler.Calls);
    }

    private sealed class SlowTransientHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++;
            await Task.Delay(TimeSpan.FromSeconds(3), token);
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("{}") };
        }
    }
}
