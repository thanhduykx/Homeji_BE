namespace Homeji.Api.Realtime;

// One active call per account, including other tabs/devices. Single API instance;
// replace with a shared atomic store before scaling to multiple API replicas.
public sealed class VoiceCallRegistry(TimeProvider timeProvider)
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, VoiceCallSession> _calls = [];
    private readonly Dictionary<Guid, (DateTimeOffset Since, int Count)> _attempts = [];
    public VoiceCallSession? Create(Guid conversationId, Guid caller, Guid recipient, string connectionId)
    {
        lock (_gate)
        {
            Prune();
            var now = timeProvider.GetUtcNow();
            if (_attempts.TryGetValue(caller, out var attempt) && now - attempt.Since < TimeSpan.FromMinutes(1))
            {
                if (attempt.Count >= 8) return null;
                _attempts[caller] = (attempt.Since, attempt.Count + 1);
            }
            else _attempts[caller] = (now, 1);
            if (_calls.Values.Any(c => c.Includes(caller) || c.Includes(recipient))) return null;
            var call = new VoiceCallSession(Guid.NewGuid(), conversationId, caller, recipient,
                connectionId, timeProvider.GetUtcNow().AddSeconds(45));
            _calls.Add(call.Id, call);
            return call;
        }
    }
    public VoiceCallSession? Get(Guid id, Guid user)
    {
        lock (_gate) { Prune(); return _calls.TryGetValue(id, out var call) && call.Includes(user) ? call : null; }
    }
    public VoiceCallSession? Accept(Guid id, Guid user, string connectionId)
    {
        lock (_gate)
        {
            Prune();
            if (!_calls.TryGetValue(id, out var c) || c.Recipient != user || c.RecipientConnectionId is not null) return null;
            c.RecipientConnectionId = connectionId;
            c.ExpiresAt = timeProvider.GetUtcNow().AddHours(1);
            return c;
        }
    }
    public VoiceCallSession? Remove(Guid id, Guid user)
    {
        lock (_gate)
        {
            if (!_calls.TryGetValue(id, out var c) || !c.Includes(user)) return null;
            _calls.Remove(id); return c;
        }
    }
    public VoiceCallSession[] Disconnect(string connectionId)
    {
        lock (_gate)
        {
            var calls = _calls.Values.Where(c => c.CallerConnectionId == connectionId || c.RecipientConnectionId == connectionId).ToArray();
            foreach (var call in calls) _calls.Remove(call.Id);
            return calls;
        }
    }
    private void Prune()
    {
        foreach (var id in _calls.Where(pair => pair.Value.ExpiresAt <= timeProvider.GetUtcNow()).Select(pair => pair.Key).ToArray()) _calls.Remove(id);
        foreach (var id in _attempts.Where(pair => timeProvider.GetUtcNow() - pair.Value.Since >= TimeSpan.FromMinutes(1)).Select(pair => pair.Key).ToArray()) _attempts.Remove(id);
    }
}

public sealed class VoiceCallSession(Guid id, Guid conversationId, Guid caller, Guid recipient, string callerConnectionId, DateTimeOffset expiresAt)
{
    public Guid Id { get; } = id;
    public Guid ConversationId { get; } = conversationId;
    public Guid Caller { get; } = caller;
    public Guid Recipient { get; } = recipient;
    public string CallerConnectionId { get; } = callerConnectionId;
    public string? RecipientConnectionId { get; set; }
    public DateTimeOffset ExpiresAt { get; set; } = expiresAt;
    private int _signalCount;
    public bool AllowSignal() => Interlocked.Increment(ref _signalCount) <= 300;
    public bool Includes(Guid user) => user == Caller || user == Recipient;
}
