using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Homeji.Api.Security;

public sealed class ChatbotRecaptchaOptions
{
    public const string SectionName = "Chatbot:Recaptcha";

    /// <summary>reCAPTCHA v3 keys. Every request needs a score token while these are the only keys set.</summary>
    public string? SiteKey { get; set; }
    public string? SecretKey { get; set; }

    /// <summary>Checkbox (v2) keys. When set, ordinary requests need no token and only bursts are challenged.</summary>
    public string? ChallengeSiteKey { get; set; }
    public string? ChallengeSecretKey { get; set; }

    public string[] AllowedHostnames { get; set; } = [];

    /// <summary>Requests a user may send within <see cref="BurstWindowSeconds"/> before the next one is challenged.</summary>
    public int BurstLimit { get; set; } = 5;
    public int BurstWindowSeconds { get; set; } = 30;

    public bool ChallengeEnabled => !string.IsNullOrWhiteSpace(ChallengeSiteKey);
    public bool ScoreEnabled => !string.IsNullOrWhiteSpace(SiteKey);

    public bool IsValid()
    {
        var scorePair = BothOrNeither(SiteKey, SecretKey);
        var challengePair = BothOrNeither(ChallengeSiteKey, ChallengeSecretKey);
        var hostnames = !ScoreEnabled && !ChallengeEnabled
            || AllowedHostnames.Length > 0 && AllowedHostnames.All(host => !string.IsNullOrWhiteSpace(host));
        return scorePair && challengePair && hostnames
            && BurstLimit is >= 1 and <= 100
            && BurstWindowSeconds is >= 1 and <= 3600;
    }

    private static bool BothOrNeither(string? first, string? second) =>
        string.IsNullOrWhiteSpace(first) == string.IsNullOrWhiteSpace(second);
}

public enum ChatbotRecaptchaResult { Accepted, Rejected, Unavailable }

public sealed class ChatbotRecaptcha(HttpClient httpClient, IOptions<ChatbotRecaptchaOptions> options)
{
    public const string TokenHeader = "X-Homeji-Recaptcha";
    public const string MessageAction = "chatbot_message";
    public const string TranscriptionAction = "chatbot_transcription";
    private const double MinimumScore = 0.5;
    private const int MaximumTokenLength = 8192;
    private readonly ChatbotRecaptchaOptions _options = options.Value;

    public bool ChallengeEnabled => _options.ChallengeEnabled;

    /// <summary>
    /// The v3 key the browser loads before every request. Withheld once the checkbox is configured,
    /// so ordinary requests no longer download Google's script.
    /// </summary>
    public string? ScoreSiteKey => _options.ChallengeEnabled || !_options.ScoreEnabled ? null : _options.SiteKey!.Trim();

    public string? ChallengeSiteKey => _options.ChallengeEnabled ? _options.ChallengeSiteKey!.Trim() : null;

    /// <summary>Checks a v3 score token for one action. Accepts everything while no v3 key is set.</summary>
    public async Task<ChatbotRecaptchaResult> VerifyScoreAsync(string token, string action, CancellationToken cancellationToken)
    {
        if (!_options.ScoreEnabled) return ChatbotRecaptchaResult.Accepted;
        var (outcome, response) = await SiteVerifyAsync(_options.SecretKey!, token, cancellationToken);
        if (response is null) return outcome;
        return IsGenuine(response)
            && response.Score is >= MinimumScore and <= 1
            && string.Equals(response.Action, action, StringComparison.Ordinal)
            ? ChatbotRecaptchaResult.Accepted
            : ChatbotRecaptchaResult.Rejected;
    }

    /// <summary>Checks a token from the checkbox challenge, which carries no score or action.</summary>
    public async Task<ChatbotRecaptchaResult> VerifyChallengeAsync(string token, CancellationToken cancellationToken)
    {
        if (!_options.ChallengeEnabled) return ChatbotRecaptchaResult.Rejected;
        var (outcome, response) = await SiteVerifyAsync(_options.ChallengeSecretKey!, token, cancellationToken);
        if (response is null) return outcome;
        return IsGenuine(response) ? ChatbotRecaptchaResult.Accepted : ChatbotRecaptchaResult.Rejected;
    }

    private bool IsGenuine(VerificationResponse response) =>
        response.Success
        && response.ErrorCodes is not { Length: > 0 }
        && _options.AllowedHostnames.Contains(response.Hostname, StringComparer.OrdinalIgnoreCase);

    private async Task<(ChatbotRecaptchaResult Outcome, VerificationResponse? Response)> SiteVerifyAsync(
        string secret, string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > MaximumTokenLength)
            return (ChatbotRecaptchaResult.Rejected, null);

        try
        {
            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["secret"] = secret.Trim(),
                ["response"] = token,
            });
            using var response = await httpClient.PostAsync("https://www.google.com/recaptcha/api/siteverify", form, cancellationToken);
            if (!response.IsSuccessStatusCode) return (ChatbotRecaptchaResult.Unavailable, null);

            var result = await response.Content.ReadFromJsonAsync<VerificationResponse>(cancellationToken);
            return result is null ? (ChatbotRecaptchaResult.Unavailable, null) : (ChatbotRecaptchaResult.Accepted, result);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (ChatbotRecaptchaResult.Unavailable, null);
        }
        catch (HttpRequestException)
        {
            return (ChatbotRecaptchaResult.Unavailable, null);
        }
        catch (JsonException)
        {
            return (ChatbotRecaptchaResult.Unavailable, null);
        }
    }

    private sealed record VerificationResponse(
        bool Success,
        double? Score,
        string? Action,
        string? Hostname,
        [property: JsonPropertyName("error-codes")] string[]? ErrorCodes);
}
