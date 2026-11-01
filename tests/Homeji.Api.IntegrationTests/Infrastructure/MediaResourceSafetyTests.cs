using System.Net;
using System.Text;
using System.Text.Json;
using Homeji.Api.Middlewares;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Conversations;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.External;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace Homeji.Api.IntegrationTests.Infrastructure;

[Collection("NativeImageProcessing")]
public sealed class MediaResourceSafetyTests
{
    [Fact]
    public async Task ImageBudget_BoundsWaitersAndCancellationReleasesQueue()
    {
        using var budget = new ImageProcessingBudget();
        using var owner = await budget.EnterAsync(default);
        using var cancellation = new CancellationTokenSource();
        var first = budget.EnterAsync(cancellation.Token);
        var second = budget.EnterAsync(cancellation.Token);
        await Assert.ThrowsAsync<ExternalServiceUnavailableException>(() => budget.EnterAsync(default));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second);
        owner.Dispose();
        using var recovered = await budget.EnterAsync(default);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LargeImage_IsDownsampledBeforeAllocationOrRejected(bool scalable)
    {
        var bytes = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", scalable ? "large-20mp.jpg" : "large-20mp.png"));
        var upload = new ConversationImageUpload("large", scalable ? "image/jpeg" : "image/png", bytes, MessageAttachmentContext.Other);
        var processor = new ConversationImageProcessor();
        if (scalable)
        {
            var output = await processor.ProcessAsync(upload);
            Assert.InRange(output.Width, 1, 2048);
            Assert.InRange(output.Height, 1, 2048);
        }
        else await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync(upload));
    }

    [Fact]
    public async Task Admission_RejectsBeforeReadingSecondRequestBodyAndRecovers()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var middleware = new MediaAdmissionMiddleware(async _ => { entered.TrySetResult(); await release.Task; });
        DefaultHttpContext Context()
        {
            var context = new DefaultHttpContext();
            context.Request.Method = "POST";
            context.Request.Path = "/api/chatbot/messages";
            context.Request.Body = new NeverReadStream();
            context.Response.Body = new MemoryStream();
            return context;
        }
        var first = middleware.InvokeAsync(Context());
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var second = Context();
        await middleware.InvokeAsync(second);
        Assert.Equal(429, second.Response.StatusCode);
        Assert.Equal("5", second.Response.Headers.RetryAfter);
        release.TrySetResult(); await first;
        var third = Context(); await middleware.InvokeAsync(third);
        Assert.Equal(200, third.Response.StatusCode);
    }

    [Fact]
    public async Task ProviderBudget_IsGlobalAndRejectsThirdInFlightRequest()
    {
        using var budget = new AiProviderBudget();
        var handler = new HoldHandler();
        using var http = new HttpClient(new AiProviderBudgetHandler(budget) { InnerHandler = handler });
        var first = http.GetAsync("https://fixture.invalid/one");
        var second = http.GetAsync("https://fixture.invalid/two");
        await handler.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Assert.ThrowsAsync<ExternalServiceUnavailableException>(() => http.GetAsync("https://fixture.invalid/three"));
        handler.Release.TrySetResult();
        using var firstResponse = await first; using var secondResponse = await second;
        using var third = await http.GetAsync("https://fixture.invalid/recovered");
        Assert.Equal(HttpStatusCode.OK, third.StatusCode);
    }

    private sealed class HoldHandler : HttpMessageHandler
    {
        private int _count;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _count) >= 2) Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return new(HttpStatusCode.OK) { Content = new StringContent("{}") };
        }
    }

    private sealed class NeverReadStream : MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Request body must not be read before admission.");
    }
}

[CollectionDefinition("NativeImageProcessing", DisableParallelization = true)]
public sealed class NativeImageProcessingGroup;
