using System.Collections.Concurrent;
using Homeji.Application.Abstractions.Authentication;

namespace Homeji.Api.Authentication;

public sealed class MemoryUserSessionRevocationCache(TimeProvider timeProvider)
    : IUserSessionRevocationCache
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(30);
    private readonly ConcurrentDictionary<Guid, CacheEntry> _entries = new();

    public bool TryGet(Guid userId, out DateTimeOffset? revokedBefore)
    {
        if (_entries.TryGetValue(userId, out var entry)
            && entry.ExpiresAt > timeProvider.GetUtcNow())
        {
            revokedBefore = entry.RevokedBefore;
            return true;
        }

        _entries.TryRemove(userId, out _);
        revokedBefore = null;
        return false;
    }

    public void Store(Guid userId, DateTimeOffset? revokedBefore)
    {
        _entries[userId] = new CacheEntry(
            revokedBefore,
            timeProvider.GetUtcNow().Add(CacheLifetime));
    }

    private sealed record CacheEntry(DateTimeOffset? RevokedBefore, DateTimeOffset ExpiresAt);
}
