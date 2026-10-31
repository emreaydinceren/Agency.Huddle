namespace Agency.Huddle.Tests.Acp.Sessions;

using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Waiting;
using Agency.Huddle.Tests.Factories;

/// <summary>
/// Pins <see cref="TurnActivity"/> at the runner level (D8 correction 6): a
/// <see cref="TurnActivity"/> passed to <see cref="PersonaRunner"/>'s constructor reaches the
/// <see cref="RoomSession"/> the runner opens. Harness: a real <see cref="PersonaRunner"/> over a
/// hand-scripted pipe server (<see cref="PipeRunnerServer"/>), shared mode (the runner's
/// default), so the Turn is held open through <see cref="FakeAgentHostFactory.Session"/>.
/// </summary>
public sealed class TurnActivityRunnerTests
{
    /// <summary>
    /// A held-open Turn marks <see cref="TurnActivity"/> busy for this runner's Agent id in the Room
    /// the Message was posted into, and clears once the Turn completes.
    /// </summary>
    [Fact]
    public async Task HeldTurn_MarksTurnActivityBusy_ThenClears()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var ct = cts.Token;

        await using var server = new PipeRunnerServer("turn-activity-runner-tests", recordReceived: false);
        var factory = new FakeAgentHostFactory();
        var release = new TaskCompletionSource();
        factory.Session.EnqueueGatedReply(release.Task, "held reply");
        var persona = TestPersonas.Nova;
        var room = GroupRoom("room-a", "Room A");
        var options = TestOptions.Create(opts => opts.PipeName = server.PipeName);
        var turnActivity = new TurnActivity();

        await using var runner = new PersonaRunner(
            persona, options, factory, new FakePromptSource(), new RoomFollows(), NullLogger<PersonaRunner>.Instance, turnActivity: turnActivity);
        await server.HandshakeAsync(runner, [room], ct);

        await server.SendAsync(Posted(room, "hello"), ct);

        await Eventually.UntilAsync(() => turnActivity.IsBusyIn("agent-1", room.Id), ct, interval: TimeSpan.FromMilliseconds(20));
        Assert.True(turnActivity.IsBusyIn("agent-1", room.Id));

        release.SetResult();

        await server.ReceiveUntilAsync<PostMessage>(null, ct);
        await Eventually.UntilAsync(() => !turnActivity.IsBusyIn("agent-1", room.Id), ct, interval: TimeSpan.FromMilliseconds(20));
        Assert.False(turnActivity.IsBusyIn("agent-1", room.Id));
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
