namespace Homeji.Infrastructure.External;

public sealed class GeminiOptions
{
    public const string SectionName = "Ai:Gemini";

    public string Endpoint { get; set; } =
        "https://generativelanguage.googleapis.com/v1beta/models/gemini-flash-latest:generateContent";

    public string? ApiKey { get; set; }

    public int TimeoutSeconds { get; set; } = 30;
}
