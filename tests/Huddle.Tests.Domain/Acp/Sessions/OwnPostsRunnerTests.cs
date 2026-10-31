namespace Agency.Huddle.Tests.Acp.Sessions;

using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Factories;

/// <summary>
/// Pins <see cref="OwnPosts"/> at the runner level (D27 correction 10): the Take call the read loop
/// makes beside <c>TakeCatchUp</c>, and correction 11's drop rule. Harness: a real
/// <see cref="PersonaRunner"/> over a hand-scripted pipe server (<see cref="PipeRunnerServer"/>, which can refuse the
/// <see cref="ReadTranscript"/> answer where a test needs it), per-Room mode.
/// </summary>
public sealed class OwnPostsRunnerTests
{
    /// <summary>
    /// A post <see cref="OwnPosts.Record"/> makes into Room B while Room A's Turn is the one making
    /// it (U12: simulated directly, since the fake invokes no App Tool) appears as a
    /// <c>turn.ownPostLine</c> in Room B's own next Turn - its SECOND, so the drop rule (correction
    /// 11) does not eat it on the first.
    /// </summary>
    [Fact]
    public async Task PostFromRoomA_IntoRoomB_AppearsInBsNextTurn()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var ct = cts.Token;

        await using var server = new PipeRunnerServer("own-posts-runner-tests", recordReceived: false);
        var factory = new FakeAgentHostFactory { SessionPerRoom = true };
        var persona = TestPersonas.Nova;
        var roomB = GroupRoom("room-b", "Room B");
        var options = TestOptions.Create(opts => opts.PipeName = server.PipeName);
        var ownPosts = new OwnPosts(options);

        await using var runner = new PersonaRunner(
            persona, options, factory, new FakePromptSource(), new RoomFollows(), NullLogger<PersonaRunner>.Instance, ownPosts: ownPosts);
        await server.HandshakeAsync(runner, [roomB], ct);

        await server.SendAsync(Posted(roomB, "b's first"), ct);
        await server.ReceiveUntilAsync<PostMessage>(null, ct);

        // Waits for the Turn's own final MessageDelta, not just its PostMessage reply: EndTurn runs
        // earlier in the same finally block, so only the final delta guarantees Room B's own Busy
        // mark has already cleared before this Record call - recording right after the reply text
        // would race RoomSession's own EndTurn call.
        await server.ReceiveUntilAsync<MessageDelta>(static d => d.IsFinal, ct);

        ownPosts.Record("agent-1", roomB.Id, "posted from room A");

        await server.SendAsync(Posted(roomB, "b's second"), ct);
        await server.ReceiveUntilAsync<PostMessage>(null, ct);

        var sessionB = Assert.Single(factory.Host!.Sessions);
        Assert.Contains("You, from another Room: posted from room A", sessionB.Prompts[^1], StringComparison.Ordinal);
    }

    /// <summary>
    /// Correction 11: an own post recorded before a Room Session's very first Turn is dropped -
    /// the Transcript range that Turn reads already holds it, even when that range turns out empty.
    /// </summary>
    [Fact]
    public async Task OwnPostsDroppedOnFirstTurnAfterOpen()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var ct = cts.Token;

        await using var server = new PipeRunnerServer("own-posts-runner-tests", recordReceived: false);
        var factory = new FakeAgentHostFactory { SessionPerRoom = true };
        var persona = TestPersonas.Nova;
        var roomB = GroupRoom("room-b", "Room B");
        var options = TestOptions.Create(opts => opts.PipeName = server.PipeName);
        var ownPosts = new OwnPosts(options);

        await using var runner = new PersonaRunner(
            persona, options, factory, new FakePromptSource(), new RoomFollows(), NullLogger<PersonaRunner>.Instance, ownPosts: ownPosts);
        await server.HandshakeAsync(runner, [roomB], ct);

        ownPosts.Record("agent-1", roomB.Id, "should be dropped");

        await server.SendAsync(Posted(roomB, "b's first"), ct);
        await server.ReceiveUntilAsync<PostMessage>(null, ct);

        var sessionB = Assert.Single(factory.Host!.Sessions);
        Assert.DoesNotContain("should be dropped", sessionB.Prompts[^1], StringComparison.Ordinal);
    }

    /// <summary>
    /// Correction 11: when the first Turn's own Transcript read is refused (RS §9 E-3), own-post
    /// lines recorded before it still render - the range that would have held them was never read.
    /// </summary>
    [Fact]
    public async Task OwnPostsRender_WhenTranscriptReadFails()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var ct = cts.Token;

        await using var server = new PipeRunnerServer("own-posts-runner-tests", recordReceived: false) { RefuseTranscriptReads = true };
        var factory = new FakeAgentHostFactory { SessionPerRoom = true };
        var persona = TestPersonas.Nova;
        var roomB = GroupRoom("room-b", "Room B");
        var options = TestOptions.Create(opts => opts.PipeName = server.PipeName);
        var ownPosts = new OwnPosts(options);

        await using var runner = new PersonaRunner(
            persona, options, factory, new FakePromptSource(), new RoomFollows(), NullLogger<PersonaRunner>.Instance, ownPosts: ownPosts);
        await server.HandshakeAsync(runner, [roomB], ct);

        ownPosts.Record("agent-1", roomB.Id, "should still render");

        await server.SendAsync(Posted(roomB, "b's first"), ct);
        await server.ReceiveUntilAsync<PostMessage>(null, ct);

        var sessionB = Assert.Single(factory.Host!.Sessions);
        Assert.Contains("You, from another Room: should still render", sessionB.Prompts[^1], StringComparison.Ordinal);
    }

    private static RoomInfo GroupRoom(string id, string name) =>
        new(
            id,
            name,
            [
                new MemberInfo("human", "Human", UserKind.Human),
                new MemberInfo("agent-1", "nova", UserKind.Agent),
                new MemberInfo("friend", "Friend", UserKind.Agent),
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
