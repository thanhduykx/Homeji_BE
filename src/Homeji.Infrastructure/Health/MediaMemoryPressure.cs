using System.Diagnostics;
using System.Globalization;

namespace Homeji.Infrastructure.Health;

/// <summary>Uses the container's charged memory, rather than double-counting shared RSS pages.</summary>
public static class MediaMemoryPressure
{
    public static bool IsHigh()
    {
        const long defaultThreshold = 350L * 1024 * 1024;
        if (OperatingSystem.IsLinux())
        {
            var current = ReadCounter("/sys/fs/cgroup/memory.current")
                ?? ReadCounter("/sys/fs/cgroup/memory/memory.usage_in_bytes");
            var limit = ReadCounter("/sys/fs/cgroup/memory.max")
                ?? ReadCounter("/sys/fs/cgroup/memory/memory.limit_in_bytes");
            // An unconstrained build/dev cgroup can also contain compiler processes and page cache.
            // Use it only when it actually represents the small-container budget being protected.
            if (current.HasValue && limit is > 0 and <= 512L * 1024 * 1024)
            {
                var threshold = Math.Min(defaultThreshold, Math.Max(0, limit.Value - 96L * 1024 * 1024));
                return current.Value >= threshold;
            }
        }
        using var process = Process.GetCurrentProcess();
        return process.WorkingSet64 >= defaultThreshold;
    }

    private static long? ReadCounter(string path)
    {
        try
        {
            return File.Exists(path) && long.TryParse(File.ReadAllText(path).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var value)
                ? value : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return null; }
    }
}
