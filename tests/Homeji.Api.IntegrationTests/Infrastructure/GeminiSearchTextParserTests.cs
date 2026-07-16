using System.Net;
using System.Text.Json;
using Homeji.Infrastructure.External;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Homeji.Api.IntegrationTests.Infrastructure;

public sealed class GeminiSearchTextParserTests
{
    [Fact]
    public async Task ParseAsync_IncludesCurrentAndHistoricalLandmarkAliases()
    {
        var handler = new CapturingHttpMessageHandler();
        var parser = new GeminiSearchTextParser(
            new HttpClient(handler),
            Options.Create(new GeminiOptions
            {
                ApiKey = "test-key",
                TimeoutSeconds = 5,
            }),
            NullLogger<GeminiSearchTextParser>.Instance);

        await parser.ParseAsync("Tìm phòng gần FPT quận 9 hoặc NVHSV");

        var prompt = ExtractPrompt(handler.RequestBody!);
        Assert.Contains("FPTU HCM", prompt, StringComparison.Ordinal);
        Assert.Contains("Nhà Văn hóa Sinh viên ĐHQG", prompt, StringComparison.Ordinal);
        Assert.Contains("Quận 9", prompt, StringComparison.Ordinal);
        Assert.Contains("không tự đổi thành phường Thủ Đức", prompt, StringComparison.Ordinal);
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

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"candidates":[{"content":{"parts":[{"text":"{\"location\":\"Quận 9\",\"keyword\":\"FPTU\",\"price_min\":null,\"price_max\":null,\"area_min\":null,\"area_max\":null,\"criteria\":[]}"}]}}]}"""),
            };
        }
    }
}
