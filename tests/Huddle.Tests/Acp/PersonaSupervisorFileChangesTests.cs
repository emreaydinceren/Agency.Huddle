using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Skills;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Conformance;
using Agency.Huddle.Tests.Pipes;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// Proves *which* <see cref="FileChangeTracker"/> a <see cref="PersonaSupervisor"/>-started runner
/// actually got, per FC §6.10 - something a construction-only <see cref="PersonaSupervisorTests"/>
/// test cannot show, because it needs a Turn to run. Built over <see cref="PipeHostFixture"/> (which
/// removes the hosted <see cref="PersonaSupervisor"/>, see <see cref="PipeHostFixture.RemovePersonaSupervisorHostedService"/>)
/// with a hand-built <see cref="PersonaSupervisor"/> wired to a <see cref="FakeAgentHostFactory"/>, so
/// no test here spends a token or launches <c>node</c>.
/// </summary>
public sealed class PersonaSupervisorFileChangesTests
{
    /// <summary>With <c>Team:FileChanges:Enabled</c> at its default (true), a Turn writes <c>{DataDir}/file-state/&lt;Name&gt;.json</c>.</summary>
    [Fact]
    public async Task Supervisor_FileChangesEnabled_TurnWritesFileState()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:Acp:Enabled"] = "true" }, ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReply("ok");
        var resolver = fixture.Services.GetRequiredService<AdapterProfileResolver>();
        var tracker = fixture.Services.GetRequiredService<FileChangeTracker>();
        using var skillStore = new SkillStore(options, NullLogger<SkillStore>.Instance);
        using var supervisor = new PersonaSupervisor(
            options, personaStore, factory, resolver, NewHealth(), new FakePromptSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance, skillStore, tracker);

        personaStore.Add(new PersonaIdentity("nova", "nova", "nova", []), "You are Nova.");
        await supervisor.StartAsync(ct);

        var (_, roomId) = await WaitForDirectRoomAsync(fixture, "nova", ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();
        await chat.PostAsync(roomId, KnownIds.Human, "hi", ct: ct);
        await WaitForHistoryCountAsync(store, roomId, 2, ct);

        await supervisor.StopAsync(ct);

        var stateFile = Path.Combine(options.Value.DataDir, "file-state", "nova.json");
        Assert.True(File.Exists(stateFile));
    }

    /// <summary>With <c>Team:FileChanges:Enabled</c> false, the runner gets no tracker (FC §6.10) and a Turn writes no state file at all.</summary>
    [Fact]
    public async Task Supervisor_FileChangesDisabled_TurnWritesNoFileState()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:Acp:Enabled"] = "true", ["Team:FileChanges:Enabled"] = "false" }, ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReply("ok");
        var resolver = fixture.Services.GetRequiredService<AdapterProfileResolver>();
        var tracker = fixture.Services.GetRequiredService<FileChangeTracker>();
        using var skillStore = new SkillStore(options, NullLogger<SkillStore>.Instance);
        using var supervisor = new PersonaSupervisor(
            options, personaStore, factory, resolver, NewHealth(), new FakePromptSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance, skillStore, tracker);

        personaStore.Add(new PersonaIdentity("nova", "nova", "nova", []), "You are Nova.");
        await supervisor.StartAsync(ct);

        var (_, roomId) = await WaitForDirectRoomAsync(fixture, "nova", ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();
        await chat.PostAsync(roomId, KnownIds.Human, "hi", ct: ct);
        await WaitForHistoryCountAsync(store, roomId, 2, ct);

        await supervisor.StopAsync(ct);

        var stateFile = Path.Combine(options.Value.DataDir, "file-state", "nova.json");
        Assert.False(File.Exists(stateFile));
    }

    /// <summary>
    /// FC §6.11: with the resolved Adapter Profile's <c>ReadsFiles</c> false, the supervisor passes no
    /// tracker to the runner even though <c>Team:FileChanges:Enabled</c> is left at its default (true)
    /// and a real <see cref="FileChangeTracker"/> singleton is passed to this supervisor's
    /// constructor - a Turn on that Adapter writes no state file at all.
    /// </summary>
    [Fact]
    public async Task Supervisor_ReadsFilesFalse_TurnWritesNoFileState()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?>
            {
                ["Team:Acp:Enabled"] = "true",
                ["Team:Acp:Adapters:0:Id"] = "agency",
                ["Team:Acp:Adapters:0:Command"] = "agency-acp",
                ["Team:Acp:Adapters:0:ReadsFiles"] = "false",
            },
            ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReply("ok");
        var resolver = fixture.Services.GetRequiredService<AdapterProfileResolver>();
        var tracker = fixture.Services.GetRequiredService<FileChangeTracker>();
        using var skillStore = new SkillStore(options, NullLogger<SkillStore>.Instance);
        using var supervisor = new PersonaSupervisor(
            options, personaStore, factory, resolver, NewHealth(), new FakePromptSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance, skillStore, tracker);

        personaStore.Add(new PersonaIdentity("nova", "nova", "nova", []) { Adapter = "agency" }, "You are Nova.");
        await supervisor.StartAsync(ct);

        var (_, roomId) = await WaitForDirectRoomAsync(fixture, "nova", ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();
        await chat.PostAsync(roomId, KnownIds.Human, "hi", ct: ct);
        await WaitForHistoryCountAsync(store, roomId, 2, ct);

        await supervisor.StopAsync(ct);

        var stateFile = Path.Combine(options.Value.DataDir, "file-state", "nova.json");
        Assert.False(File.Exists(stateFile));
    }

    /// <summary>Finding P-13's real production wiring: <see cref="PersonaSupervisor"/> passes its own <see cref="FileChangeTracker"/> through to the runner it starts, over the real <see cref="Agency.Huddle.App.Acp.DotAcpAgentHostFactory"/>.</summary>
    [Fact]
    public async Task MockAdapterFixture_PassesTracker()
    {
        var ct = TestContext.Current.CancellationToken;
        var persona = new Persona("nova", "You are Nova.");

        await using var fixture = await MockAdapterFixture.StartAsync(persona, cancellationToken: ct);
        var directory = fixture.Services.GetRequiredService<ITeamDirectory>();
        var user = await directory.FindUserByNameAsync("nova", ct);
        Assert.NotNull(user);
        var room = await directory.FindRoomWithExactMembersAsync(KnownIds.Human, user.Id, ct);
        Assert.NotNull(room);

        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();
        await chat.PostAsync(room.Id, KnownIds.Human, "hi", ct: ct);
        await WaitForHistoryCountAsync(store, room.Id, 2, ct);

        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        var stateFile = Path.Combine(options.Value.DataDir, "file-state", "nova.json");
        Assert.True(File.Exists(stateFile));
    }

    /// <summary>
    /// D24: <see cref="PersonaSupervisor"/> passes its own trailing <see cref="RoomSessionStore"/>
    /// through to the runner it starts - after one per-Room Turn, that Persona's file exists under
    /// <c>{DataDir}/room-sessions/</c>.
    /// </summary>
    [Fact]
    public async Task Supervisor_PassesRoomSessionStore()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:Acp:Enabled"] = "true" }, ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var factory = new FakeAgentHostFactory { SessionPerRoom = true };
        factory.Session.EnqueueReply("ok");
        var resolver = fixture.Services.GetRequiredService<AdapterProfileResolver>();
        var roomSessions = fixture.Services.GetRequiredService<RoomSessionStore>();
        using var skillStore = new SkillStore(options, NullLogger<SkillStore>.Instance);
        using var supervisor = new PersonaSupervisor(
            options, personaStore, factory, resolver, NewHealth(), new FakePromptSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance, skillStore, roomSessions: roomSessions);

        personaStore.Add(new PersonaIdentity("nova", "nova", "nova", []), "You are Nova.");
        await supervisor.StartAsync(ct);

        var (_, roomId) = await WaitForDirectRoomAsync(fixture, "nova", ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();
        await chat.PostAsync(roomId, KnownIds.Human, "hi", ct: ct);
        await WaitForHistoryCountAsync(store, roomId, 2, ct);

        await supervisor.StopAsync(ct);

        var roomSessionFile = Path.Combine(options.Value.DataDir, "room-sessions", "nova.json");
        Assert.True(File.Exists(roomSessionFile));
    }

    /// <summary>
    /// D27, finding P-7: <see cref="PersonaSupervisor"/> passes its own trailing
    /// <see cref="OwnPosts"/> through to the runner it starts. Shared mode's <c>BeginTurn(agent, null)</c>
    /// marks every Room Busy for the Turn's duration, which is the observable this test checks: a
    /// <see cref="OwnPosts.Record"/> call made while the held-open Turn is in flight is refused.
    /// </summary>
    [Fact]
    public async Task Supervisor_PassesOwnPosts()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:Acp:Enabled"] = "true" }, ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var factory = new FakeAgentHostFactory();
        var release = new TaskCompletionSource();
        factory.Session.EnqueueGatedReply(release.Task, "ok");
        var resolver = fixture.Services.GetRequiredService<AdapterProfileResolver>();
        var ownPosts = new OwnPosts(options);
        using var skillStore = new SkillStore(options, NullLogger<SkillStore>.Instance);
        using var supervisor = new PersonaSupervisor(
            options, personaStore, factory, resolver, NewHealth(), new FakePromptSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance, skillStore, ownPosts: ownPosts);

        personaStore.Add(new PersonaIdentity("nova", "nova", "nova", []), "You are Nova.");
        await supervisor.StartAsync(ct);

        var (agentId, roomId) = await WaitForDirectRoomAsync(fixture, "nova", ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();
        await chat.PostAsync(roomId, KnownIds.Human, "hi", ct: ct);

        // Waits for the Turn to actually be in flight (PromptAsync reached), rather than a fixed
        // delay: only then is shared mode's BeginTurn(agent, null) guaranteed to have marked every
        // Room Busy for this Agent.
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (factory.Session.Prompts.Count == 0 && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(20, ct);
        }

        Assert.NotEmpty(factory.Session.Prompts);
        ownPosts.Record(agentId, roomId, "should be refused while the Turn is in flight");
        Assert.Empty(ownPosts.Take(agentId, roomId));

        release.SetResult();
        await supervisor.StopAsync(ct);
    }

    /// <summary>Builds a fresh <see cref="PersonaHealth"/> against the real clock.</summary>
    private static PersonaHealth NewHealth() => new(TimeProvider.System, NullLogger<PersonaHealth>.Instance);

    /// <summary>Waits until <paramref name="agentName"/> is registered and Online, then returns its id and its direct Room with the Human.</summary>
    private static async Task<(string AgentId, string RoomId)> WaitForDirectRoomAsync(
        PipeHostFixture fixture, string agentName, CancellationToken ct)
    {
        var directory = fixture.Services.GetRequiredService<ITeamDirectory>();
        var gateway = fixture.Services.GetRequiredService<Agency.Huddle.App.Pipes.IAgentGateway>();

        while (true)
        {
            var user = await directory.FindUserByNameAsync(agentName, ct);
            if (user is not null && gateway.IsOnline(user.Id))
            {
                var room = await directory.FindRoomWithExactMembersAsync(KnownIds.Human, user.Id, ct);
                if (room is not null)
                {
                    return (user.Id, room.Id);
                }
            }

            await Task.Delay(50, ct);
        }
    }

    /// <summary>Waits until <paramref name="roomId"/>'s Transcript holds at least <paramref name="count"/> Messages.</summary>
    private static async Task<IReadOnlyList<ChatMessage>> WaitForHistoryCountAsync(
        IChatStore store, string roomId, int count, CancellationToken ct)
    {
        while (true)
        {
            var history = await store.ReadAllAsync(roomId, ct);
            if (history.Count >= count)
            {
                return history;
            }

            await Task.Delay(50, ct);
        }
    }
}
