using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Acp;

/// <summary>Tests for <see cref="PersonaStatusResolver.Resolve"/>.</summary>
public sealed class PersonaStatusResolverTests
{
    private static readonly DateTimeOffset SomeSince = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Rule 1, the one that is easy to get backwards: a pipe that is still connected must not mask a
    /// health report that says the runner has died.
    /// </summary>
    [Fact]
    public void ConnectedButHealthSaysOffline_IsOffline()
    {
        PersonaStatus health = new(PersonaState.Offline, "The Agent stopped responding.", PersonaStatusResolverTests.SomeSince);

        var result = PersonaStatusResolver.Resolve(connected: true, health);

        Assert.Equal(PersonaState.Offline, result.State);
        Assert.Equal("The Agent stopped responding.", result.Reason);
        Assert.Equal(PersonaStatusResolverTests.SomeSince, result.Since);
    }

    /// <summary>No pipe and no health report at all still resolves to Offline, just with no reason to give.</summary>
    [Fact]
    public void NotConnectedWithNoHealth_IsOfflineWithNoReason()
    {
        var result = PersonaStatusResolver.Resolve(connected: false, health: null);

        Assert.Equal(PersonaState.Offline, result.State);
        Assert.Null(result.Reason);
    }

    /// <summary>
    /// A disconnected pipe reports Offline even when the last health report was not itself Offline —
    /// but its reason, if it had one, still rides along.
    /// </summary>
    [Fact]
    public void NotConnectedWithAReason_KeepsTheReason()
    {
        PersonaStatus health = new(PersonaState.Degraded, "Turn ran long.", PersonaStatusResolverTests.SomeSince);

        var result = PersonaStatusResolver.Resolve(connected: false, health);

        Assert.Equal(PersonaState.Offline, result.State);
        Assert.Equal("Turn ran long.", result.Reason);
    }

    /// <summary>A slow Adapter launch reads as Starting, never as Offline.</summary>
    [Fact]
    public void Starting_IsNotOffline()
    {
        PersonaStatus health = new(PersonaState.Starting, null, PersonaStatusResolverTests.SomeSince);

        var result = PersonaStatusResolver.Resolve(connected: true, health);

        Assert.Equal(PersonaState.Starting, result.State);
    }

    /// <summary>Connected and reported Degraded resolves to Degraded, carrying the health reason.</summary>
    [Fact]
    public void ConnectedWithDegraded_IsDegradedWithItsReason()
    {
        PersonaStatus health = new(PersonaState.Degraded, "Ignoring every Mention.", PersonaStatusResolverTests.SomeSince);

        var result = PersonaStatusResolver.Resolve(connected: true, health);

        Assert.Equal(PersonaState.Degraded, result.State);
        Assert.Equal("Ignoring every Mention.", result.Reason);
    }

    /// <summary>Connected and reported Online resolves to Online.</summary>
    [Fact]
    public void ConnectedWithOnline_IsOnline()
    {
        PersonaStatus health = new(PersonaState.Online, null, PersonaStatusResolverTests.SomeSince);

        var result = PersonaStatusResolver.Resolve(connected: true, health);

        Assert.Equal(PersonaState.Online, result.State);
    }
}
