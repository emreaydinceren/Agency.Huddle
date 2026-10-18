using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Tools;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>Tests for <see cref="TaskPresence.Resolve(bool, bool)"/>, pinning Spec §10.8's presence table.</summary>
public sealed class TaskPresenceTests
{
    /// <summary>An offline Agent is always Offline, whatever its busy state.</summary>
    /// <param name="busy">The busy flag under test; Offline must not depend on it.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Resolve_Offline_ReturnsOffline(bool busy)
    {
        PresenceState state = TaskPresence.Resolve(online: false, busy: busy);

        Assert.Equal(PresenceState.Offline, state);
    }

    /// <summary>An online Agent with a Turn running is Awake.</summary>
    [Fact]
    public void Resolve_OnlineAndBusy_ReturnsAwake()
    {
        PresenceState state = TaskPresence.Resolve(online: true, busy: true);

        Assert.Equal(PresenceState.Awake, state);
    }

    /// <summary>An online Agent with no Turn running is Asleep.</summary>
    [Fact]
    public void Resolve_OnlineAndNotBusy_ReturnsAsleep()
    {
        PresenceState state = TaskPresence.Resolve(online: true, busy: false);

        Assert.Equal(PresenceState.Asleep, state);
    }

    /// <summary>A name no Teammate holds resolves to no presence at all.</summary>
    [Fact]
    public async Task For_UnknownName_ReturnsNull()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        SqliteTeamDirectory directory = new(dir.Options());
        await directory.InitializeAsync("You", ct);
        FakeAgentGateway gateway = new();
        PersonaHealth health = new(TimeProvider.System, NullLogger<PersonaHealth>.Instance);
        TurnActivity turns = new();

        PresenceState? state = TaskPresence.For("Nobody", directory, gateway, health, turns);

        Assert.Null(state);
    }

    /// <summary>The Human's own Name never has a presence badge, even though it is a known Name.</summary>
    [Fact]
    public async Task For_HumanName_ReturnsNull()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        SqliteTeamDirectory directory = new(dir.Options());
        await directory.InitializeAsync("You", ct);
        FakeAgentGateway gateway = new();
        PersonaHealth health = new(TimeProvider.System, NullLogger<PersonaHealth>.Instance);
        TurnActivity turns = new();

        PresenceState? state = TaskPresence.For("You", directory, gateway, health, turns);

        Assert.Null(state);
    }

    /// <summary>An Agent with no live pipe connection and no health report is Offline.</summary>
    [Fact]
    public async Task For_AgentOffline_ReturnsOffline()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        SqliteTeamDirectory directory = new(dir.Options());
        await directory.InitializeAsync("You", ct);
        await directory.UpsertAgentUserAsync("Nova", null, ct);
        FakeAgentGateway gateway = new();
        PersonaHealth health = new(TimeProvider.System, NullLogger<PersonaHealth>.Instance);
        TurnActivity turns = new();

        PresenceState? state = TaskPresence.For("Nova", directory, gateway, health, turns);

        Assert.Equal(PresenceState.Offline, state);
    }

    /// <summary>An Agent that is connected, healthy and idle is Asleep.</summary>
    [Fact]
    public async Task For_AgentOnlineNotBusy_ReturnsAsleep()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        SqliteTeamDirectory directory = new(dir.Options());
        await directory.InitializeAsync("You", ct);
        User? nova = await directory.UpsertAgentUserAsync("Nova", null, ct);
        Assert.NotNull(nova);
        FakeAgentGateway gateway = new();
        gateway.SetOnline(nova.Id);
        PersonaHealth health = new(TimeProvider.System, NullLogger<PersonaHealth>.Instance);
        TurnActivity turns = new();

        PresenceState? state = TaskPresence.For("Nova", directory, gateway, health, turns);

        Assert.Equal(PresenceState.Asleep, state);
    }

    /// <summary>An Agent that is connected and has a Turn running is Awake.</summary>
    [Fact]
    public async Task For_AgentOnlineBusy_ReturnsAwake()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        SqliteTeamDirectory directory = new(dir.Options());
        await directory.InitializeAsync("You", ct);
        User? nova = await directory.UpsertAgentUserAsync("Nova", null, ct);
        Assert.NotNull(nova);
        FakeAgentGateway gateway = new();
        gateway.SetOnline(nova.Id);
        PersonaHealth health = new(TimeProvider.System, NullLogger<PersonaHealth>.Instance);
        TurnActivity turns = new();
        turns.Begin(nova.Id, "room-1");

        PresenceState? state = TaskPresence.For("Nova", directory, gateway, health, turns);

        Assert.Equal(PresenceState.Awake, state);
    }

    /// <summary>A Degraded health report still counts as online (Spec §10.8 / corrections-B5).</summary>
    [Fact]
    public async Task For_AgentDegraded_CountsAsOnline()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        SqliteTeamDirectory directory = new(dir.Options());
        await directory.InitializeAsync("You", ct);
        User? nova = await directory.UpsertAgentUserAsync("Nova", null, ct);
        Assert.NotNull(nova);
        FakeAgentGateway gateway = new();
        gateway.SetOnline(nova.Id);
        PersonaHealth health = new(TimeProvider.System, NullLogger<PersonaHealth>.Instance);
        health.Report("Nova", PersonaState.Degraded, "slow");
        TurnActivity turns = new();

        PresenceState? state = TaskPresence.For("Nova", directory, gateway, health, turns);

        Assert.Equal(PresenceState.Asleep, state);
    }

    /// <summary>A Starting health report counts as Offline, even with a live pipe connection (corrections-B5).</summary>
    [Fact]
    public async Task For_AgentStarting_CountsAsOffline()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        SqliteTeamDirectory directory = new(dir.Options());
        await directory.InitializeAsync("You", ct);
        User? nova = await directory.UpsertAgentUserAsync("Nova", null, ct);
        Assert.NotNull(nova);
        FakeAgentGateway gateway = new();
        gateway.SetOnline(nova.Id);
        PersonaHealth health = new(TimeProvider.System, NullLogger<PersonaHealth>.Instance);
        health.Report("Nova", PersonaState.Starting, null);
        TurnActivity turns = new();

        PresenceState? state = TaskPresence.For("Nova", directory, gateway, health, turns);

        Assert.Equal(PresenceState.Offline, state);
    }
}
