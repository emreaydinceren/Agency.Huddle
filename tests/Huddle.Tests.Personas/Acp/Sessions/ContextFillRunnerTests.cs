namespace Agency.Huddle.Tests.Acp.Sessions;

using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Waiting;
using Agency.Huddle.Tests.Factories;

/// <summary>
/// Pins the <see cref="ContextFill"/> lifecycle at the runner level: a <see cref="ContextFill"/> passed
/// to <see cref="PersonaRunner"/> is cleared for the Agent at Welcome and again when the runner is
/// disposed, after the session pool, so a last usage update cannot bring an entry back. Harness: a real
/// <see cref="PersonaRunner"/> over a hand-scripted pipe server, shared mode.
/// </summary>
public sealed class ContextFillRunnerTests
{
    /// <summary>Welcome clears a stale reading left for this runner's Agent, and leaves another Agent's alone.</summary>
    [Fact]
    public async Task Welcome_ClearsAStaleReadingForThatAgent()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var ct = cts.Token;
        await using var server = new PipeRunnerServer("context-fill-welcome-tests", recordReceived: false);
        var factory = new FakeAgentHostFactory();
        var room = GroupRoom("room-a", "Room A");
        var options = TestOptions.Create(opts => opts.PipeName = server.PipeName);
        ContextFill fill = new();
        fill.Report("agent-1", "room-a", 90_000, 200_000);
        fill.Report("agent-2", "room-a", 10_000, 200_000);

        await using var runner = new PersonaRunner(
            TestPersonas.Nova, options, factory, new FakePromptSource(), new RoomFollows(), NullLogger<PersonaRunner>.Instance, contextFill: fill);
        await server.HandshakeAsync(runner, [room], ct);

        Assert.Null(fill.Get("agent-1", "room-a"));
        Assert.NotNull(fill.Get("agent-2", "room-a"));
    }

    /// <summary>
    /// Disposing the runner clears the Agent's readings, including one a session reports while the pool
    /// is being disposed: the clear comes after the pool, so the late report cannot survive it.
    /// </summary>
    [Fact]
    public async Task Dispose_ClearsTheReading_EvenOneReportedDuringPoolDisposal()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var ct = cts.Token;
        await using var server = new PipeRunnerServer("context-fill-dispose-tests", recordReceived: false);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReplyWithUsage([50_000], "done");
        var room = GroupRoom("room-a", "Room A");
        var options = TestOptions.Create(opts => opts.PipeName = server.PipeName);
        ContextFill fill = new();
        factory.OnHostStarted = host => host.OnDispose = () => fill.Report("agent-1", null, 60_000, 200_000);

        var runner = new PersonaRunner(
            TestPersonas.Nova, options, factory, new FakePromptSource(), new RoomFollows(), NullLogger<PersonaRunner>.Instance, contextFill: fill);
        await server.HandshakeAsync(runner, [room], ct);
        await server.SendAsync(Posted(room, "hello"), ct);
        await server.ReceiveUntilAsync<PostMessage>(null, ct);
        await Eventually.UntilAsync(() => fill.Get("agent-1", "room-a")?.Used == 50_000, ct, interval: TimeSpan.FromMilliseconds(20));

        await runner.DisposeAsync();

        Assert.True(factory.Host?.Disposed);
        Assert.Null(fill.Get("agent-1", "room-a"));
    }

    private static RoomInfo GroupRoom(string id, string name) =>
        new(
            id,
            name,
            [
                new MemberInfo("human", "Human", UserKind.Human),
                new MemberInfo("agent-1", "nova", UserKind.Agent),
            ]);

    private static MessagePosted Posted(RoomInfo room, string text) =>
        new(
            room.Id,
            room.Name,
            new ChatMessage(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, "human", "Human", text),
            Mentioned: true,
            Mentions: [],
            Members: room.Members,
            AgentMessagesSinceHuman: 0,
            Budget: 0);
}
