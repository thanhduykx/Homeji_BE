namespace Homeji.Application.Abstractions.Security;

public sealed record CspViolation(
    string? Document,
    string? Directive,
    string? Blocked,
    string? Source,
    int? Line,
    string? Disposition);

public interface ICspReportLog
{
    void Record(CspViolation violation);
}
