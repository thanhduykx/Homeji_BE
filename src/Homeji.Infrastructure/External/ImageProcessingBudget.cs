using Homeji.Infrastructure.Health;
using Homeji.Application.Common.Exceptions;

namespace Homeji.Infrastructure.External;

/// <summary>Bounds native image work across all request scopes in this process.</summary>
public sealed class ImageProcessingBudget : IDisposable
{
    internal static ImageProcessingBudget Shared { get; } = new();
    private readonly SemaphoreSlim _slot = new(1, 1);
    private int _waiting;

    public async Task<IDisposable> EnterAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!await _slot.WaitAsync(0, cancellationToken))
        {
            if (Interlocked.Increment(ref _waiting) > 2)
            {
                Interlocked.Decrement(ref _waiting);
                throw Busy();
            }
            try
            {
                if (!await _slot.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken)) throw Busy();
            }
            finally { Interlocked.Decrement(ref _waiting); }
        }
        try
        {
            if (MediaMemoryPressure.IsHigh()) throw Busy();
            return new Lease(_slot);
        }
        catch { _slot.Release(); throw; }
    }

    private static ExternalServiceUnavailableException Busy() => new("ImageProcessing",
        "Hệ thống đang xử lý ảnh. Vui lòng thử lại sau vài giây.", TimeSpan.FromSeconds(5));

    public void Dispose() => _slot.Dispose();

    private sealed class Lease(SemaphoreSlim slot) : IDisposable
    {
        private SemaphoreSlim? _slot = slot;
        public void Dispose() => Interlocked.Exchange(ref _slot, null)?.Release();
    }
}
