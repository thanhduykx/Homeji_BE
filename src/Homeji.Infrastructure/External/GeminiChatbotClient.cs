using System.Net.Http.Json;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Globalization;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Chatbot;
using Homeji.Application.IServices.Chatbot;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Homeji.Infrastructure.External;

public sealed class GeminiChatbotClient : IChatbotAiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Action<ILogger, int, int, int, Exception?> LogGeminiChatFailed =
        LoggerMessage.Define<int, int, int>(
            LogLevel.Warning,
            new EventId(2002, nameof(LogGeminiChatFailed)),
            "Gemini chatbot request failed with status {StatusCode} on attempt {Attempt}/{MaxAttempts}.");

    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiChatbotClient> _logger;

    public GeminiChatbotClient(
        HttpClient httpClient,
        IOptions<GeminiOptions> options,
        ILogger<GeminiChatbotClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        if (_options.TimeoutSeconds > 0)
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 5, 120));
        }
    }

    public async Task<string> GenerateReplyAsync(
        IReadOnlyCollection<ChatbotMessageDto> conversationMessages,
        string latestUserMessage,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var payload = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new
                        {
                            text = BuildPrompt(conversationMessages, latestUserMessage),
                        },
                    },
                },
            },
        };

        var maxAttempts = Math.Clamp(_options.MaxRetryAttempts + 1, 1, 4);
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint)
            {
                Content = JsonContent.Create(payload, options: JsonOptions),
            };
            request.Headers.Add("X-goog-api-key", _options.ApiKey);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return NormalizeReply(ExtractModelText(responseText));
            }

            LogGeminiChatFailed(_logger, (int)response.StatusCode, attempt, maxAttempts, null);
            var retryAfter = GetRetryDelay(response, responseText, attempt);
            if (IsRetryable(response.StatusCode) && attempt < maxAttempts)
            {
                if (retryAfter > TimeSpan.Zero)
                {
                    await Task.Delay(retryAfter, cancellationToken);
                }

                continue;
            }

            if (IsRetryable(response.StatusCode))
            {
                throw new ExternalServiceUnavailableException(
                    "Gemini",
                    response.StatusCode == HttpStatusCode.TooManyRequests
                        ? "Chatbot AI quota is temporarily exhausted. Please try again later."
                        : "Chatbot AI is temporarily unavailable. Please try again later.",
                    retryAfter);
            }

            throw Validation("chatbot", "Chatbot is temporarily unavailable.");
        }

        throw new InvalidOperationException("Gemini retry loop completed unexpectedly.");
    }

    private TimeSpan GetRetryDelay(
        HttpResponseMessage response,
        string responseText,
        int attempt)
    {
        var requestedDelay = response.Headers.RetryAfter?.Delta
            ?? ParseRetryDelay(responseText)
            ?? TimeSpan.FromMilliseconds(
                Math.Clamp(_options.RetryBaseDelayMilliseconds, 0, 10_000)
                * Math.Pow(2, attempt - 1));
        var maximumDelay = TimeSpan.FromSeconds(Math.Clamp(_options.MaxRetryDelaySeconds, 0, 30));

        return requestedDelay <= maximumDelay ? requestedDelay : maximumDelay;
    }

    private static TimeSpan? ParseRetryDelay(string responseText)
    {
        try
        {
            using var document = JsonDocument.Parse(responseText);
            if (!document.RootElement.TryGetProperty("error", out var error)
                || !error.TryGetProperty("details", out var details)
                || details.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var detail in details.EnumerateArray())
            {
                if (!detail.TryGetProperty("retryDelay", out var retryDelayElement))
                {
                    continue;
                }

                var retryDelay = retryDelayElement.GetString();
                if (retryDelay is not null
                    && retryDelay.EndsWith('s')
                    && double.TryParse(
                        retryDelay[..^1],
                        NumberStyles.AllowDecimalPoint,
                        CultureInfo.InvariantCulture,
                        out var seconds)
                    && seconds >= 0)
                {
                    return TimeSpan.FromSeconds(seconds);
                }
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static bool IsRetryable(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.TooManyRequests
            or HttpStatusCode.ServiceUnavailable;
    }

    private static string BuildPrompt(
        IReadOnlyCollection<ChatbotMessageDto> conversationMessages,
        string latestUserMessage)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Bạn là Homeji Assistant trong popup chat của web/app Homeji.");
        builder.AppendLine("Nhiệm vụ: hỗ trợ người dùng tìm phòng, hiểu nhu cầu thuê trọ, giải thích Premium, thanh toán MoMo/PayOS, tài khoản và cách dùng app.");
        builder.AppendLine("Quy tắc trả lời:");
        builder.AppendLine("- Trả lời bằng tiếng Việt, ngắn gọn, thực tế, thân thiện.");
        builder.AppendLine("- Nếu người dùng hỏi tìm phòng, hãy hỏi thêm khu vực/ngân sách/tiện ích nếu thiếu.");
        builder.AppendLine("- Không bịa dữ liệu phòng cụ thể nếu không có trong đoạn chat.");
        builder.AppendLine("- Không yêu cầu hoặc hiển thị API key, password, token.");
        builder.AppendLine("- Nếu câu hỏi ngoài phạm vi Homeji, trả lời ngắn và hướng về nhu cầu thuê trọ/Homeji.");
        builder.AppendLine();
        builder.AppendLine("Markdown formatting requirements:");
        builder.AppendLine("- Format the answer as compact Markdown suitable for a narrow chat popup.");
        builder.AppendLine("- Use short paragraphs, ## headings when useful, bullet or numbered lists for multiple items, and **bold** for key facts.");
        builder.AppendLine("- Do not use HTML or Markdown tables. Avoid a heading when the answer is only one short sentence.");
        builder.AppendLine();
        builder.AppendLine(HomejiLocationKnowledge.GroundingPrompt);
        builder.AppendLine();
        builder.AppendLine("Lịch sử chat gần nhất:");

        foreach (var message in conversationMessages.OrderBy(message => message.CreatedAt))
        {
            var sender = message.Sender == ChatMessageSender.User ? "User" : "Assistant";
            builder.AppendLine(CultureInfo.InvariantCulture, $"{sender}: {message.Content}");
        }

        builder.AppendLine();
        builder.AppendLine(CultureInfo.InvariantCulture, $"Tin nhắn mới nhất của user: {latestUserMessage}");
        builder.AppendLine("Câu trả lời của assistant:");

        return builder.ToString();
    }

    private static string ExtractModelText(string responseText)
    {
        using var document = JsonDocument.Parse(responseText);
        var candidates = document.RootElement.GetProperty("candidates");
        if (candidates.ValueKind != JsonValueKind.Array || candidates.GetArrayLength() == 0)
        {
            throw Validation("chatbot", "Chatbot returned no candidates.");
        }

        var parts = candidates[0]
            .GetProperty("content")
            .GetProperty("parts");

        if (parts.ValueKind != JsonValueKind.Array || parts.GetArrayLength() == 0)
        {
            throw Validation("chatbot", "Chatbot returned no content.");
        }

        var text = parts[0].GetProperty("text").GetString();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw Validation("chatbot", "Chatbot returned empty content.");
        }

        return text;
    }

    private static string NormalizeReply(string value)
    {
        var normalized = value.Trim();
        return normalized.Length <= ChatMessage.MaxContentLength
            ? normalized
            : normalized[..ChatMessage.MaxContentLength];
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.Endpoint)
            || string.IsNullOrWhiteSpace(_options.ApiKey)
            || _options.ApiKey.Equals("REPLACE_WITH_GEMINI_API_KEY", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Gemini AI settings are not configured.");
        }
    }

    private static RequestValidationException Validation(string field, string message)
    {
        return new RequestValidationException(new Dictionary<string, string[]>
        {
            [field] = [message],
        });
    }
}
