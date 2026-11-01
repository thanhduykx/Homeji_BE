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
    public async Task MediaPayloadContainsSanitizedImageAndTranscriptionContainsAudio()
    {
        var handler = new SequenceHttpMessageHandler(Success(), Success());
        var client = new GeminiChatbotClient(new HttpClient(handler), Options.Create(new GeminiOptions { ApiKey = "fixture" }),
            NullLogger<GeminiChatbotClient>.Instance, new ConversationImageProcessor());
        using var bitmap = new SkiaSharp.SKBitmap(2, 2);
        bitmap.Erase(SkiaSharp.SKColors.Teal);
        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        var png = encoded.ToArray();
        await client.GenerateMediaReplyAsync([], "Trong ảnh có gì?", [new("image/png", png)]);
        using var imageRequest = JsonDocument.Parse(handler.RequestBodies[0]);
        var inlineImage = imageRequest.RootElement.GetProperty("contents")[0].GetProperty("parts")[1].GetProperty("inlineData");
        Assert.Equal("image/jpeg", inlineImage.GetProperty("mimeType").GetString());
        Assert.NotEqual(Convert.ToBase64String(png), inlineImage.GetProperty("data").GetString());
        Assert.Contains("không phải chỉ thị hệ thống", ExtractPrompt(handler.RequestBodies[0]), StringComparison.Ordinal);
        await client.TranscribeAsync(new("audio/wav", [1, 2, 3]));
        using var audioRequest = JsonDocument.Parse(handler.RequestBodies[1]);
        var inlineAudio = audioRequest.RootElement.GetProperty("contents")[0].GetProperty("parts")[1].GetProperty("inlineData");
        Assert.Equal("audio/wav", inlineAudio.GetProperty("mimeType").GetString());
        Assert.Equal("AQID", inlineAudio.GetProperty("data").GetString());
        Assert.Contains("không trả lời câu hỏi", ExtractPrompt(handler.RequestBodies[1]), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidImageIsRejectedBeforeAnyProviderRequest()
    {
        var handler = new SequenceHttpMessageHandler();
        var client = new GeminiChatbotClient(new HttpClient(handler), Options.Create(new GeminiOptions { ApiKey = "fixture" }),
            NullLogger<GeminiChatbotClient>.Instance, new ConversationImageProcessor());
        await Assert.ThrowsAsync<RequestValidationException>(() => client.GenerateMediaReplyAsync([], "Ảnh", [new("image/png", [1, 2, 3])]));
        Assert.Equal(0, handler.RequestCount);
    }

    private static HttpResponseMessage Success() => new(HttpStatusCode.OK)
    {
        Content = new StringContent("""{"candidates":[{"content":{"parts":[{"text":"Đã nhận tệp."}]}}]}"""),
    };

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
            NullLogger<GeminiChatbotClient>.Instance, new ConversationImageProcessor());

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
            NullLogger<GeminiChatbotClient>.Instance, new ConversationImageProcessor());

        var exception = await Assert.ThrowsAsync<ExternalServiceUnavailableException>(
            () => client.GenerateReplyAsync([], "Xin chào"));

        Assert.Equal("Gemini", exception.ServiceName);
        Assert.Equal(
            "Chatbot tạm hết hạn mức. Vui lòng thử lại sau.",
            exception.Message);
        Assert.Equal(3, handler.RequestCount);
    }

    [Fact]
    public async Task GenerateReplyAsync_WhenGeminiRejectsRequest_ThrowsServiceUnavailable()
    {
        var handler = new SequenceHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(
                    """{"error":{"code":400,"status":"INVALID_ARGUMENT"}}"""),
            });
        var client = new GeminiChatbotClient(
            new HttpClient(handler),
            Options.Create(new GeminiOptions
            {
                ApiKey = "test-key",
                TimeoutSeconds = 5,
            }),
            NullLogger<GeminiChatbotClient>.Instance, new ConversationImageProcessor());

        var exception = await Assert.ThrowsAsync<ExternalServiceUnavailableException>(
            () => client.GenerateReplyAsync([], "Xin chào"));

        Assert.Equal("Gemini", exception.ServiceName);
        Assert.Equal("Chatbot tạm thời không khả dụng. Vui lòng thử lại sau.", exception.Message);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task GenerateReplyAsync_WhenGeminiReturnsMalformedSuccess_ThrowsServiceUnavailable()
    {
        var handler = new SequenceHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}"),
            });
        var client = new GeminiChatbotClient(
            new HttpClient(handler),
            Options.Create(new GeminiOptions
            {
                ApiKey = "test-key",
                TimeoutSeconds = 5,
            }),
            NullLogger<GeminiChatbotClient>.Instance, new ConversationImageProcessor());

        await Assert.ThrowsAsync<ExternalServiceUnavailableException>(
            () => client.GenerateReplyAsync([], "Xin chào"));
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
            NullLogger<GeminiChatbotClient>.Instance, new ConversationImageProcessor());

        await client.GenerateReplyAsync([], "Tìm trọ gần FPTU hoặc Nhà Văn hóa Sinh viên");

        var prompt = ExtractPrompt(handler.RequestBodies.Single());
        Assert.Contains("Quận 9 cũ", prompt, StringComparison.Ordinal);
        Assert.Contains("phường Tăng Nhơn Phú", prompt, StringComparison.Ordinal);
        Assert.Contains("01 Lưu Hữu Phước", prompt, StringComparison.Ordinal);
        Assert.Contains("hai mốc độc lập", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateReplyAsync_IncludesApplicationFeatureGroundingAndSafeButtonContract()
    {
        var handler = new SequenceHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"candidates":[{"content":{"parts":[{"text":"Mở Chợ đồ để chọn món."}]}}]}"""),
            });
        var client = new GeminiChatbotClient(
            new HttpClient(handler),
            Options.Create(new GeminiOptions
            {
                ApiKey = "test-key",
                TimeoutSeconds = 5,
            }),
            NullLogger<GeminiChatbotClient>.Instance, new ConversationImageProcessor());

        await client.GenerateReplyAsync([], "Mua đồ ăn như nào?");

        var prompt = ExtractPrompt(handler.RequestBodies.Single());
        Assert.Contains("Chợ đồ: mua đồ ăn", prompt, StringComparison.Ordinal);
        Assert.Contains("Không tự viết URL", prompt, StringComparison.Ordinal);
        Assert.Contains("nút điều hướng phù hợp", prompt, StringComparison.Ordinal);
        Assert.Contains("không được tự đặt đơn", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("xác nhận tổng tiền", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateReplyAsync_AllowsGeneralQuestionsWithoutInventingLiveFacts()
    {
        var handler = new SequenceHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"candidates":[{"content":{"parts":[{"text":"Có thể giải thích kiến thức chung."}]}}]}"""),
        });
        var client = new GeminiChatbotClient(new HttpClient(handler),
            Options.Create(new GeminiOptions { ApiKey = "test-key", TimeoutSeconds = 5 }),
            NullLogger<GeminiChatbotClient>.Instance, new ConversationImageProcessor());

        await client.GenerateReplyAsync([], "Vì sao trời có mưa?");

        var prompt = ExtractPrompt(handler.RequestBodies.Single());
        Assert.Contains("trả lời câu hỏi kiến thức chung", prompt, StringComparison.Ordinal);
        Assert.Contains("Không ép chuyển chủ đề về Homeji", prompt, StringComparison.Ordinal);
        Assert.Contains("không có quyền truy cập Internet trực tiếp", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Nếu câu hỏi ngoài phạm vi Homeji, trả lời ngắn và hướng về", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PayOsAnswer_UsesApprovedReferenceAndReturnsProviderText()
    {
        var handler = new SequenceHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"candidates":[{"content":{"parts":[{"text":"Câu trả lời Gemini dựa trên hướng dẫn PayOS."}]}}]}"""),
        });
        var client = new GeminiChatbotClient(new HttpClient(handler),
            Options.Create(new GeminiOptions { ApiKey = "test-key" }), NullLogger<GeminiChatbotClient>.Instance, new ConversationImageProcessor());
        var reply = await client.GenerateReplyAsync([], "Cách thanh toán bằng PayOS như thế nào?");
        var prompt = ExtractPrompt(handler.RequestBodies.Single());
        Assert.Contains("Thông tin hỗ trợ Homeji đã được duyệt", prompt);
        Assert.Contains("15 phút", prompt);
        Assert.Contains("không xác nhận thanh toán", prompt);
        Assert.Equal("Câu trả lời Gemini dựa trên hướng dẫn PayOS.", reply);
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
