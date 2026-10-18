using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.Acp.Tests.Fakes;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;

namespace Agency.Huddle.Tests.Conformance;

/// <summary>
/// Pins RS §10's MockAdapter bullet — several Room Sessions, on one Adapter process, opening,
/// idling closed and resuming — against the real factory through <see cref="MockAdapterFixture"/>,
/// rather than against the lower-level <c>PersonaHostTests</c> (D19) this builds on. Every wait below
/// is on a real completion signal (a <see cref="FakeAcpAgent.WaitForAsync"/>, a
/// <see cref="RoomEvents.MessagePosted"/> subscription, or a poll of the real
/// <see cref="RoomSessionStore"/>), the same idiom <see cref="TurnLifecycleTests"/> already
/// establishes, and <see cref="ManualTimeProvider"/> drives the idle sweep deterministically instead
/// of waiting out real wall-clock minutes.
/// </summary>
/// <remarks>
/// <b><see cref="TwoRooms_TwoSessionNewCalls_OneProcess"/> used to be skipped here for a known,
/// reported defect; it no longer is.</b> With two Room Sessions concurrently live on one Adapter
/// process, the SECOND one ever Turned — whichever physical Room it was; swapping post order moved
/// the failure with it — completed its <c>session/prompt</c> RPC successfully (<c>stopReason:
/// end_turn</c> was returned and recorded in <see cref="FakeAcpAgent.Received"/>), but the reply text
/// came back empty and nothing was posted into the Room:
/// <c>Agency.Huddle.App.Acp.Sessions.RoomSession.AppendAndPublishDeltaAsync</c> dropped the
/// <c>MessageChunk</c> because <c>this.activeTurn</c> read back <see langword="null"/>. Root cause,
/// confirmed empirically: <c>src/Huddle.Acp/DotAcp/DotAcpAgentSession.cs</c>'s <c>PromptAsync</c>
/// published <c>TurnCompleted</c> the instant its own <c>session/prompt</c> RPC returned, with no
/// ordering guarantee against a concurrently in-flight <c>session/update</c> notification dispatch
/// (StreamJsonRpc completes an outbound request through a different path than the one that invokes an
/// inbound notification's target method) — a race a single-session Turn rarely exercises enough to
/// surface. Traced with temporary instrumentation (since reverted); see
/// <c>Conversation/red-D30-30.1.i-fix.txt</c> for that capture and
/// <c>DotAcpAgentSession.WaitForQuietDispatchAsync</c>'s remarks for the root cause writeup and the
/// bounded quiet-window mitigation now in place there (Huddle.Acp, per root <c>CLAUDE.md</c>'s "Two
/// owners" table, the repo owner authorised this edit).
/// </remarks>
public sealed class RoomSessionConformanceTests
{
    /// <summary>
    /// RS §10: two Rooms, each with their own Room Session, run over one Adapter process. Proven two
    /// ways: two distinct <c>session/new</c> calls minted two distinct stored session ids (read back
    /// from the real <see cref="RoomSessionStore"/>, exactly as the app itself resumes from), and
    /// <c>initialize</c> — sent exactly once, when the process itself starts — appears only once, so
    /// a second process was never spawned to serve the second Room.
    /// </summary>
    [Fact]
    public async Task TwoRooms_TwoSessionNewCalls_OneProcess()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Persona persona = new("nova", "You are Nova.");

        await using MockAdapterFixture fixture = await MockAdapterFixture.StartAsync(persona, cancellationToken: ct);
        ITeamDirectory directory = fixture.Services.GetRequiredService<ITeamDirectory>();
        ChatService chat = fixture.Services.GetRequiredService<ChatService>();
        RoomEvents roomEvents = fixture.Services.GetRequiredService<RoomEvents>();
        RoomSessionStore roomSessions = fixture.Services.GetRequiredService<RoomSessionStore>();

        User? nova = await directory.FindUserByNameAsync("nova", ct);
        Assert.NotNull(nova);

        Room roomA = await chat.CreateRoomForAsync([nova.Id], ct);
        // CreateRoomForAsync with one Agent id reuses the existing direct Room for that Agent (D30
        // correction 17), so the second Room this test needs comes straight from the Team Directory.
        Room roomB = await directory.CreateRoomAsync("Second Room", [KnownIds.Human, nova.Id], ct);

        await PostAndAwaitReplyAsync(chat, roomEvents, roomA.Id, nova.Id, "hello in room A", ct);
        await PostAndAwaitReplyAsync(chat, roomEvents, roomB.Id, nova.Id, "hello in room B", ct);

        RoomSessionEntry? entryA = await WaitForStoredEntryAsync(roomSessions, "nova", roomA.Id, ct);
        RoomSessionEntry? entryB = await WaitForStoredEntryAsync(roomSessions, "nova", roomB.Id, ct);
        Assert.NotNull(entryA);
        Assert.NotNull(entryB);
        Assert.NotEqual(entryA.SessionId, entryB.SessionId);

        Assert.Equal(2, fixture.Agent.Received.Count(m => string.Equals((string?)m["method"], "session/new", StringComparison.Ordinal)));
        Assert.Single(fixture.Agent.Received, m => string.Equals((string?)m["method"], "initialize", StringComparison.Ordinal));
    }

    /// <summary>RS §6.2: once a Room Session has been Idle with an empty queue past <see cref="Agency.Huddle.App.Acp.AcpOptions.SessionIdleMinutes"/>, the idle sweep sends <c>session/close</c> for it.</summary>
    [Fact]
    public async Task IdleSweep_SendsSessionClose()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Persona persona = new("nova", "You are Nova.");
        ManualTimeProvider time = new();
        Dictionary<string, string?> config = new(StringComparer.Ordinal)
        {
            ["Team:Acp:SessionIdleMinutes"] = "1",
        };

        await using MockAdapterFixture fixture =
            await MockAdapterFixture.StartAsync(persona, config, time, cancellationToken: ct);
        ITeamDirectory directory = fixture.Services.GetRequiredService<ITeamDirectory>();
        ChatService chat = fixture.Services.GetRequiredService<ChatService>();
        RoomEvents roomEvents = fixture.Services.GetRequiredService<RoomEvents>();
        RoomSessionStore roomSessions = fixture.Services.GetRequiredService<RoomSessionStore>();

        User? nova = await directory.FindUserByNameAsync("nova", ct);
        Assert.NotNull(nova);
        Room room = await chat.CreateRoomForAsync([nova.Id], ct);

        await PostAndAwaitReplyAsync(chat, roomEvents, room.Id, nova.Id, "hello nova", ct);
        await WaitForStoredEntryAsync(roomSessions, "nova", room.Id, ct);

        time.Advance(TimeSpan.FromMinutes(2));
        await fixture.Runner.SweepIdleSessionsAsync();

        JsonObject closeRequest = await fixture.Agent.WaitForAsync("session/close", TimeSpan.FromSeconds(5));
        ct.ThrowIfCancellationRequested();
        Assert.NotNull(closeRequest);
    }

    /// <summary>RS §6.2 / §6.6: the next Turn after an idle-swept close resumes the same session id the earlier Turn stored, rather than opening fresh.</summary>
    [Fact]
    public async Task NextTurnAfterClose_SendsSessionResumeWithStoredId()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Persona persona = new("nova", "You are Nova.");
        ManualTimeProvider time = new();
        Dictionary<string, string?> config = new(StringComparer.Ordinal)
        {
            ["Team:Acp:SessionIdleMinutes"] = "1",
        };

        await using MockAdapterFixture fixture =
            await MockAdapterFixture.StartAsync(persona, config, time, cancellationToken: ct);
        ITeamDirectory directory = fixture.Services.GetRequiredService<ITeamDirectory>();
        ChatService chat = fixture.Services.GetRequiredService<ChatService>();
        RoomEvents roomEvents = fixture.Services.GetRequiredService<RoomEvents>();
        RoomSessionStore roomSessions = fixture.Services.GetRequiredService<RoomSessionStore>();

        User? nova = await directory.FindUserByNameAsync("nova", ct);
        Assert.NotNull(nova);
        Room room = await chat.CreateRoomForAsync([nova.Id], ct);

        await PostAndAwaitReplyAsync(chat, roomEvents, room.Id, nova.Id, "hello nova", ct);
        RoomSessionEntry firstEntry = await WaitForStoredEntryAsync(roomSessions, "nova", room.Id, ct);

        time.Advance(TimeSpan.FromMinutes(2));
        await fixture.Runner.SweepIdleSessionsAsync();
        await fixture.Agent.WaitForAsync("session/close", TimeSpan.FromSeconds(5));

        await PostAndAwaitReplyAsync(chat, roomEvents, room.Id, nova.Id, "still there?", ct);

        JsonObject resumeRequest = await fixture.Agent.WaitForAsync("session/resume", TimeSpan.FromSeconds(5));
        ct.ThrowIfCancellationRequested();
        string? resumedSessionId = (string?)(resumeRequest["params"] as JsonObject)?["sessionId"];
        Assert.Equal(firstEntry.SessionId, resumedSessionId);
    }

    /// <summary>Posts <paramref name="text"/> as the Human into <paramref name="roomId"/> and waits for <paramref name="agentId"/>'s reply to land in the Transcript.</summary>
    private static async Task PostAndAwaitReplyAsync(
        ChatService chat, RoomEvents roomEvents, string roomId, string agentId, string text, CancellationToken ct)
    {
        TaskCompletionSource<ChatMessage> repliedSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnMessagePosted(MessagePostedEvent posted)
        {
            if (string.Equals(posted.Room.Id, roomId, StringComparison.Ordinal) &&
                string.Equals(posted.Message.SenderId, agentId, StringComparison.Ordinal))
            {
                repliedSource.TrySetResult(posted.Message);
            }
        }

        roomEvents.MessagePosted += OnMessagePosted;
        try
        {
            await chat.PostAsync(roomId, KnownIds.Human, text, ct: ct);
            _ = await repliedSource.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        }
        finally
        {
            roomEvents.MessagePosted -= OnMessagePosted;
        }
    }

    /// <summary>Polls the real <see cref="RoomSessionStore"/> until <paramref name="roomId"/>'s Turn-end write for <paramref name="name"/> has committed (P-17), rather than waiting a fixed delay.</summary>
    private static async Task<RoomSessionEntry> WaitForStoredEntryAsync(RoomSessionStore roomSessions, string name, string roomId, CancellationToken ct)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);

        while (true)
        {
            RoomSessionEntry? entry = roomSessions.Get(name, roomId);
            if (entry is not null)
            {
                return entry;
            }

            linked.Token.ThrowIfCancellationRequested();
            await Task.Delay(20, linked.Token);
        }
    }
}
