using Homeji.Api.BackgroundJobs;
using Microsoft.Extensions.Logging;

namespace Homeji.Api.IntegrationTests.Infrastructure;

public sealed class MemoryUsageWorkerTests
{
    [Fact]
    public async Task Worker_EmitsInitialProcessSampleAndStopsWithoutWaitingForInterval()
    {
        var logger = new SampleLogger();
        using var worker = new MemoryUsageWorker(logger);
        await worker.StartAsync(CancellationToken.None);
        var sample = await logger.Sample.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(Assert.IsType<long>(sample["WorkingSetBytes"]) > 0);
        Assert.True(Assert.IsType<long>(sample["ManagedBytes"]) > 0);
        using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await worker.StopAsync(shutdown.Token);
        Assert.NotNull(worker.ExecuteTask);
        Assert.True(worker.ExecuteTask.IsCompletedSuccessfully);
    }

    private sealed class SampleLogger : ILogger<MemoryUsageWorker>
    {
        public TaskCompletionSource<Dictionary<string, object?>> Sample { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (eventId.Id == 1 && state is IEnumerable<KeyValuePair<string, object?>> fields)
            {
                Sample.TrySetResult(fields.ToDictionary(field => field.Key, field => field.Value));
            }
        }
    }
}
