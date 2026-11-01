using System.Net;
using System.Text.Json;
using Homeji.Application.Common.Exceptions;
using Homeji.Infrastructure.External;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace Homeji.Api.IntegrationTests.Infrastructure;

[Collection("NativeImageProcessing")]
public sealed class MultimodalModerationTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("http://res.cloudinary.com/homeji/image/upload/a.jpg")]
    [InlineData("https://res.cloudinary.com.evil.test/homeji/image/upload/a.jpg")]
    [InlineData("https://127.0.0.1/homeji/image/upload/a.jpg")]
    [InlineData("https://res.cloudinary.com/other/image/upload/a.jpg")]
    [InlineData("https://res.cloudinary.com/homeji/image/fetch/https://127.0.0.1/a")]
    [InlineData("https://res.cloudinary.com/homeji/image/upload/a.jpg?secret=value")]
    public async Task ImageReader_NeverFetchesUnmanagedUrls(string url)
    {
        var calls = 0;
        using var http = new HttpClient(new Handler((_, _) => { calls++; return Task.FromResult(Image()); }));
        var reader = Reader(http);
        Assert.Null(await reader.ReadAsync(url, default));
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task ImageReader_RejectsOversizeWithoutTrustingMimeOrLength()
    {
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(Image(new byte[ModerationImageReader.MaxImageBytes + 1]))));
        Assert.Null(await Reader(http).ReadAsync(Url(0), default));
    }

    [Fact]
    public async Task Review_SendsEveryImageInBatchesOfFiveAndAggregatesMismatch()
    {
        var imagesRead = 0;
        using var imageHttp = new HttpClient(new Handler((request, _) =>
        {
            Assert.Contains("c_limit,w_1024,h_1024", request.RequestUri!.AbsolutePath, StringComparison.Ordinal);
            imagesRead++; return Task.FromResult(Image());
        }));
        var batchSizes = new List<int>();
        using var aiHttp = new HttpClient(new Handler(async (request, ct) =>
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Assert.True(body.RootElement.TryGetProperty("systemInstruction", out _));
            Assert.True(body.RootElement.GetProperty("generationConfig").TryGetProperty("responseSchema", out _));
            Assert.Equal(GeminiListingReviewClient.MaxOutputTokens, body.RootElement.GetProperty("generationConfig").GetProperty("maxOutputTokens").GetInt32());
            var parts = body.RootElement.GetProperty("contents")[0].GetProperty("parts");
            batchSizes.Add(parts.EnumerateArray().Count(part => part.TryGetProperty("inlineData", out _)));
            return Reply(batchSizes.Count == 1 ? "clear" : "review", batchSizes.Count == 1 ? "none" : "image_mismatch");
        }));
        var client = Client(aiHttp, Reader(imageHttp));
        var verdict = await client.ReviewAsync(Snapshot(10), default);
        Assert.Equal(10, imagesRead);
        Assert.Collection(batchSizes, count => Assert.Equal(5, count), count => Assert.Equal(5, count));
        Assert.Equal("review", verdict.Decision);
        Assert.Equal("image_mismatch", verdict.Category);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task MissingOrUnreadableImage_CannotBecomeClear(int count)
    {
        using var imageHttp = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound))));
        using var aiHttp = new HttpClient(new Handler((_, _) => Task.FromResult(Reply("clear", "none"))));
        var result = await Client(aiHttp, Reader(imageHttp)).ReviewAsync(Snapshot(count), default);
        Assert.Equal("review", result.Decision);
    }

    [Fact]
    public async Task Gemini3_UsesBoundedReasoningAndLeavesBudgetForVerdict()
    {
        using var imageHttp = new HttpClient(new Handler((_, _) => Task.FromResult(Image())));
        using var aiHttp = new HttpClient(new Handler(async (request, ct) =>
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var generation = body.RootElement.GetProperty("generationConfig");
            Assert.Equal("low", generation.GetProperty("thinkingConfig").GetProperty("thinkingLevel").GetString());
            Assert.Equal(2048, generation.GetProperty("maxOutputTokens").GetInt32());
            return Reply("clear", "none");
        }));
        var client = new GeminiListingReviewClient(aiHttp,
            Options.Create(new GeminiOptions { ApiKey = "fixture", Endpoint = "https://fixture.invalid/models/gemini-3.5-flash:generateContent" }), Reader(imageHttp));
        Assert.Equal("clear", (await client.ReviewAsync(Snapshot(1), default)).Decision);
    }

    [Fact]
    public async Task LowConfidenceBatch_CannotBeHiddenByAnotherClearBatch()
    {
        var calls = 0;
        using var imageHttp = new HttpClient(new Handler((_, _) => Task.FromResult(Image())));
        using var aiHttp = new HttpClient(new Handler((_, _) => Task.FromResult(Reply("clear", "none", ++calls == 1 ? .99 : .3))));
        var result = await Client(aiHttp, Reader(imageHttp)).ReviewAsync(Snapshot(6), default);
        Assert.Equal("review", result.Decision);
    }

    [Fact]
    public async Task SafetyBlockedOrInvalidProviderOutput_IsNotApproval()
    {
        using var imageHttp = new HttpClient(new Handler((_, _) => Task.FromResult(Image())));
        using var aiHttp = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent("{\"promptFeedback\":{\"blockReason\":\"SAFETY\"}}") })));
        await Assert.ThrowsAsync<ExternalServiceUnavailableException>(() => Client(aiHttp, Reader(imageHttp)).ReviewAsync(Snapshot(1), default));
    }

    private static string Url(int index) => $"https://res.cloudinary.com/homeji/image/upload/v1/photo-{index}.jpg";
    private static string Snapshot(int count) => JsonSerializer.Serialize(new
    { title = "Bàn học", description = "Bàn gỗ", images = Enumerable.Range(0, count).Select(Url).ToArray() }, Json);
    private static ModerationImageReader Reader(HttpClient http) => new(http, Options.Create(new CloudinaryOptions { CloudName = "homeji" }), new ConversationImageProcessor());
    private static GeminiListingReviewClient Client(HttpClient http, ModerationImageReader reader) => new(http,
        Options.Create(new GeminiOptions { ApiKey = "fixture", Endpoint = "https://fixture.invalid" }), reader);
    private static HttpResponseMessage Image(byte[]? bytes = null)
    {
        if (bytes is null)
        {
            using var bitmap = new SKBitmap(2, 2);
            bitmap.Erase(SKColors.Blue);
            using var image = SKImage.FromBitmap(bitmap);
            using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 80);
            bytes = encoded.ToArray();
        }
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
        response.Content.Headers.ContentType = new("image/jpeg"); return response;
    }
    private static HttpResponseMessage Reply(string decision, string category, double confidence = .99)
    {
        var verdict = JsonSerializer.Serialize(new { decision, category, reason = "Cần đối chiếu ảnh", evidence = "Ảnh 6", confidence }, Json);
        return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new
        { candidates = new[] { new { finishReason = "STOP", content = new { parts = new[] { new { text = verdict } } } } } }, Json)) };
    }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> action) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => action(request, cancellationToken);
    }
}
