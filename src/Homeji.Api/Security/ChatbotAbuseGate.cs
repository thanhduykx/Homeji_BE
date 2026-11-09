namespace Homeji.Api.Security;

public enum ChatbotGateDecision
{
    Allowed,
    /// <summary>The user sent too many requests in a short time and must pass the checkbox first.</summary>
    ChallengeRequired,
    /// <summary>A token was sent but Google did not accept it.</summary>
    Rejected,
    Unavailable,
}

/// <summary>
/// Decides whether a chatbot request may reach the AI. With checkbox keys set, ordinary use passes without
/// contacting Google and only a burst is challenged. With only v3 keys set, every request needs a score token,
/// which keeps the earlier behaviour while the checkbox keys are being rolled out.
/// </summary>
public sealed class ChatbotAbuseGate(ChatbotRecaptcha recaptcha, ChatbotBurstGuard burst)
{
    public string? ChallengeSiteKey => recaptcha.ChallengeSiteKey;

    public async Task<ChatbotGateDecision> CheckAsync(string clientKey, string token, string action, CancellationToken cancellationToken)
    {
        if (!recaptcha.ChallengeEnabled)
            return Map(await recaptcha.VerifyScoreAsync(token, action, cancellationToken));

        if (!burst.RecordAndCheck(clientKey)) return ChatbotGateDecision.Allowed;
        if (string.IsNullOrWhiteSpace(token)) return ChatbotGateDecision.ChallengeRequired;

        var result = Map(await recaptcha.VerifyChallengeAsync(token, cancellationToken));
        if (result == ChatbotGateDecision.Allowed) burst.Reset(clientKey);
        return result;
    }

    private static ChatbotGateDecision Map(ChatbotRecaptchaResult result) => result switch
    {
        ChatbotRecaptchaResult.Accepted => ChatbotGateDecision.Allowed,
        ChatbotRecaptchaResult.Rejected => ChatbotGateDecision.Rejected,
        _ => ChatbotGateDecision.Unavailable,
    };
}
