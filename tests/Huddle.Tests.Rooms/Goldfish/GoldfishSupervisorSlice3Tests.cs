using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Goldfish;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.Pipes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Agency.Huddle.Tests.Goldfish;

/// <summary>
/// Pins slice 3 step 1 (corrections-slice3 §1 and §2): <c>IPossessionLookup</c>, <c>GoldfishGuard</c>, the supervisor's
/// <c>BeginAsync</c>, <c>OpenInRoom</c>, <c>OnReportAsync</c> and <c>EndAsync(result)</c>, the start-row texts, and the gateway hooks
/// (runner lost, stale connection, <c>GoldfishReport</c> over the pipe) with their DI wiring.
/// </summary>
public sealed class GoldfishSupervisorSlice3Tests
{
    private const string Brief = "  Check @Kai's math\n  twice  ";

    /// <summary>A Starting possession is not yet the Agent's own: the lookup answers null while the warm calling Turn is still unstamped.</summary>
    [Fact]
    public async Task PossessionOf_Starting_IsNull()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(Rig.Make("p1", rig.Nova.Id, rig.Room.Id, rig.Now) with { State = PossessionState.Starting });

        Assert.Null(rig.Supervisor.PossessionOf(rig.Nova.Id, rig.Room.Id));
        Assert.False(((IPossessionLookup)rig.Supervisor).IsPossessed(rig.Nova.Id, rig.Room.Id));
    }

    /// <summary>Working, Waiting and Returning rows answer with their id, and the default <c>IsPossessed</c> follows.</summary>
    [Theory]
    [InlineData("Working")]
    [InlineData("Waiting")]
    [InlineData("Returning")]
    public async Task PossessionOf_WorkingWaitingReturning_ReturnsTheRowId(string stateName)
    {
        PossessionState state = Enum.Parse<PossessionState>(stateName);
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        Possession row = Rig.Make("p1", rig.Nova.Id, rig.Room.Id, rig.Now) with { State = state };
        rig.Insert(state == PossessionState.Returning ? row with { Exit = GoldfishExit.CalledBack } : row);

        Assert.Equal("p1", rig.Supervisor.PossessionOf(rig.Nova.Id, rig.Room.Id));
        Assert.True(((IPossessionLookup)rig.Supervisor).IsPossessed(rig.Nova.Id, rig.Room.Id));
    }

    /// <summary>An ended row, another Room and another Agent are not possessed.</summary>
    [Fact]
    public async Task PossessionOf_EndedOtherRoomOtherAgent_IsNull()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(Rig.Make("p1", rig.Nova.Id, rig.Room.Id, rig.Now));
        rig.Insert(Rig.Ended("p2", rig.Kai.Id, rig.Now) with { RoomId = rig.Room.Id });

        Assert.Null(rig.Supervisor.PossessionOf(rig.Nova.Id, "other-room"));
        Assert.Null(rig.Supervisor.PossessionOf(rig.Kai.Id, rig.Room.Id));
        Assert.Null(rig.Supervisor.PossessionOf(rig.Outsider.Id, rig.Room.Id));
    }

    /// <summary>The guard resolves the Agent's one busy Room, and answers the open row of that Room only.</summary>
    [Fact]
    public async Task Guard_SoleRoomPossessed_AnswersTheRowAndRefuses()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(Rig.Make("p1", rig.Nova.Id, rig.Room.Id, rig.Now));
        rig.Activity.Begin(rig.Nova.Id, rig.Room.Id);

        Assert.Equal("p1", rig.Guard.PossessionOf(rig.Nova.Id));
        Assert.Equal("[tool.goldfish.refused]", rig.Guard.Refusal(rig.Nova.Id));
    }

    /// <summary>With no Turn, in a Room that is not the possession's, or for another Agent, the guard answers null and does not refuse.</summary>
    [Fact]
    public async Task Guard_NoTurnOtherRoomOtherAgent_AnswersNull()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(Rig.Make("p1", rig.Nova.Id, rig.Room.Id, rig.Now));

        Assert.Null(rig.Guard.PossessionOf(rig.Nova.Id));
        rig.Activity.Begin(rig.Nova.Id, "other-room");
        Assert.Null(rig.Guard.PossessionOf(rig.Nova.Id));
        Assert.Null(rig.Guard.Refusal(rig.Nova.Id));
        rig.Activity.Begin(rig.Kai.Id, rig.Room.Id);
        Assert.Null(rig.Guard.Refusal(rig.Kai.Id));
    }

    /// <summary>BeginAsync writes a Starting row with the Agent, Room, Task and the brief word for word.</summary>
    [Fact]
    public async Task BeginAsync_WritesAStartingRow()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);

        Possession? begun = await rig.Supervisor.BeginAsync(rig.Nova.Id, rig.Room.Id, "t1", Brief, ct);

        Assert.NotNull(begun);
        Possession row = rig.Row(begun.Id);
        Assert.Equal(
            (rig.Nova.Id, rig.Room.Id, "t1", Brief, PossessionState.Starting, rig.Now),
            (row.AgentId, row.RoomId, row.TaskId, row.Brief, row.State, row.StartedAt));
        Assert.Equal(row, begun);
    }

    /// <summary>The start row is Huddle's quiet Message, the brief byte for byte, and the brief's own Mention owes nobody a Turn.</summary>
    [Fact]
    public async Task BeginAsync_PostsTheQuietStartRowWithTheBriefVerbatim()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Queue.Subscribe(rig.Kai.Id);

        _ = await rig.Supervisor.BeginAsync(rig.Nova.Id, rig.Room.Id, null, Brief, ct);

        ChatMessage line = Assert.Single(await rig.HuddleLinesAsync(ct));
        Assert.Equal(GoldfishLine.StartRow("Nova", Brief), line.Text);
        Assert.Equal("Nova went into goldfish mode. They don't remember this Room.\n\n  Check @Kai's math\n  twice  ", line.Text);
        Assert.Empty(TurnQueueRows.Read(rig.DataDir));
    }

    /// <summary>The runner is sent GoldfishBegin with the possession id, Room, brief and the Task id.</summary>
    [Fact]
    public async Task BeginAsync_SendsGoldfishBeginWithTheTaskId()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);

        Possession? begun = await rig.Supervisor.BeginAsync(rig.Nova.Id, rig.Room.Id, "t1", Brief, ct);

        Assert.NotNull(begun);
        (string agentId, GoldfishBegin begin) = Assert.Single(rig.Link.Begins);
        Assert.Equal(rig.Nova.Id, agentId);
        Assert.Equal((begun.Id, rig.Room.Id, Brief, "t1"), (begin.PossessionId, begin.RoomId, begin.Brief, begin.TaskId));
    }

    /// <summary>A second BeginAsync for an Agent that has an open possession returns null and posts and sends nothing more.</summary>
    [Fact]
    public async Task BeginAsync_SecondCall_ReturnsNullAndDoesNothingMore()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        _ = await rig.Supervisor.BeginAsync(rig.Nova.Id, rig.Room.Id, null, Brief, ct);

        Possession? second = await rig.Supervisor.BeginAsync(rig.Nova.Id, rig.Room.Id, null, "again", ct);

        Assert.Null(second);
        Assert.Single(rig.Link.Begins);
        Assert.Single(await rig.HuddleLinesAsync(ct));
    }

    /// <summary>A start-row post that fails ends the possession Failed, tells the runner it ended, never sends Begin, and rethrows.</summary>
    [Fact]
    public async Task BeginAsync_PostFails_EndsFailedAndRethrows()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Transcript.FailAppend = true;

        _ = await Assert.ThrowsAsync<IOException>(() => rig.Supervisor.BeginAsync(rig.Nova.Id, rig.Room.Id, null, Brief, ct));

        (string agentId, string possessionId, GoldfishExit exit) = Assert.Single(rig.Link.Ends);
        Assert.Equal((rig.Nova.Id, GoldfishExit.Failed), (agentId, exit));
        Assert.Equal((PossessionState.Ended, GoldfishExit.Failed), (rig.Row(possessionId).State, rig.Row(possessionId).Exit));
        Assert.Empty(rig.Link.Begins);
        Assert.Null(rig.Supervisor.FindOpen(rig.Nova.Id));
    }

    /// <summary>A GoldfishBegin that cannot be sent ends the possession Failed, with the return row posted, and rethrows.</summary>
    [Fact]
    public async Task BeginAsync_SendFails_EndsFailedAndRethrows()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Link.BeginThrows = true;

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Supervisor.BeginAsync(rig.Nova.Id, rig.Room.Id, null, Brief, ct));

        (_, string possessionId, GoldfishExit exit) = Assert.Single(rig.Link.Ends);
        Assert.Equal(GoldfishExit.Failed, exit);
        Assert.Equal(PossessionState.Ended, rig.Row(possessionId).State);
        IReadOnlyList<ChatMessage> lines = await rig.HuddleLinesAsync(ct);
        Assert.Equal("@Nova is back. Didn't finish: the Adapter stopped.", lines[^1].Text);
    }

    /// <summary>OpenInRoom answers the open possession of that Room, and null for another Room or once it has ended.</summary>
    [Fact]
    public async Task OpenInRoom_AnswersTheOpenRowOfThatRoomOnly()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(Rig.Make("p1", rig.Nova.Id, rig.Room.Id, rig.Now));
        rig.Insert(Rig.Ended("p2", rig.Kai.Id, rig.Now) with { RoomId = "other-room" });

        Assert.Equal("p1", rig.Supervisor.OpenInRoom(rig.Room.Id)?.Id);
        Assert.Null(rig.Supervisor.OpenInRoom("other-room"));
        Assert.Null(rig.Supervisor.OpenInRoom("no-room"));
    }

    /// <summary>TurnStarted moves a Starting or Waiting possession to Working.</summary>
    [Theory]
    [InlineData("Starting")]
    [InlineData("Waiting")]
    public async Task OnReportAsync_TurnStarted_MovesToWorking(string fromName)
    {
        PossessionState from = Enum.Parse<PossessionState>(fromName);
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(Rig.Make("p1", rig.Nova.Id, rig.Room.Id, rig.Now) with { State = from });

        await rig.Supervisor.OnReportAsync(rig.Nova.Id, new GoldfishReport("p1", GoldfishReportKind.TurnStarted), ct);

        Assert.Equal(PossessionState.Working, rig.Row("p1").State);
    }

    /// <summary>TurnEnded with a running total of 1.0 then 1.5 adds the difference: spend 1.5 over two Turns, Waiting after each.</summary>
    [Fact]
    public async Task OnReportAsync_TurnEnded_ChargesTheDeltaAndWaits()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(Rig.Make("p1", rig.Nova.Id, rig.Room.Id, rig.Now) with { State = PossessionState.Starting });

        await rig.Supervisor.OnReportAsync(rig.Nova.Id, new GoldfishReport("p1", GoldfishReportKind.TurnStarted), ct);
        await rig.Supervisor.OnReportAsync(rig.Nova.Id, new GoldfishReport("p1", GoldfishReportKind.TurnEnded, 1.0m, "USD", Done: false), ct);
        Possession first = rig.Row("p1");
        await rig.Supervisor.OnReportAsync(rig.Nova.Id, new GoldfishReport("p1", GoldfishReportKind.TurnStarted), ct);
        await rig.Supervisor.OnReportAsync(rig.Nova.Id, new GoldfishReport("p1", GoldfishReportKind.TurnEnded, 1.5m, "USD", Done: false), ct);

        Assert.Equal((PossessionState.Waiting, 1, 1.0m, "USD"), (first.State, first.Turns, first.Spend, first.Currency));
        Possession second = rig.Row("p1");
        Assert.Equal((PossessionState.Waiting, 2, 1.5m, "USD"), (second.State, second.Turns, second.Spend, second.Currency));
    }

    /// <summary>A Failed report ends the possession Failed with the Adapter-stopped return line.</summary>
    [Fact]
    public async Task OnReportAsync_Failed_EndsFailed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(Rig.Make("p1", rig.Nova.Id, rig.Room.Id, rig.Now) with { State = PossessionState.Starting });

        await rig.Supervisor.OnReportAsync(rig.Nova.Id, new GoldfishReport("p1", GoldfishReportKind.Failed, Detail: "boom"), ct);

        Assert.Equal((PossessionState.Ended, GoldfishExit.Failed), (rig.Row("p1").State, rig.Row("p1").Exit));
        Assert.Equal([(rig.Nova.Id, "p1", GoldfishExit.Failed)], rig.Link.Ends);
    }

    /// <summary>Opened changes nothing, and a report for an id that is not the Agent's open row (unknown, or another Agent's) is ignored.</summary>
    [Fact]
    public async Task OnReportAsync_OpenedUnknownOrForeignId_ChangesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(Rig.Make("p1", rig.Nova.Id, rig.Room.Id, rig.Now) with { State = PossessionState.Starting });
        rig.Insert(Rig.Make("p2", rig.Kai.Id, rig.Room.Id, rig.Now) with { State = PossessionState.Waiting });

        await rig.Supervisor.OnReportAsync(rig.Nova.Id, new GoldfishReport("p1", GoldfishReportKind.Opened), ct);
        await rig.Supervisor.OnReportAsync(rig.Nova.Id, new GoldfishReport("nope", GoldfishReportKind.Failed), ct);
        await rig.Supervisor.OnReportAsync(rig.Nova.Id, new GoldfishReport("p2", GoldfishReportKind.TurnStarted), ct);
        await rig.Supervisor.OnReportAsync(rig.Nova.Id, new GoldfishReport("p2", GoldfishReportKind.TurnEnded, 9m, "USD", Done: false), ct);

        Assert.Equal(PossessionState.Starting, rig.Row("p1").State);
        Possession other = rig.Row("p2");
        Assert.Equal((PossessionState.Waiting, 0, 0m), (other.State, other.Turns, other.Spend));
        Assert.Empty(rig.Link.Ends);
    }

    /// <summary>The return line counts the list items of the posted result, not the length of the findings array.</summary>
    [Fact]
    public async Task EndAsync_FinishedWithListInTheResultText_CountsThePostedItemsNotTheFindingsArray()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(Rig.Make("p1", rig.Nova.Id, rig.Room.Id, rig.Now));
        GoldfishResult result = new("Checked:\n1. a\n2. b\n   - c\n3. d", []);

        _ = await rig.Supervisor.EndAsync("p1", GoldfishExit.Finished, null, ct, result);

        IReadOnlyList<ChatMessage> lines = await rig.HuddleLinesAsync(ct);
        Assert.Equal(GoldfishLine.ReturnLine("Nova", GoldfishExit.Finished, null, TimeSpan.Zero, true, findings: 4), lines[1].Text);
    }

    /// <summary>A finished possession whose result is prose and has no findings posts a return line without a number.</summary>
    [Fact]
    public async Task EndAsync_FinishedWithProse_ReturnLineHasNoCount()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(Rig.Make("p1", rig.Nova.Id, rig.Room.Id, rig.Now));

        _ = await rig.Supervisor.EndAsync("p1", GoldfishExit.Finished, null, ct, new GoldfishResult("All fine.", []));

        IReadOnlyList<ChatMessage> lines = await rig.HuddleLinesAsync(ct);
        Assert.Equal("@Nova is back. Goldfish finished: result above.", lines[1].Text);
    }

    /// <summary>A finished possession with a result posts the brief, the result text and each finding, in that order, as Huddle.</summary>
    [Fact]
    public async Task EndAsync_FinishedWithResult_PostsTheResultAndFindings()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(Rig.Make("p1", rig.Nova.Id, rig.Room.Id, rig.Now));
        GoldfishResult result = new("All checked", ["first finding", "second finding"]);

        bool ended = await rig.Supervisor.EndAsync("p1", GoldfishExit.Finished, null, ct, result);

        Assert.True(ended);
        IReadOnlyList<ChatMessage> lines = await rig.HuddleLinesAsync(ct);
        Assert.Equal(GoldfishLine.ResultText("Look into it", "All checked", "- first finding\n- second finding", false), lines[0].Text);
        Assert.Equal(GoldfishLine.ReturnLine("Nova", GoldfishExit.Finished, null, TimeSpan.Zero, true, findings: 2), lines[1].Text);
        Assert.Equal(lines[0].Id, rig.Row("p1").ResultMessageId);
    }

    /// <summary>A stuck possession's result is posted as a partial result, and its return line carries the first line.</summary>
    [Fact]
    public async Task EndAsync_StuckWithResult_PostsAPartialResult()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(Rig.Make("p1", rig.Nova.Id, rig.Room.Id, rig.Now));

        _ = await rig.Supervisor.EndAsync("p1", GoldfishExit.Stuck, null, ct, new GoldfishResult("Could not open the file", []));

        IReadOnlyList<ChatMessage> lines = await rig.HuddleLinesAsync(ct);
        Assert.Equal(GoldfishLine.ResultText("Look into it", "Could not open the file", null, true), lines[0].Text);
        Assert.Equal(GoldfishLine.ReturnLine("Nova", GoldfishExit.Stuck, null, TimeSpan.Zero, true, firstLine: "Could not open the file"), lines[1].Text);
    }

    /// <summary>A comeback with no <c>goldfish_done</c> posts the goldfish's last stamped reply as the partial result, not "No result was posted".</summary>
    [Fact]
    public async Task EndAsync_CalledBackAfterAStampedReply_PostsThatReplyAsThePartialResult()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(Rig.Make("p1", rig.Nova.Id, rig.Room.Id, rig.Now));
        await rig.File.AppendAsync(rig.Room.Id, new ChatMessage("m-1", rig.Now, rig.Nova.Id, "Nova", "first thought") { Goldfish = "p1" }, ct);
        await rig.File.AppendAsync(rig.Room.Id, new ChatMessage("m-2", rig.Now, rig.Kai.Id, "Kai", "not the goldfish") { Goldfish = "p1" }, ct);
        await rig.File.AppendAsync(rig.Room.Id, new ChatMessage("m-3", rig.Now, rig.Nova.Id, "Nova", "the last reply") { Goldfish = "p1" }, ct);
        await rig.File.AppendAsync(rig.Room.Id, new ChatMessage("m-4", rig.Now, rig.Nova.Id, "Nova", "warm, unstamped") { Goldfish = null }, ct);

        _ = await rig.Supervisor.EndAsync("p1", GoldfishExit.CalledBack, "You", ct);

        IReadOnlyList<ChatMessage> lines = await rig.HuddleLinesAsync(ct);
        Assert.Equal(GoldfishLine.ResultText("Look into it", "the last reply", null, true), lines[0].Text);
    }

    /// <summary>A possession that ends without any stamped reply still says that no result was posted.</summary>
    [Fact]
    public async Task EndAsync_CalledBackWithNoStampedReply_SaysNoResultWasPosted()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(Rig.Make("p1", rig.Nova.Id, rig.Room.Id, rig.Now));

        _ = await rig.Supervisor.EndAsync("p1", GoldfishExit.CalledBack, "You", ct);

        IReadOnlyList<ChatMessage> lines = await rig.HuddleLinesAsync(ct);
        Assert.Equal("Brief: Look into it\n\nNo result was posted.", lines[0].Text);
    }

    /// <summary>A possession finished by <c>goldfish_done</c> in its first Turn counts that Turn, and the late Turn report adds its spend.</summary>
    [Fact]
    public async Task EndAsync_FinishedInTheFirstTurn_CountsTheTurnAndTheLateReportAddsSpend()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(Rig.Make("p1", rig.Nova.Id, rig.Room.Id, rig.Now));
        await rig.Supervisor.OnReportAsync(rig.Nova.Id, new GoldfishReport("p1", GoldfishReportKind.TurnStarted), ct);

        _ = await rig.Supervisor.EndAsync("p1", GoldfishExit.Finished, null, ct, new GoldfishResult("Done", []));
        Assert.Equal(1, rig.Row("p1").Turns);

        await rig.Supervisor.OnReportAsync(rig.Nova.Id, new GoldfishReport("p1", GoldfishReportKind.TurnEnded, 0.05m, "USD", Done: false), ct);

        Possession row = rig.Row("p1");
        Assert.Equal((1, 0.05m, "USD"), (row.Turns, row.Spend, row.Currency));
    }

    /// <summary>The start row names the Agent, then the brief word for word; the Task comment names the Agent, then the brief word for word.</summary>
    [Fact]
    public void StartRowAndTaskComment_NameTheAgentAndQuoteTheBriefVerbatim()
    {
        string row = GoldfishLine.StartRow("Nova", Brief);
        string comment = GoldfishLine.TaskComment("Nova", Brief);

        Assert.Equal("Nova went into goldfish mode. They don't remember this Room.\n\n  Check @Kai's math\n  twice  ", row);
        Assert.Equal("Goldfish brief for Nova:   Check @Kai's math\n  twice  ", comment);
    }

    /// <summary>A runner connection that closes while its Agent is possessed ends the possession Failed, through the gateway hook.</summary>
    [Fact]
    public async Task Gateway_UnregisterWhilePossessed_EndsFailed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using PipeHostFixture fixture = await PipeHostFixture.StartAsync(ct);
        GatewayRig rig = GatewayRig.Attach(fixture);
        await using JsonLineStream nova = await fixture.ConnectClientAsync(ct);
        Welcome welcome = await HelloAsync(nova, "Nova", ct);
        rig.Insert("p1", welcome.AgentId);

        await nova.DisposeAsync();
        await UntilAsync(() => rig.Store.Get("p1")?.State == PossessionState.Ended, ct);

        Assert.Equal([(welcome.AgentId, "p1", GoldfishExit.Failed)], rig.Link.Ends);
    }

    /// <summary>A reconnect while possessed ends the old possession (E-3); the stale connection's own unregister then does not touch the next one.</summary>
    [Fact]
    public async Task Gateway_ReconnectWhilePossessed_EndsFailedAndTheStaleUnregisterDoesNot()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using PipeHostFixture fixture = await PipeHostFixture.StartAsync(ct);
        GatewayRig rig = GatewayRig.Attach(fixture);
        await using JsonLineStream first = await fixture.ConnectClientAsync(ct);
        Welcome welcome = await HelloAsync(first, "Nova", ct);
        rig.Insert("p1", welcome.AgentId);

        await using JsonLineStream second = await fixture.ConnectClientAsync(ct);
        _ = await HelloAsync(second, "Nova", ct);
        await UntilAsync(() => rig.Store.Get("p1")?.State == PossessionState.Ended, ct);
        rig.Insert("p2", welcome.AgentId);
        Assert.Null(await first.ReadAsync(ct));

        Assert.Equal([(welcome.AgentId, "p1", GoldfishExit.Failed)], rig.Link.Ends);
        Assert.Equal(PossessionState.Working, rig.Store.Get("p2")?.State);
    }

    /// <summary>A GoldfishReport read from the Agent's pipe reaches the supervisor under the connection's Agent id.</summary>
    [Fact]
    public async Task Gateway_GoldfishReportOverThePipe_ReachesTheSupervisor()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using PipeHostFixture fixture = await PipeHostFixture.StartAsync(ct);
        GatewayRig rig = GatewayRig.Attach(fixture);
        await using JsonLineStream nova = await fixture.ConnectClientAsync(ct);
        Welcome welcome = await HelloAsync(nova, "Nova", ct);
        rig.Insert("p1", welcome.AgentId, PossessionState.Starting);

        await nova.WriteAsync(new GoldfishReport("p1", GoldfishReportKind.TurnStarted), ct);
        await UntilAsync(() => rig.Store.Get("p1")?.State == PossessionState.Working, ct);

        Assert.Equal(PossessionState.Working, rig.Store.Get("p1")?.State);
    }

    /// <summary>The composition root registers the supervisor as the lookup, hands it to the gateway, and offers the guard.</summary>
    [Fact]
    public async Task Services_RegisterTheLookupTheGuardAndTheGatewayLink()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using PipeHostFixture fixture = await PipeHostFixture.StartAsync(ct);

        GoldfishSupervisor supervisor = fixture.Services.GetRequiredService<GoldfishSupervisor>();

        Assert.Same(supervisor, fixture.Services.GetRequiredService<IPossessionLookup>());
        Assert.Same(supervisor, fixture.Services.GetRequiredService<AgentGateway>().Goldfish);
        Assert.Same(fixture.Services.GetRequiredService<GoldfishGuard>(), fixture.Services.GetRequiredService<GoldfishGuard>());
    }

    private static async Task<Welcome> HelloAsync(JsonLineStream client, string name, CancellationToken ct)
    {
        await client.WriteAsync(new Hello(name, null), ct);
        return Assert.IsType<Welcome>(await client.ReadAsync(ct));
    }

    private static async Task UntilAsync(Func<bool> condition, CancellationToken ct)
    {
        using CancellationTokenSource bound = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bound.CancelAfter(TimeSpan.FromSeconds(10));
        while (!condition())
        {
            await Task.Delay(TimeSpan.FromMilliseconds(20), bound.Token);
        }
    }

    private sealed class FailableChatStore(IChatStore inner) : IChatStore
    {
        public bool FailAppend { get; set; }

        public Task AppendAsync(string roomId, ChatMessage message, CancellationToken ct = default) =>
            this.FailAppend ? Task.FromException(new IOException("The Transcript could not be written.")) : inner.AppendAsync(roomId, message, ct);

        public Task<IReadOnlyList<ChatMessage>> ReadAllAsync(string roomId, CancellationToken ct = default) => inner.ReadAllAsync(roomId, ct);

        public Task DeleteAsync(string roomId, CancellationToken ct = default) => inner.DeleteAsync(roomId, ct);

        public Task<bool> HasMessagesAsync(string roomId, CancellationToken ct = default) => inner.HasMessagesAsync(roomId, ct);
    }

    private sealed class RecordingLink : IGoldfishRunnerLink
    {
        public List<(string AgentId, string PossessionId, GoldfishExit Exit)> Ends { get; } = [];

        public List<(string AgentId, GoldfishBegin Begin)> Begins { get; } = [];

        public bool BeginThrows { get; set; }

        public Task SendEndAsync(string agentId, string possessionId, GoldfishExit exit, CancellationToken ct)
        {
            this.Ends.Add((agentId, possessionId, exit));
            return Task.CompletedTask;
        }

        public Task SendBeginAsync(string agentId, GoldfishBegin begin, CancellationToken ct)
        {
            if (this.BeginThrows)
            {
                throw new InvalidOperationException("runner link down");
            }

            this.Begins.Add((agentId, begin));
            return Task.CompletedTask;
        }
    }

    private sealed class KeyEchoPrompts : IPromptSource
    {
        public string Render(string key, IReadOnlyDictionary<string, string> values) => $"[{key}]";

        public string Raw(string key) => $"[{key}]";

        public bool IsOverridden(string key) => false;
    }

    /// <summary>A supervisor of the fixture's own host, with a recording link, set on the host's gateway.</summary>
    private sealed class GatewayRig
    {
        private GatewayRig(GoldfishStore store, RecordingLink link, DateTimeOffset now)
        {
            this.Store = store;
            this.Link = link;
            this.Now = now;
        }

        public GoldfishStore Store { get; }

        public RecordingLink Link { get; }

        private DateTimeOffset Now { get; }

        public static GatewayRig Attach(PipeHostFixture fixture)
        {
            IServiceProvider services = fixture.Services;
            GoldfishStore store = services.GetRequiredService<GoldfishStore>();
            RecordingLink link = new();
            GoldfishSupervisor supervisor = new(
                store,
                services.GetRequiredService<ITeamDirectory>(),
                services.GetRequiredService<ChatService>(),
                link,
                services.GetRequiredService<IOptions<TeamOptions>>(),
                NullLogger<GoldfishSupervisor>.Instance);
            services.GetRequiredService<AgentGateway>().Goldfish = supervisor;
            return new GatewayRig(store, link, DateTimeOffset.UtcNow);
        }

        public void Insert(string id, string agentId, PossessionState state = PossessionState.Working) =>
            Assert.True(this.Store.TryInsert(Rig.Make(id, agentId, "r1", this.Now) with { State = state }));
    }

    private sealed class Rig : IDisposable
    {
        private readonly TempDataDir dir;

        private Rig(TempDataDir dir, SqliteTeamDirectory team, (User Nova, User Kai, User Outsider, Room Room) seeded)
        {
            this.dir = dir;
            this.Nova = seeded.Nova;
            this.Kai = seeded.Kai;
            this.Outsider = seeded.Outsider;
            this.Room = seeded.Room;
            this.Clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.Zero));
            this.Queue = new TurnQueueStore(dir.Options());
            this.Options = dir.Options(o =>
            {
                o.Goldfish.Enabled = true;
                o.Goldfish.KeepDays = 30;
            });
            FileChatStore file = dir.CreateChatStore();
            this.File = file;
            this.Transcript = new FailableChatStore(file);
            RoomEvents events = new(NullLogger<RoomEvents>.Instance);
            this.Chat = new ChatService(
                team,
                this.Transcript,
                events,
                new FakeMentionAliasSource(),
                this.Options,
                new App.Agents.ProposalStore(events),
                new App.Questions.QuestionStore(events),
                NullLogger<ChatService>.Instance,
                timeProvider: this.Clock)
            {
                OwedTurns = new OwedTurnPolicy(new RoomFollows()),
                Ledger = new TurnQueue(this.Queue, this.Options, NullLogger<TurnQueue>.Instance, new Agency.Huddle.Tests.Desktop.FakeLifetime(), this.Clock),
            };
            this.Store = new GoldfishStore(this.Options);
            this.Supervisor = new GoldfishSupervisor(this.Store, team, this.Chat, this.Link, this.Options, NullLogger<GoldfishSupervisor>.Instance, this.Clock);
            this.Activity = new TurnActivity();
            this.Guard = new GoldfishGuard(this.Supervisor, this.Activity, new KeyEchoPrompts());
        }

        public FileChatStore File { get; }

        public FailableChatStore Transcript { get; }

        public TurnQueueStore Queue { get; }

        public ChatService Chat { get; }

        public FakeTimeProvider Clock { get; }

        public IOptions<TeamOptions> Options { get; }

        public GoldfishStore Store { get; }

        public RecordingLink Link { get; } = new();

        public GoldfishSupervisor Supervisor { get; }

        public TurnActivity Activity { get; }

        public GoldfishGuard Guard { get; }

        public User Nova { get; }

        public User Kai { get; }

        public User Outsider { get; }

        public Room Room { get; }

        public string DataDir => this.dir.Path;

        public DateTimeOffset Now => this.Clock.GetUtcNow();

        public static async Task<Rig> CreateAsync(CancellationToken ct)
        {
            TempDataDir dir = new();
            SqliteTeamDirectory team = dir.CreateDirectory();
            await team.InitializeAsync("You", ct);
            User nova = await AddAgentAsync(team, "Nova", ct);
            User kai = await AddAgentAsync(team, "Kai", ct);
            User outsider = await AddAgentAsync(team, "Juno", ct);
            Room room = await team.CreateRoomAsync("Pricing", [KnownIds.Human, nova.Id, kai.Id], ct);
            return new Rig(dir, team, (nova, kai, outsider, room));
        }

        /// <summary>An open possession in the Working state, brief "Look into it".</summary>
        public static Possession Make(string id, string agentId, string roomId, DateTimeOffset startedAt) =>
            new(id, agentId, roomId, null, "Look into it", PossessionState.Working, null, null, startedAt, null, 0, 0m, null, null);

        /// <summary>An ended possession whose end is the given time.</summary>
        public static Possession Ended(string id, string agentId, DateTimeOffset endedAt) =>
            new(id, agentId, "r1", null, "Look into it", PossessionState.Ended, GoldfishExit.Finished, null, endedAt.AddHours(-1), endedAt, 1, 0m, null, null);

        public void Insert(Possession possession) => Assert.True(this.Store.TryInsert(possession));

        public Possession Row(string id) => this.Store.Get(id) ?? throw new InvalidOperationException($"No possession '{id}'.");

        public async Task<IReadOnlyList<ChatMessage>> HuddleLinesAsync(CancellationToken ct) =>
            [.. (await this.File.ReadAllAsync(this.Room.Id, ct)).Where(message => string.Equals(message.SenderId, KnownIds.Huddle, StringComparison.Ordinal))];

        public void Dispose() => this.dir.Dispose();

        private static async Task<User> AddAgentAsync(SqliteTeamDirectory team, string name, CancellationToken ct)
        {
            User? agent = await team.UpsertAgentUserAsync(name, null, ct);
            Assert.NotNull(agent);
            return agent;
        }
    }
}
