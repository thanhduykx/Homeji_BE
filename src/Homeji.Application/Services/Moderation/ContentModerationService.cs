using System.Text.RegularExpressions;
using Homeji.Application.IRepositories.Moderation;

namespace Homeji.Application.Services.Moderation;

public sealed class ContentModerationService
{
    private static readonly Regex HiddenPhoneRegex = new(@"\d(?:[\s.\-_\(\)]*\d){9}", RegexOptions.Compiled);
    private readonly IBadWordRepository _badWords;

    public ContentModerationService(IBadWordRepository badWords)
    {
        _badWords = badWords;
    }

    public async Task<IReadOnlyList<string>> ValidateAsync(string content, CancellationToken cancellationToken)
    {
        var violations = new List<string>();
        if (HiddenPhoneRegex.IsMatch(content))
        {
            violations.Add("Description must not contain hidden phone numbers.");
        }

        var normalized = content.ToLowerInvariant();
        var badWords = await _badWords.GetActiveValuesAsync(cancellationToken);
        foreach (var badWord in badWords)
        {
            if (normalized.Contains(badWord, StringComparison.OrdinalIgnoreCase))
            {
                violations.Add("Description contains prohibited words.");
                break;
            }
        }

        return violations;
    }
}
