using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.AI;
using Homeji.Application.IServices.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Homeji.Infrastructure.External;

public sealed class GeminiSearchTextParser : IAiSearchTextParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Action<ILogger, int, Exception?> LogGeminiParsingFailed =
        LoggerMessage.Define<int>(
            LogLevel.Warning,
            new EventId(2001, nameof(LogGeminiParsingFailed)),
            "Gemini NLP parsing failed with status {StatusCode}.");

    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiSearchTextParser> _logger;

    public GeminiSearchTextParser(
        HttpClient httpClient,
        IOptions<GeminiOptions> options,
        ILogger<GeminiSearchTextParser> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        if (_options.TimeoutSeconds > 0)
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 5, 120));
        }
    }

    public async Task<AiParsedSearchCriteriaDto> ParseAsync(
        string text,
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
                            text = BuildPrompt(text),
                        },
                    },
                },
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
            },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint)
        {
            Content = JsonContent.Create(payload, options: JsonOptions),
        };
        request.Headers.Add("X-goog-api-key", _options.ApiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            LogGeminiParsingFailed(_logger, (int)response.StatusCode, null);
            throw new ExternalDependencyException("AI parser is temporarily unavailable.");
        }

        var modelText = ExtractModelText(responseText);
        return ParseCriteriaJson(modelText);
    }

    private static string BuildPrompt(string text)
    {
        return $$"""
            Bạn là bộ NLP parser cho ứng dụng tìm nhà trọ Homeji.
            Hãy bóc tách câu người dùng thành JSON hợp lệ, không thêm giải thích.

            Schema bắt buộc:
            {
              "location": string | null,
              "keyword": string | null,
              "price_min": number | null,
              "price_max": number | null,
              "area_min": number | null,
              "area_max": number | null,
              "criteria": string[]
            }

            Quy tắc:
            - Chuyển tiền Việt sang VND: "2tr", "2 triệu" => 2000000.
            - "cao nhất", "không được hơn", "tối đa" => price_max.
            - Chuẩn hóa criteria sang camelCase tiếng Anh nếu phù hợp: parking, freeTime, wifi, airConditioner, privateToilet, security, quiet, petFriendly, kitchen.
            - Nếu không chắc, để null hoặc mảng rỗng.

            Câu người dùng:
            {{text}}
            """;
    }

    private static string ExtractModelText(string responseText)
    {
        using var document = JsonDocument.Parse(responseText);
        var candidates = document.RootElement.GetProperty("candidates");
        if (candidates.ValueKind != JsonValueKind.Array || candidates.GetArrayLength() == 0)
        {
            throw new ExternalDependencyException("AI parser returned no candidates.");
        }

        var parts = candidates[0]
            .GetProperty("content")
            .GetProperty("parts");

        if (parts.ValueKind != JsonValueKind.Array || parts.GetArrayLength() == 0)
        {
            throw new ExternalDependencyException("AI parser returned no content.");
        }

        var text = parts[0].GetProperty("text").GetString();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ExternalDependencyException("AI parser returned empty content.");
        }

        return StripCodeFence(text.Trim());
    }

    private static AiParsedSearchCriteriaDto ParseCriteriaJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        return new AiParsedSearchCriteriaDto(
            GetString(root, "location"),
            GetString(root, "keyword"),
            GetDecimal(root, "price_min"),
            GetDecimal(root, "price_max"),
            GetDecimal(root, "area_min"),
            GetDecimal(root, "area_max"),
            GetStringArray(root, "criteria"));
    }

    private static string StripCodeFence(string value)
    {
        if (!value.StartsWith("```", StringComparison.Ordinal))
        {
            return value;
        }

        var withoutOpeningFence = value[(value.IndexOf('\n') + 1)..];
        var closingFenceIndex = withoutOpeningFence.LastIndexOf("```", StringComparison.Ordinal);
        return closingFenceIndex >= 0
            ? withoutOpeningFence[..closingFenceIndex].Trim()
            : withoutOpeningFence.Trim();
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static decimal? GetDecimal(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(propertyName, out var property)
            || property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out var number))
        {
            return number;
        }

        if (property.ValueKind == JsonValueKind.String
            && decimal.TryParse(
                property.GetString(),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static string[] GetStringArray(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return property.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.Endpoint)
            || string.IsNullOrWhiteSpace(_options.ApiKey)
            || _options.ApiKey.Equals("REPLACE_WITH_GEMINI_API_KEY", StringComparison.OrdinalIgnoreCase))
        {
            throw new ExternalDependencyException("Gemini AI settings are not configured.");
        }
    }
}
