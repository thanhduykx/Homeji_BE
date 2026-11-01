using System.Diagnostics;
using System.ComponentModel;

namespace Homeji.Api.BackgroundJobs;

/// <summary>Provides process memory samples where hosting metrics are unavailable.</summary>
public sealed class MemoryUsageWorker(ILogger<MemoryUsageWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, Exception?> SampleFailed =
        LoggerMessage.Define(LogLevel.Warning, new EventId(2, nameof(MemoryUsageWorker)),
            "Process memory sample unavailable; retrying at the next interval.");
    private static readonly Action<ILogger, long, long, long, long, long, int, Exception?> Sample =
        LoggerMessage.Define<long, long, long, long, long, int>(LogLevel.Information,
            new EventId(1, nameof(MemoryUsageWorker)),
            "Memory sample: WorkingSetBytes={WorkingSetBytes} ManagedBytes={ManagedBytes} " +
            "HeapBytes={HeapBytes} FragmentedBytes={FragmentedBytes} " +
            "AvailableMemoryBytes={AvailableMemoryBytes} Gen2Collections={Gen2Collections}");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var process = Process.GetCurrentProcess();
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        try
        {
            do
            {
                try
                {
                    process.Refresh();
                    var memory = GC.GetGCMemoryInfo();
                    // Do not force a collection: observing memory must not change GC behavior.
                    Sample(logger, process.WorkingSet64, GC.GetTotalMemory(false),
                        memory.HeapSizeBytes, memory.FragmentedBytes,
                        memory.TotalAvailableMemoryBytes, GC.CollectionCount(2), null);
                }
                catch (Exception error) when (error is Win32Exception or InvalidOperationException or NotSupportedException)
                {
                    // Diagnostics must not stop the API if process counters are unavailable.
                    SampleFailed(logger, error);
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal host shutdown.
        }
    }
}
