namespace Homeji.Infrastructure.IntegrationTests;

internal static class LocalPostingDatabase
{
    public const string SkipReason = "Run scripts/quality/Test-LocalBackend.ps1 -IncludePostingPersistence to use an isolated PostgreSQL instance.";
    public static bool IsConfigured => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HOMEJI_TEST_DB"));
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class LocalPostingDatabaseFactAttribute : FactAttribute
{
    public LocalPostingDatabaseFactAttribute()
    {
        if (!LocalPostingDatabase.IsConfigured) Skip = LocalPostingDatabase.SkipReason;
    }
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class LocalPostingDatabaseTheoryAttribute : TheoryAttribute
{
    public LocalPostingDatabaseTheoryAttribute()
    {
        if (!LocalPostingDatabase.IsConfigured) Skip = LocalPostingDatabase.SkipReason;
    }
}
