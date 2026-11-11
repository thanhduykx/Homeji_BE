using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace Homeji.Api.Security;

/// <summary>
/// Counts each user's recent chatbot requests in memory. One API instance is assumed;
/// with several instances each would count separately and a burst could be split across them.
/// </summary>
public sealed class ChatbotBurstGuard(IOptions<ChatbotRecaptchaOptions> options, TimeProvider clock)
{
    private readonly ConcurrentDictionary<string, RecentRequests> _requests = new(StringComparer.Ordinal);
    private readonly int _limit = options.Value.BurstLimit;
    private readonly TimeSpan _window = TimeSpan.FromSeconds(options.Value.BurstWindowSeconds);
    private long _nextSweepTicks;

    /// <summary>Records one request and reports whether it goes past the limit for its window.</summary>
    public bool RecordAndCheck(string clientKey)
    {
        var now = clock.GetUtcNow();
        SweepIfDue(now);
        var recent = _requests.GetOrAdd(clientKey, _ => new RecentRequests());
        lock (recent)
        {
            recent.DropBefore(now - _window);
            recent.Add(now);
            // Keep at most one entry past the limit, so a client that keeps sending stays challenged
            // without its history growing.
            if (recent.Count > _limit + 1) recent.DropOldest();
            return recent.Count > _limit;
        }
    }

    /// <summary>Starts a fresh window after the user has passed the challenge.</summary>
    public void Reset(string clientKey) => _requests.TryRemove(clientKey, out _);

    private void SweepIfDue(DateTimeOffset now)
    {
        var due = Interlocked.Read(ref _nextSweepTicks);
        if (now.UtcTicks < due) return;
        if (Interlocked.CompareExchange(ref _nextSweepTicks, (now + _window).UtcTicks, due) != due) return;

        var cutoff = now - _window;
        foreach (var (key, recent) in _requests)
        {
            lock (recent)
            {
                recent.DropBefore(cutoff);
                if (recent.Count == 0) _requests.TryRemove(key, out _);
            }
        }
    }

    private sealed class RecentRequests
    {
        // Concurrent requests can acquire the client lock in a different order than they arrived.
        private readonly PriorityQueue<DateTimeOffset, DateTimeOffset> _times = new();
        public int Count => _times.Count;
        public void Add(DateTimeOffset time) => _times.Enqueue(time, time);
        public void DropOldest() => _times.Dequeue();

        public void DropBefore(DateTimeOffset cutoff)
        {
            while (_times.TryPeek(out var time, out _) && time <= cutoff) _times.Dequeue();
        }
    }
}
