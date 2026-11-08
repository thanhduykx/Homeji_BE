using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Homeji.Api.Security;

public sealed class ChatbotRecaptchaOptions
{
    public const string SectionName = "Chatbot:Recaptcha";

    public string? SiteKey { get; set; }
    public string? SecretKey { get; set; }
    public string[] AllowedHostnames { get; set; } = [];

    public bool IsValid() =>
        string.IsNullOrWhiteSpace(SiteKey) && string.IsNullOrWhiteSpace(SecretKey)
        || !string.IsNullOrWhiteSpace(SiteKey) && !string.IsNullOrWhiteSpace(SecretKey)
            && AllowedHostnames.Length > 0 && AllowedHostnames.All(host => !string.IsNullOrWhiteSpace(host));
}

public enum ChatbotRecaptchaResult { Accepted, Rejected, Unavailable }

public sealed class ChatbotRecaptcha(HttpClient httpClient, IOptions<ChatbotRecaptchaOptions> options)
{
    public const string TokenHeader = "X-Homeji-Recaptcha";
    public const string MessageAction = "chatbot_message";
    public const string TranscriptionAction = "chatbot_transcription";
    private const double MinimumScore = 0.5;
    private readonly ChatbotRecaptchaOptions _options = options.Value;

    public string? SiteKey => string.IsNullOrWhiteSpace(_options.SiteKey) ? null : _options.SiteKey.Trim();

    public async Task<ChatbotRecaptchaResult> VerifyAsync(string token, string action, CancellationToken cancellationToken)
    {
        if (SiteKey is null) return ChatbotRecaptchaResult.Accepted;
        if (string.IsNullOrWhiteSpace(token) || token.Length > 8192)
            return ChatbotRecaptchaResult.Rejected;

        try
        {
            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["secret"] = _options.SecretKey!.Trim(),
                ["response"] = token,
            });
            using var response = await httpClient.PostAsync("https://www.google.com/recaptcha/api/siteverify", form, cancellationToken);
            if (!response.IsSuccessStatusCode) return ChatbotRecaptchaResult.Unavailable;

            var result = await response.Content.ReadFromJsonAsync<VerificationResponse>(cancellationToken);
            if (result is null) return ChatbotRecaptchaResult.Unavailable;
            return result.Success
                && result.ErrorCodes is not { Length: > 0 }
                && result.Score is >= MinimumScore and <= 1
                && string.Equals(result.Action, action, StringComparison.Ordinal)
                && _options.AllowedHostnames.Contains(result.Hostname, StringComparer.OrdinalIgnoreCase)
                ? ChatbotRecaptchaResult.Accepted
                : ChatbotRecaptchaResult.Rejected;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ChatbotRecaptchaResult.Unavailable;
        }
        catch (HttpRequestException)
        {
            return ChatbotRecaptchaResult.Unavailable;
        }
        catch (JsonException)
        {
            return ChatbotRecaptchaResult.Unavailable;
        }
    }

    private sealed record VerificationResponse(
        bool Success,
        double? Score,
        string? Action,
        string? Hostname,
        [property: JsonPropertyName("error-codes")] string[]? ErrorCodes);
}
