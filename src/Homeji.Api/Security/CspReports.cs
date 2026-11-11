using System.Globalization;
using System.Text.Json;
using Homeji.Application.Abstractions.Security;

namespace Homeji.Api.Security;

/// <summary>Reduces a browser's violation report to fields that are safe to keep in logs.</summary>
public static class CspReportParser
{
    private const int MaxValueLength = 200;

    /// <summary>Chromium posts { "csp-report": fields }; WebKit posts { type: "csp-violation", body: fields } to the same directive.</summary>
    public static CspViolation? Parse(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object) return null;
        if (!payload.TryGetProperty("csp-report", out var fields)
            && !(Text(payload, "type") == "csp-violation" && payload.TryGetProperty("body", out fields)))
        {
            return null;
        }
        if (fields.ValueKind != JsonValueKind.Object) return null;

        return new CspViolation(
            SanitizeUrl(Text(fields, "documentURL", "document-uri")),
            Text(fields, "effectiveDirective", "effective-directive", "violated-directive"),
            SanitizeUrl(Text(fields, "blockedURL", "blocked-uri")),
            SanitizeUrl(Text(fields, "sourceFile", "source-file")),
            Number(fields, "lineNumber", "line-number"),
            Text(fields, "disposition"));
    }

    /// <summary>Query strings and fragments can hold reset tokens and API keys, so only the location is kept.</summary>
    public static string? SanitizeUrl(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (Uri.TryCreate(value, UriKind.Absolute, out var url))
        {
            return url.Scheme is "http" or "https" or "ws" or "wss"
                ? Truncate($"{url.Scheme}://{url.Authority}{url.AbsolutePath}")
                : url.Scheme;
        }
        // Browsers also report keywords such as "inline", "eval" and "wasm-eval".
        return value.All(character => character is '-' or (>= 'a' and <= 'z')) ? value : "invalid";
    }

    private static string? Text(JsonElement fields, params string[] names)
    {
        foreach (var name in names)
        {
            if (fields.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return Clean(value.GetString()!);
            }
        }
        return null;
    }

    private static int? Number(JsonElement fields, params string[] names)
    {
        foreach (var name in names)
        {
            if (fields.TryGetProperty(name, out var value)
                && value.ValueKind == JsonValueKind.Number
                && value.TryGetInt32(out var number)
                && number >= 0)
            {
                return number;
            }
        }
        return null;
    }

    /// <summary>Values are attacker-controlled: keep each report on one log line.</summary>
    private static string Clean(string value) =>
        Truncate(string.Concat(value.Where(character => char.GetUnicodeCategory(character)
            is not (UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator))));

    private static string Truncate(string value) =>
        value.Length <= MaxValueLength ? value : value[..MaxValueLength];
}

/// <summary>Anyone can post a report, so log volume is capped per process, not per client.</summary>
public sealed partial class CspReportLog(ILogger<CspReportLog> logger, TimeProvider? clock = null) : ICspReportLog
{
    private const int MaxLoggedPerWindow = 60;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly object _gate = new();
    private DateTimeOffset _windowStart = (clock ?? TimeProvider.System).GetUtcNow();
    private int _logged;
    private int _dropped;

    public void Record(CspViolation violation)
    {
        var droppedBefore = 0;
        lock (_gate)
        {
            if (_clock.GetUtcNow() - _windowStart >= Window)
            {
                droppedBefore = _dropped;
                _windowStart = _clock.GetUtcNow();
                _logged = 0;
                _dropped = 0;
            }
            if (_logged >= MaxLoggedPerWindow)
            {
                _dropped++;
                return;
            }
            _logged++;
        }

        if (droppedBefore > 0)
        {
            Write(logger, JsonSerializer.Serialize(new { Event = "csp-violations-dropped", Count = droppedBefore }, JsonOptions));
        }
        Write(logger, JsonSerializer.Serialize(
            new
            {
                Event = "csp-violation",
                violation.Document,
                violation.Directive,
                violation.Blocked,
                violation.Source,
                violation.Line,
                violation.Disposition,
            },
            JsonOptions));
    }

    [LoggerMessage(EventId = 7401, Level = LogLevel.Warning, Message = "{Report}")]
    private static partial void Write(ILogger logger, string report);
}
