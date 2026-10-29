using Homeji.Api.Realtime;

namespace Homeji.Api.IntegrationTests;

public sealed class VoiceCallRegistryTests
{
    [Fact]
    public void Only_recipient_can_accept_and_only_one_device_can_claim_call()
    {
        var registry = new VoiceCallRegistry(TimeProvider.System);
        var caller = Guid.NewGuid(); var recipient = Guid.NewGuid();
        var call = registry.Create(Guid.NewGuid(), caller, recipient, "caller")!;
        Assert.Null(registry.Accept(call.Id, caller, "caller"));
        Assert.Null(registry.Accept(call.Id, Guid.NewGuid(), "stranger"));
        Assert.NotNull(registry.Accept(call.Id, recipient, "first-device"));
        Assert.Null(registry.Accept(call.Id, recipient, "second-device"));
        Assert.Null(registry.Remove(call.Id, Guid.NewGuid()));
        Assert.NotNull(registry.Get(call.Id, recipient));
        Assert.Null(registry.Get(call.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task Simultaneous_calls_cannot_reserve_the_same_account_twice()
    {
        var registry = new VoiceCallRegistry(TimeProvider.System);
        var recipient = Guid.NewGuid();
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(i => Task.Run(() =>
            registry.Create(Guid.NewGuid(), Guid.NewGuid(), recipient, $"connection-{i}"))));
        Assert.Single(results.OfType<VoiceCallSession>());
    }

    [Fact]
    public void Disconnect_and_ringing_expiry_release_both_participants()
    {
        var clock = new TestClock(); var registry = new VoiceCallRegistry(clock);
        var caller = Guid.NewGuid(); var recipient = Guid.NewGuid();
        var call = registry.Create(Guid.NewGuid(), caller, recipient, "old")!;
        clock.Now = clock.Now.AddSeconds(46);
        Assert.Null(registry.Get(call.Id, caller));
        var replacement = registry.Create(Guid.NewGuid(), caller, recipient, "new")!;
        Assert.Empty(registry.Disconnect("old"));
        Assert.NotNull(registry.Get(replacement.Id, caller));
        Assert.Single(registry.Disconnect("new"));
        Assert.NotNull(registry.Create(Guid.NewGuid(), recipient, caller, "third"));
    }

    [Fact]
    public void Repeated_ringing_and_signal_floods_are_bounded()
    {
        var clock = new TestClock(); var registry = new VoiceCallRegistry(clock);
        var caller = Guid.NewGuid(); var recipient = Guid.NewGuid();
        for (var i = 0; i < 8; i++)
        {
            var call = registry.Create(Guid.NewGuid(), caller, recipient, "caller")!;
            registry.Remove(call.Id, caller);
        }
        Assert.Null(registry.Create(Guid.NewGuid(), caller, recipient, "caller"));
        clock.Now = clock.Now.AddMinutes(1);
        var allowed = registry.Create(Guid.NewGuid(), caller, recipient, "caller")!;
        for (var i = 0; i < 300; i++) Assert.True(allowed.AllowSignal());
        Assert.False(allowed.AllowSignal());
    }
    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
