using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Pipes;
using static Agency.Huddle.Tests.Acp.PersonaSupervisorTestSupport;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// <see cref="PersonaSupervisor.RestartAsync"/> and the Room Session entries the supervisor forgets
/// or keeps as hosts come and go. Split from the former single <c>PersonaSupervisorTests</c> class;
/// the shared builders live in <see cref="PersonaSupervisorTestSupport"/>.
/// </summary>
public sealed class PersonaSupervisorRestartTests
{
    /// <summary>
    /// T7.2's whole reason for existing: a Persona whose adapter failed to start is not stuck until
    /// the entire app restarts. <see cref="PersonaSupervisor.RestartAsync"/> on a Persona with no
    /// currently running host must start it, not no-op.
    /// </summary>
    [Fact]
    public async Task RestartAsync_StartsAPersonaThatFailedToStart()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:Acp:Enabled"] = "true" }, ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        WritePersonaFile(options, "nova");

        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var inner = new FakeAgentHostFactory();
        var factory = new FailOnceThenSucceedAgentHostFactory(
            "nova", new InvalidOperationException("Simulated failure starting Persona 'nova'."), inner);
        var health = NewHealth();
        var resolver = fixture.Services.GetRequiredService<AdapterProfileResolver>();
        using var skillStore = NewSkillStore(options);
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, resolver, health, new FakePromptSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance, skillStore);

        await supervisor.StartAsync(ct);
        await WaitUntilAsync(() => health.Get("nova") is { State: PersonaState.Offline }, ct);
        Assert.Equal(0, supervisor.RunningHostCount);

        await supervisor.RestartAsync("nova", ct);
        await WaitUntilAsync(() => supervisor.RunningHostCount >= 1, ct);

        Assert.Equal(1, supervisor.RunningHostCount);
        Assert.Equal(PersonaState.Online, health.Get("nova")?.State);

        await supervisor.StopAsync(ct);
    }

    /// <summary>A Restart of an already-running Persona stops its old host and starts a new one - a second factory call for the same Persona, never a second, concurrent host.</summary>
    [Fact]
    public async Task RestartAsync_ReplacesARunningHost()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:Acp:Enabled"] = "true" }, ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var factory = new FakeAgentHostFactory();
        var resolver = fixture.Services.GetRequiredService<AdapterProfileResolver>();
        using var skillStore = NewSkillStore(options);
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, resolver, NewHealth(), new FakePromptSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance, skillStore);

        personaStore.Add(Identity("nova"), "You are Nova.");
        await supervisor.StartAsync(ct);
        // The host must be registered as running, not merely asked for: a Restart while the first start is
        // still in flight is not a Restart of a running Persona.
        await WaitUntilAsync(() => supervisor.RunningHostCount >= 1, ct);

        await supervisor.RestartAsync("nova", ct);
        await WaitUntilAsync(() => factory.Calls.Count >= 2, ct);

        Assert.Equal(1, supervisor.RunningHostCount);
        await supervisor.StopAsync(ct);

        Assert.Equal(2, factory.Calls.Count);
        Assert.All(factory.Calls, call => Assert.Equal("nova", call.Persona.Name));
    }

    /// <summary>
    /// Two concurrent Restarts of the same Persona is exactly the race the shared <c>restarting</c>
    /// set (also used by the automatic file-change restart) exists to stop - a Restart button is the
    /// new way to provoke it. The second call must return harmlessly rather than throw or start a
    /// second host racing the first.
    /// </summary>
    [Fact]
    public async Task RestartAsync_IsSafeWhenCalledTwiceConcurrently()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:Acp:Enabled"] = "true" }, ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var factory = new FakeAgentHostFactory();
        var resolver = fixture.Services.GetRequiredService<AdapterProfileResolver>();
        using var skillStore = NewSkillStore(options);
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, resolver, NewHealth(), new FakePromptSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance, skillStore);

        personaStore.Add(Identity("nova"), "You are Nova.");
        await supervisor.StartAsync(ct);
        await WaitUntilAsync(() => factory.Calls.Count >= 1, ct);

        var first = supervisor.RestartAsync("nova", ct);
        var second = supervisor.RestartAsync("nova", ct);
        await Task.WhenAll(first, second);

        await WaitUntilAsync(() => supervisor.RunningHostCount >= 1, ct);

        // Bounded grace period: give a wrongly-racing supervisor every chance to start a second host.
        await Task.Delay(TimeSpan.FromMilliseconds(500), ct);

        Assert.Equal(1, supervisor.RunningHostCount);
        await supervisor.StopAsync(ct);
    }

    /// <summary>
    /// RS §6.13 / U7: the Restart button forgets every stored Room Session for the Persona, and the
    /// forget happens AFTER the old runner is disposed, so the old runner's own final Turn-end write
    /// (simulated here through <see cref="FakePersonaHost.OnDispose"/>) cannot resurrect an entry the
    /// button was meant to clear (D29 correction 16).
    /// </summary>
    [Fact]
    public async Task RestartButton_ForgetsEveryRoomSession()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:Acp:Enabled"] = "true" }, ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var directory = fixture.Services.GetRequiredService<Agency.Huddle.App.Data.ITeamDirectory>();
        var gateway = fixture.Services.GetRequiredService<Agency.Huddle.App.Pipes.IAgentGateway>();
        var chat = fixture.Services.GetRequiredService<Agency.Huddle.App.Services.ChatService>();
        var factory = new FakeAgentHostFactory { SessionPerRoom = true };
        var resolver = fixture.Services.GetRequiredService<AdapterProfileResolver>();
        using var skillStore = NewSkillStore(options);
        var roomSessions = new RoomSessionStore(options, NullLogger<RoomSessionStore>.Instance);
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, resolver, NewHealth(), new FakePromptSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance, skillStore, roomSessions: roomSessions);

        personaStore.Add(Identity("nova"), "You are Nova.");
        await supervisor.StartAsync(ct);
        await WaitUntilAsync(() => factory.Calls.Count >= 1, ct);
        var novaId = await WaitForAgentOnlineAsync(directory, gateway, "nova", ct);
        var room = await chat.CreateRoomForAsync([novaId], ct);

        roomSessions.Put("nova", room.Id, new RoomSessionEntry("sess-1", "claude", null, null, null, DateTimeOffset.UtcNow));
        var oldHost = factory.Host;
        Assert.NotNull(oldHost);
        oldHost.OnDispose = () =>
            roomSessions.Put("nova", room.Id, new RoomSessionEntry("resurrected", "claude", null, null, null, DateTimeOffset.UtcNow));

        await supervisor.RestartAsync("nova", ct);
        await WaitUntilAsync(() => factory.Calls.Count >= 2, ct);
        await supervisor.StopAsync(ct);

        Assert.Null(roomSessions.Get("nova", room.Id));
    }

    /// <summary>RS §6.13: a Persona edit that restarts its host also forgets every one of its stored Room Sessions.</summary>
    [Fact]
    public async Task PersonaEdit_ForgetsEveryRoomSession()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:Acp:Enabled"] = "true" }, ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var directory = fixture.Services.GetRequiredService<Agency.Huddle.App.Data.ITeamDirectory>();
        var gateway = fixture.Services.GetRequiredService<Agency.Huddle.App.Pipes.IAgentGateway>();
        var chat = fixture.Services.GetRequiredService<Agency.Huddle.App.Services.ChatService>();
        var factory = new FakeAgentHostFactory { SessionPerRoom = true };
        var resolver = fixture.Services.GetRequiredService<AdapterProfileResolver>();
        using var skillStore = NewSkillStore(options);
        var roomSessions = new RoomSessionStore(options, NullLogger<RoomSessionStore>.Instance);
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, resolver, NewHealth(), new FakePromptSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance, skillStore, roomSessions: roomSessions);

        personaStore.Add(Identity("nova"), "You are Nova.");
        await supervisor.StartAsync(ct);
        await WaitUntilAsync(() => factory.Calls.Count >= 1, ct);
        var novaId = await WaitForAgentOnlineAsync(directory, gateway, "nova", ct);
        var room = await chat.CreateRoomForAsync([novaId], ct);
        roomSessions.Put("nova", room.Id, new RoomSessionEntry("sess-1", "claude", null, null, null, DateTimeOffset.UtcNow));

        personaStore.Update("nova", PersonaText("nova", "You are a changed Nova."), model: null, effort: null);

        await WaitUntilAsync(() => factory.Calls.Count >= 2, ct);
        await supervisor.StopAsync(ct);

        Assert.Null(roomSessions.Get("nova", room.Id));
    }

    /// <summary>RS §6.13: a Model change restarts the host and forgets every stored Room Session, because a stale entry naming the old Model must never be resumed against the new one.</summary>
    [Fact]
    public async Task ModelChange_Forgets()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:Acp:Enabled"] = "true" }, ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var directory = fixture.Services.GetRequiredService<Agency.Huddle.App.Data.ITeamDirectory>();
        var gateway = fixture.Services.GetRequiredService<Agency.Huddle.App.Pipes.IAgentGateway>();
        var chat = fixture.Services.GetRequiredService<Agency.Huddle.App.Services.ChatService>();
        var factory = new FakeAgentHostFactory { SessionPerRoom = true };
        var resolver = fixture.Services.GetRequiredService<AdapterProfileResolver>();
        using var skillStore = NewSkillStore(options);
        var roomSessions = new RoomSessionStore(options, NullLogger<RoomSessionStore>.Instance);
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, resolver, NewHealth(), new FakePromptSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance, skillStore, roomSessions: roomSessions);

        personaStore.Add(Identity("nova"), "You are Nova.", "a");
        await supervisor.StartAsync(ct);
        await WaitUntilAsync(() => factory.Calls.Count >= 1, ct);
        var novaId = await WaitForAgentOnlineAsync(directory, gateway, "nova", ct);
        var room = await chat.CreateRoomForAsync([novaId], ct);
        roomSessions.Put("nova", room.Id, new RoomSessionEntry("sess-1", "claude", "a", null, null, DateTimeOffset.UtcNow));

        personaStore.Update("nova", PersonaText("nova", "You are Nova."), "b", effort: null);

        await WaitUntilAsync(() => factory.Calls.Count >= 2, ct);
        await supervisor.StopAsync(ct);

        Assert.Null(roomSessions.Get("nova", room.Id));
    }

    /// <summary>RS §6.13: an app restart (a plain <see cref="PersonaSupervisor.StopAsync"/>, with nothing calling Restart) forgets nothing, so every Room resumes on its next Turn.</summary>
    [Fact]
    public async Task SupervisorStop_KeepsEntries()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:Acp:Enabled"] = "true" }, ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var directory = fixture.Services.GetRequiredService<Agency.Huddle.App.Data.ITeamDirectory>();
        var gateway = fixture.Services.GetRequiredService<Agency.Huddle.App.Pipes.IAgentGateway>();
        var chat = fixture.Services.GetRequiredService<Agency.Huddle.App.Services.ChatService>();
        var factory = new FakeAgentHostFactory { SessionPerRoom = true };
        var resolver = fixture.Services.GetRequiredService<AdapterProfileResolver>();
        using var skillStore = NewSkillStore(options);
        var roomSessions = new RoomSessionStore(options, NullLogger<RoomSessionStore>.Instance);
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, resolver, NewHealth(), new FakePromptSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance, skillStore, roomSessions: roomSessions);

        personaStore.Add(Identity("nova"), "You are Nova.");
        await supervisor.StartAsync(ct);
        await WaitUntilAsync(() => factory.Calls.Count >= 1, ct);
        var novaId = await WaitForAgentOnlineAsync(directory, gateway, "nova", ct);
        var room = await chat.CreateRoomForAsync([novaId], ct);
        roomSessions.Put("nova", room.Id, new RoomSessionEntry("sess-1", "claude", null, null, null, DateTimeOffset.UtcNow));

        await supervisor.StopAsync(ct);

        Assert.NotNull(roomSessions.Get("nova", room.Id));
    }

    /// <summary>RS §6.13 / E-12: starting a Persona's runner in shared mode forgets every stored Room Session for it, so a later switch back to per-Room starts fresh rather than resuming a stale entry.</summary>
    [Fact]
    public async Task SharedModeStart_ForgetsAll()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:Acp:Enabled"] = "true" }, ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var factory = new FakeAgentHostFactory(); // SessionPerRoom defaults false - shared mode.
        var resolver = fixture.Services.GetRequiredService<AdapterProfileResolver>();
        using var skillStore = NewSkillStore(options);
        var roomSessions = new RoomSessionStore(options, NullLogger<RoomSessionStore>.Instance);
        roomSessions.Put("nova", "room-1", new RoomSessionEntry("sess-1", "claude", null, null, null, DateTimeOffset.UtcNow));
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, resolver, NewHealth(), new FakePromptSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance, skillStore, roomSessions: roomSessions);

        personaStore.Add(Identity("nova"), "You are Nova.");
        await supervisor.StartAsync(ct);
        await WaitUntilAsync(() => factory.Calls.Count >= 1, ct);
        await supervisor.StopAsync(ct);

        Assert.Null(roomSessions.Get("nova", "room-1"));
    }
}