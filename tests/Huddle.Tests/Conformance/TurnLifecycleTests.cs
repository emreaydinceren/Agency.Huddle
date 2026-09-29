using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.Acp.Tests.Fakes;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using Agency.Huddle.MockAdapter;

namespace Agency.Huddle.Tests.Conformance;

/// <summary>
/// Task 10.5 (Spec §15.8, T-21, T-25, T-26, T-27, T-28): drives a real Persona through
/// <see cref="MockAdapterFixture"/> across a whole Turn's lifecycle rather than just its opening
/// handshake — streaming, a Human Stop, disposal, and an Adapter change — each one proved through the
/// real <see cref="PersonaRunner"/>, the real <c>ChatService</c>/<c>Drafts</c>/<c>RoomEvents</c>
/// plumbing and a real ACP peer, with no <c>Task.Delay</c> anywhere: every wait below is on a real
/// completion signal — a <see cref="FakeAcpAgent.WaitForAsync"/>, a <see cref="RoomEvents"/>
/// subscription, or a <see cref="PersonaRunner.StatusChanged"/> subscription — the same idiom the
/// three existing conformance tests already establish.
/// </summary>
/// <remarks>
/// <para>
/// <b>Formerly required a fixed test order; no longer does.</b> Investigating this task's fourth test
/// surfaced a real, deterministically reproducible bug in the ACP client's dispatch ordering
/// (<c>src/Huddle.Acp/DotAcp/DotAcpAgentSession.cs</c> and the <c>dotacp.client</c> package it wraps):
/// running the exact Turn sequence in
/// <see cref="StopTurnAsync_TurnInFlight_EndsStoppedAndLeavesTheFailureStreakUnbroken"/> (two failed
/// Turns, a Stopped Turn, then one more failed Turn, all on one session) left the process in a state
/// where the very next, entirely independent <see cref="MockAdapterFixture"/> session's multi-chunk
/// streamed reply was silently truncated to just its first chunk. This class carried a
/// <c>ChunkedReplyFirstOrderer</c> to force
/// <see cref="PostAsync_ChunkedReply_DeliversSeveralDraftUpdatesThenPersistsTheMessage"/> ahead of the
/// Stop test for exactly that reason. <c>DotAcpAgentSession.WaitForQuietDispatchAsync</c>'s remarks
/// document the root cause and the mitigation now in place (a bounded quiet-window wait before
/// publishing <c>TurnCompleted</c>, since the underlying dispatch race cannot be closed with a hard
/// barrier - see those remarks for why); with it, this class's own natural (unordered) test order
/// passes reliably, proved 10/10 before the orderer was removed, so it no longer carries one.
/// </para>
/// </remarks>
public sealed class TurnLifecycleTests
{
    /// <summary>
    /// T-21: the chunked-echo default (<see cref="MockBehaviour.ChunkedEchoAsync"/>) exists precisely
    /// to prove that a reply arrives incrementally rather than all at once. This asserts on the
    /// observable product of that on this side of the wire — <see cref="Drafts"/>, which every
    /// <c>MessageDelta</c> the real <see cref="PersonaRunner"/> writes is translated into by the real
    /// <c>AgentConnection</c> — growing across at least three deliveries before the finished reply
    /// lands in the Transcript.
    /// </summary>
    [Fact]
    public async Task PostAsync_ChunkedReply_DeliversSeveralDraftUpdatesThenPersistsTheMessage()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Persona persona = new("nova", "You are Nova.");

        await using MockAdapterFixture fixture = await MockAdapterFixture.StartAsync(persona, cancellationToken: ct);
        (string agentId, string roomId) = await GetDirectRoomAsync(fixture, "nova", ct);

        fixture.Agent.OnPrompt = MockBehaviour.ChunkedEchoAsync;

        Drafts drafts = fixture.Services.GetRequiredService<Drafts>();
        RoomEvents roomEvents = fixture.Services.GetRequiredService<RoomEvents>();
        ChatService chat = fixture.Services.GetRequiredService<ChatService>();
        IChatStore store = fixture.Services.GetRequiredService<IChatStore>();

        List<string> draftSnapshots = [];
        TaskCompletionSource<ChatMessage> repliedSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnDraftChanged(string changedRoomId)
        {
            if (!string.Equals(changedRoomId, roomId, StringComparison.Ordinal))
            {
                return;
            }

            Draft? draft = drafts.ForRoom(roomId).FirstOrDefault(d => string.Equals(d.AgentId, agentId, StringComparison.Ordinal));
            if (draft is { Text.Length: > 0 })
            {
                lock (draftSnapshots)
                {
                    draftSnapshots.Add(draft.Text);
                }
            }
        }

        void OnMessagePosted(MessagePostedEvent posted)
        {
            if (string.Equals(posted.Message.SenderId, agentId, StringComparison.Ordinal))
            {
                repliedSource.TrySetResult(posted.Message);
            }
        }

        roomEvents.DraftChanged += OnDraftChanged;
        roomEvents.MessagePosted += OnMessagePosted;
        try
        {
            await chat.PostAsync(roomId, KnownIds.Human, "hello nova, please say several things", ct: ct);

            ChatMessage reply = await repliedSource.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);

            int deltaCount;
            lock (draftSnapshots)
            {
                deltaCount = draftSnapshots.Count;
            }

            Assert.True(deltaCount >= 3, $"Expected at least 3 incremental draft updates before the reply posted, saw {deltaCount}.");
            Assert.False(string.IsNullOrEmpty(reply.Text));

            IReadOnlyList<ChatMessage> history = await store.ReadAllAsync(roomId, ct);
            Assert.Contains(history, m => string.Equals(m.Id, reply.Id, StringComparison.Ordinal));
        }
        finally
        {
            roomEvents.DraftChanged -= OnDraftChanged;
            roomEvents.MessagePosted -= OnMessagePosted;
        }
    }

    /// <summary>
    /// T-25/26: rules.md is explicit that "a Turn the Human stopped is not a failure" and that a Stop
    /// "reports no health state, raises no alert and does not break the consecutive-failure streak".
    /// This drives two ordinary Turn failures (so the streak has something in it), stops a third Turn
    /// mid-flight through the real <see cref="IAgentGateway.StopTurnAsync"/> — the same entry point the
    /// UI's Stop button calls — and proves a fourth failure afterward is reported as the streak's
    /// *third* consecutive failure, not its first: if the Stop had reset the counter, the fourth Turn's
    /// reason would read "A Turn in Room '...' failed", never "3 consecutive Turns have failed".
    /// </summary>
    [Fact]
    public async Task StopTurnAsync_TurnInFlight_EndsStoppedAndLeavesTheFailureStreakUnbroken()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Persona persona = new("nova", "You are Nova.");

        await using MockAdapterFixture fixture = await MockAdapterFixture.StartAsync(persona, cancellationToken: ct);
        (string agentId, string roomId) = await GetDirectRoomAsync(fixture, "nova", ct);

        int promptCallCount = 0;
        TaskCompletionSource thirdPromptStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        fixture.Agent.OnPrompt = context =>
        {
            int call = Interlocked.Increment(ref promptCallCount);
            return call switch
            {
                1 or 2 or 4 => Task.FromException<string>(new FakeRpcError(1, "boom")),
                3 => BlockUntilCancelledAsync(context, thirdPromptStarted),
                _ => Task.FromException<string>(new InvalidOperationException("Unexpected extra session/prompt call.")),
            };
        };

        List<PersonaStatus> statuses = [];
        TaskCompletionSource firstDegraded = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource secondDegraded = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource thirdDegraded = new(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnStatusChanged(PersonaStatus status)
        {
            lock (statuses)
            {
                statuses.Add(status);
                int degradedCount = statuses.Count(s => s.State == PersonaState.Degraded);
                switch (degradedCount)
                {
                    case 1:
                        firstDegraded.TrySetResult();
                        break;
                    case 2:
                        secondDegraded.TrySetResult();
                        break;
                    case 3:
                        thirdDegraded.TrySetResult();
                        break;
                }
            }
        }

        fixture.Runner.StatusChanged += OnStatusChanged;
        try
        {
            ChatService chat = fixture.Services.GetRequiredService<ChatService>();
            IAgentGateway gateway = fixture.Services.GetRequiredService<IAgentGateway>();
            IChatStore store = fixture.Services.GetRequiredService<IChatStore>();

            await chat.PostAsync(roomId, KnownIds.Human, "first - will fail", ct: ct);
            await firstDegraded.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);

            await chat.PostAsync(roomId, KnownIds.Human, "second - will fail", ct: ct);
            await secondDegraded.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);

            await chat.PostAsync(roomId, KnownIds.Human, "third - will be stopped", ct: ct);
            await thirdPromptStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);

            await gateway.StopTurnAsync(agentId, roomId, ct);
            JsonObject cancelNotification = await fixture.Agent.WaitForAsync("session/cancel", TimeSpan.FromSeconds(5));
            Assert.Equal("2.0", (string?)cancelNotification["jsonrpc"]);

            // The fourth message is only sent once the Stop's cancel notification is observed, so its
            // work item is enqueued strictly after PersonaRunner's stop high-water mark is set - see
            // PersonaRunner.RunReadLoopAsync's StopTurn branch. Waiting for its own Degraded signal is
            // what proves the third (stopped) Turn's ProcessWorkItemAsync, and its finally block, fully
            // unwound before the consumer moved on to this one.
            await chat.PostAsync(roomId, KnownIds.Human, "fourth - will fail again", ct: ct);
            await thirdDegraded.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);

            List<PersonaStatus> degradedStatuses;
            lock (statuses)
            {
                degradedStatuses = [.. statuses.Where(s => s.State == PersonaState.Degraded)];
            }

            Assert.Equal(3, degradedStatuses.Count);
            Assert.Contains("3 consecutive Turns have failed", degradedStatuses[2].Reason, StringComparison.Ordinal);

            lock (statuses)
            {
                Assert.DoesNotContain(statuses, s => s.State == PersonaState.Offline);
            }

            IReadOnlyList<ChatMessage> history = await store.ReadAllAsync(roomId, ct);
            Assert.DoesNotContain(history, m => string.Equals(m.SenderId, agentId, StringComparison.Ordinal));
        }
        finally
        {
            fixture.Runner.StatusChanged -= OnStatusChanged;
        }
    }

    /// <summary>
    /// T-27: D9 proved this at the session level; this proves it end to end through a real runner.
    /// Disposing the fixture disposes the real <c>DotAcpAgentSession</c>, which sends <c>session/close</c>
    /// as a courtesy to the peer and awaits its answer before returning — so by the time
    /// <see cref="MockAdapterFixture.DisposeAsync"/> completes, the peer has already observed it.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_RealSession_PeerObservesSessionClose()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Persona persona = new("nova", "You are Nova.");

        await using MockAdapterFixture fixture = await MockAdapterFixture.StartAsync(persona, cancellationToken: ct);
        await fixture.Agent.WaitForAsync("session/new", TimeSpan.FromSeconds(5));

        await fixture.DisposeAsync();

        Assert.Contains(
            fixture.Agent.Received,
            message => string.Equals((string?)message["method"], "session/close", StringComparison.Ordinal));
    }

    /// <summary>
    /// T-28: rules.md - "Editing a Persona or changing its Model, its Effort or its Adapter restarts
    /// its session ... none can be swapped into a running session, so 'the edit takes effect' can only
    /// mean stop-and-restart." <see cref="FakeAgentProcessLauncher"/> is built for exactly one
    /// <c>Launch()</c> call over one fixed duplex pair (flagged as unresolved by
    /// <see cref="MockAdapterFixture"/>'s own author), so a restart onto a second Adapter cannot be
    /// driven through one fixture. This drives it through two independent fixtures instead - two
    /// independent hosts, DI containers and <see cref="FakeAgentProcessLauncher"/> instances, so each
    /// only ever serves one <c>Launch()</c> - and proves the thing T-28 actually claims: a session
    /// started under the new Persona (the "new peer") carries the new Adapter's prompt and tool-naming
    /// convention, not the old one's. <c>PersonaSupervisor.NeedsRestart</c> - the trigger that decides
    /// *when* a restart happens - is exercised by <c>PersonaSupervisorLifecycleTests</c>; this test's job is the
    /// one only a real host/session/prompt-composer round trip can prove.
    /// </summary>
    [Fact]
    public async Task PersonaAdapterChange_NewPeerReceivesTheNewPromptAndToolConvention()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        Persona before = new("nova", "You are Nova, version one.", Adapter: "agency-v1");
        Dictionary<string, string?> beforeConfig = new(StringComparer.Ordinal)
        {
            ["Team:Acp:Adapters:0:Id"] = "agency-v1",
            ["Team:Acp:Adapters:0:Command"] = "node",
            ["Team:Acp:Adapters:0:Args:0"] = "--mock-adapter-fixture",
            ["Team:Acp:Adapters:0:UsesToolNamePrefix"] = "false",
        };

        string appendedBefore;
        await using (MockAdapterFixture beforeFixture = await MockAdapterFixture.StartAsync(before, beforeConfig, cancellationToken: ct))
        {
            appendedBefore = await GetAppendedSystemPromptAsync(beforeFixture, ct);
        }

        Assert.Contains("You are Nova, version one.", appendedBefore, StringComparison.Ordinal);
        Assert.DoesNotContain("mcp__", appendedBefore, StringComparison.Ordinal);

        Persona after = before with { Text = "You are Nova, version two.", Adapter = "claude-v2" };
        Dictionary<string, string?> afterConfig = new(StringComparer.Ordinal)
        {
            ["Team:Acp:Adapters:0:Id"] = "claude-v2",
            ["Team:Acp:Adapters:0:Command"] = "node",
            ["Team:Acp:Adapters:0:Args:0"] = "--mock-adapter-fixture",
            ["Team:Acp:Adapters:0:UsesToolNamePrefix"] = "true",
        };

        string appendedAfter;
        await using (MockAdapterFixture afterFixture = await MockAdapterFixture.StartAsync(after, afterConfig, cancellationToken: ct))
        {
            appendedAfter = await GetAppendedSystemPromptAsync(afterFixture, ct);
        }

        Assert.Contains("You are Nova, version two.", appendedAfter, StringComparison.Ordinal);
        Assert.Contains("mcp__team__get_help", appendedAfter, StringComparison.Ordinal);
        Assert.DoesNotContain("You are Nova, version one.", appendedAfter, StringComparison.Ordinal);
    }

    /// <summary>An <see cref="FakeAcpAgent.OnPrompt"/> script that blocks until a <c>session/cancel</c> notification signals it, for driving a Stop while a Turn is genuinely in flight.</summary>
    /// <param name="context">The in-flight prompt, used to await the peer's own cancel signal.</param>
    /// <param name="started">Resolved the moment this script starts running, so a test knows the Turn is actually in flight before it stops it.</param>
    /// <returns>The ACP stop reason once cancelled.</returns>
    private static async Task<string> BlockUntilCancelledAsync(PromptContext context, TaskCompletionSource started)
    {
        started.TrySetResult();
        await context.WaitForCancelAsync().ConfigureAwait(false);
        return "cancelled";
    }

    /// <summary>
    /// Resolves the Persona's own Agent id and its direct Room with the Human. Safe to call the moment
    /// <see cref="MockAdapterFixture.StartAsync"/> returns, with no polling: <c>AgentConnection.RunAsync</c>
    /// awaits <c>ChatService.EnsureRoomForAsync</c> before it ever writes the <c>Welcome</c> envelope, and
    /// <see cref="PersonaRunner.StartAsync"/> awaits reading that <c>Welcome</c> before returning - so the
    /// direct Room already exists by the time this fixture's own <c>StartAsync</c> completes.
    /// </summary>
    private static async Task<(string AgentId, string RoomId)> GetDirectRoomAsync(
        MockAdapterFixture fixture, string personaName, CancellationToken ct)
    {
        ITeamDirectory directory = fixture.Services.GetRequiredService<ITeamDirectory>();
        User? user = await directory.FindUserByNameAsync(personaName, ct);
        Assert.NotNull(user);

        Room? room = await directory.FindRoomWithExactMembersAsync(KnownIds.Human, user.Id, ct);
        Assert.NotNull(room);

        return (user.Id, room.Id);
    }

    /// <summary>Waits for <c>session/new</c> and pulls the appended system prompt text out of its <c>_meta</c> payload — the same shape <see cref="PromptDeliveryTests"/> and <see cref="ToolPrefixTests"/> already pin.</summary>
    private static async Task<string> GetAppendedSystemPromptAsync(MockAdapterFixture fixture, CancellationToken ct)
    {
        JsonObject sessionNewRequest = await fixture.Agent.WaitForAsync("session/new", TimeSpan.FromSeconds(5));
        ct.ThrowIfCancellationRequested();

        JsonObject? parameters = sessionNewRequest["params"] as JsonObject;
        JsonObject? meta = parameters?["_meta"] as JsonObject;
        JsonObject? systemPrompt = meta?["systemPrompt"] as JsonObject;
        string? appendedPrompt = (string?)systemPrompt?["append"];

        Assert.NotNull(appendedPrompt);
        return appendedPrompt;
    }
}
