using Agency.Huddle.App;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Factories;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Agency.Huddle.Tests.Services;

/// <summary>The Turn Queue's start-up pass (spec §5.7): what each row becomes, the start-time cutoff, the tail read, the restart line and the age rule.</summary>
public sealed class TurnQueueRecoveryTests
{
    /// <summary>A row whose Room no longer exists is closed as gone and the pass says nothing about it.</summary>
    [Fact]
    public async Task Pass_RoomGone_ClosesRowAsGone()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        _ = rig.Queue.Record(rig.Nova.Id, "m1", "no-such-room", rig.Now.AddHours(-2), rig.Before);

        await rig.RunAsync(ct);

        TurnQueueRowSnapshot row = TurnQueueRows.Find(rig.DataDir, rig.Nova.Id, "no-such-room", "m1");
        Assert.Equal(("done", "gone"), (row.Status, row.Outcome));
        Assert.Empty(await rig.HuddleLinesAsync(ct));
    }

    /// <summary>An Agent that is no longer a Member of the Room has its row closed as gone.</summary>
    [Fact]
    public async Task Pass_AgentNoLongerMember_ClosesRowAsGone()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await rig.AppendAsync("m1", KnownIds.Human, "hello", ct);
        _ = rig.Queue.Record(rig.Outsider.Id, "m1", rig.Room.Id, rig.Now.AddHours(-2), rig.Before);

        await rig.RunAsync(ct);

        Assert.Equal(("done", "gone"), Outcome(rig, rig.Outsider, "m1"));
    }

    /// <summary>An Agent whose Persona is gone has its row closed as gone.</summary>
    [Fact]
    public async Task Pass_PersonaGone_ClosesRowAsGone()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await rig.AppendAsync("m1", KnownIds.Human, "hello", ct);
        rig.Waiting(rig.Kai, "m1", TimeSpan.FromHours(2));
        rig.Personaless.Add(rig.Kai.Name);

        await rig.RunAsync(ct);

        Assert.Equal(("done", "gone"), Outcome(rig, rig.Kai, "m1"));
        Assert.Empty(await rig.HuddleLinesAsync(ct));
    }

    /// <summary>A waiting row whose Message is not in the Transcript is deleted (the crash fell between §5.4's steps 3 and 4).</summary>
    [Fact]
    public async Task Pass_WaitingMessageAbsent_DeletesRow()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Waiting(rig.Nova, "never-appended", TimeSpan.FromHours(1));

        await rig.RunAsync(ct);

        Assert.Empty(TurnQueueRows.Read(rig.DataDir));
        Assert.Empty(await rig.HuddleLinesAsync(ct));
    }

    /// <summary>A young waiting row stays waiting and the line says it is picked up.</summary>
    [Fact]
    public async Task Pass_WaitingYoung_StaysWaiting()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await rig.AppendAsync("m1", KnownIds.Human, "hello", ct);
        rig.Waiting(rig.Nova, "m1", TimeSpan.FromHours(2));

        await rig.RunAsync(ct);

        Assert.Equal(("waiting", null), Outcome(rig, rig.Nova, "m1"));
    }

    /// <summary>A waiting row older than ResumeWithin is closed as expired.</summary>
    [Fact]
    public async Task Pass_WaitingOlderThanLimit_ClosesAsExpired()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await rig.AppendAsync("m1", KnownIds.Human, "hello", ct);
        rig.Waiting(rig.Nova, "m1", TimeSpan.FromHours(25));

        await rig.RunAsync(ct);

        Assert.Equal(("done", "expired"), Outcome(rig, rig.Nova, "m1"));
    }

    /// <summary>A running row whose minted reply is in the Transcript is closed as replied, so nothing runs again (U3).</summary>
    [Fact]
    public async Task Pass_RunningReplyPresent_ClosesAsRepliedAndPostsNoLine()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await rig.AppendAsync("m1", KnownIds.Human, "hello", ct);
        await rig.AppendAsync("r1", rig.Nova.Id, "answer", ct);
        rig.Running(rig.Nova, "m1", "r1", 1);

        await rig.RunAsync(ct);

        Assert.Equal(("done", "replied"), Outcome(rig, rig.Nova, "m1"));
        Assert.Empty(await rig.HuddleLinesAsync(ct));
    }

    /// <summary>A running row with no reply and one attempt is set back to waiting and loses its connection (cut off).</summary>
    [Fact]
    public async Task Pass_RunningNoReplyFirstAttempt_SetsBackToWaiting()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await rig.AppendAsync("m1", KnownIds.Human, "hello", ct);
        rig.Running(rig.Nova, "m1", "r1", 1);

        await rig.RunAsync(ct);

        TurnQueueRowSnapshot row = TurnQueueRows.Find(rig.DataDir, rig.Nova.Id, rig.Room.Id, "m1");
        Assert.Equal(("waiting", null, null), (row.Status, row.Outcome, row.ConnectionId));
    }

    /// <summary>A running row that was already cut off once is abandoned: a second cut-off never runs again.</summary>
    [Fact]
    public async Task Pass_RunningNoReplySecondAttempt_ClosesAsAbandoned()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await rig.AppendAsync("m1", KnownIds.Human, "hello", ct);
        rig.Running(rig.Nova, "m1", "r1", 2);

        await rig.RunAsync(ct);

        Assert.Equal(("done", "abandoned"), Outcome(rig, rig.Nova, "m1"));
    }

    /// <summary>A row updated at or after the pass's start time is never touched, and says nothing in the line (a post racing the pass, U4).</summary>
    [Fact]
    public async Task Pass_RowUpdatedAtStart_IsLeftAloneAndUnmentioned()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        _ = rig.Queue.Record(rig.Nova.Id, "late", rig.Room.Id, rig.Now.AddHours(-40), rig.Now);
        rig.Running(rig.Kai, "m2", "r2", 2, updated: rig.Now);

        await rig.RunAsync(ct);

        Assert.Equal(("waiting", null), Outcome(rig, rig.Nova, "late"));
        Assert.Equal(("running", null), Outcome(rig, rig.Kai, "m2"));
        Assert.Empty(await rig.HuddleLinesAsync(ct));
    }

    /// <summary>The tail read doubles from 50 and stops at the oldest open Message, never reading the whole file.</summary>
    [Fact]
    public async Task Pass_OldestMessageDeep_ReadsTailsOf50Then100AndStops()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        for (int i = 0; i < 30; i++)
        {
            await rig.AppendAsync("old" + i, KnownIds.Human, "old", ct);
        }

        await rig.AppendAsync("target", KnownIds.Human, "wanted", ct);
        for (int i = 0; i < 70; i++)
        {
            await rig.AppendAsync("new" + i, KnownIds.Human, "new", ct);
        }

        rig.Waiting(rig.Nova, "target", TimeSpan.FromHours(2));

        await rig.RunAsync(ct);

        Assert.Equal([50, 100], rig.Transcript.TailCounts);
        Assert.Equal(0, rig.Transcript.ReadAllCalls);
        Assert.Equal(("waiting", null), Outcome(rig, rig.Nova, "target"));
    }

    /// <summary>A Message that is not in a short Transcript is found missing after one read, because the read returned fewer than asked.</summary>
    [Fact]
    public async Task Pass_ShortTranscript_ReadsOnceWhenItReturnsFewerThanAsked()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await rig.AppendAsync("m1", KnownIds.Human, "hello", ct);
        rig.Waiting(rig.Nova, "missing", TimeSpan.FromHours(2));

        await rig.RunAsync(ct);

        Assert.Equal([50], rig.Transcript.TailCounts);
        Assert.Empty(TurnQueueRows.Read(rig.DataDir));
    }

    /// <summary>A Message near the end is found by the first read of 50.</summary>
    [Fact]
    public async Task Pass_MessageInFirstTail_ReadsOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        for (int i = 0; i < 80; i++)
        {
            await rig.AppendAsync("f" + i, KnownIds.Human, "filler", ct);
        }

        await rig.AppendAsync("m1", KnownIds.Human, "hello", ct);
        rig.Waiting(rig.Nova, "m1", TimeSpan.FromHours(2));

        await rig.RunAsync(ct);

        Assert.Equal([50], rig.Transcript.TailCounts);
    }

    /// <summary>Done rows older than KeepDays are pruned and newer ones stay.</summary>
    [Fact]
    public async Task Pass_PrunesDoneRowsOlderThanKeepDays()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        _ = rig.Queue.Record(rig.Nova.Id, "oldDone", rig.Room.Id, rig.Now.AddDays(-9), rig.Now.AddDays(-8));
        _ = rig.Queue.Finish(rig.Nova.Id, rig.Room.Id, ["oldDone"], TurnQueueOutcome.Silent, rig.Now.AddDays(-8));
        _ = rig.Queue.Record(rig.Nova.Id, "newDone", rig.Room.Id, rig.Now.AddDays(-7), rig.Now.AddDays(-6));
        _ = rig.Queue.Finish(rig.Nova.Id, rig.Room.Id, ["newDone"], TurnQueueOutcome.Silent, rig.Now.AddDays(-6));

        await rig.RunAsync(ct);

        TurnQueueRowSnapshot kept = Assert.Single(TurnQueueRows.Read(rig.DataDir));
        Assert.Equal("newDone", kept.MessageId);
    }

    /// <summary>One line per Room names every Agent and both outcomes, in exact words.</summary>
    [Fact]
    public async Task Pass_MixedRows_PostsOneLineNamingEveryAgentAndBothOutcomes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        foreach (string id in new[] { "a", "b", "c", "d", "e" })
        {
            await rig.AppendAsync(id, KnownIds.Human, "text " + id, ct);
        }

        rig.Waiting(rig.Nova, "a", TimeSpan.FromHours(3));
        rig.Waiting(rig.Nova, "b", TimeSpan.FromHours(2));
        rig.Waiting(rig.Kai, "c", TimeSpan.FromHours(1));
        rig.Waiting(rig.Nova, "d", TimeSpan.FromHours(30));
        rig.Running(rig.Nova, "e", "re", 2);

        await rig.RunAsync(ct);

        ChatMessage line = Assert.Single(await rig.HuddleLinesAsync(ct));
        Assert.Equal(
            "Picked up after a restart. 2 Messages waiting for Nova, 1 for Kai. "
            + "Not picked up: 1 Message for Nova that waited more than 24 hours; 1 that Huddle stopped twice while Nova was answering it.",
            line.Text);
    }

    /// <summary>When nothing is picked up the line opens with the not-picked-up words.</summary>
    [Fact]
    public async Task Pass_NothingPickedUp_PostsNotPickedUpLine()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await rig.AppendAsync("a", KnownIds.Human, "hello", ct);
        rig.Waiting(rig.Kai, "a", TimeSpan.FromHours(48));

        await rig.RunAsync(ct);

        ChatMessage line = Assert.Single(await rig.HuddleLinesAsync(ct));
        Assert.Equal("Not picked up after a restart. Not picked up: 1 Message for Kai that waited more than 24 hours.", line.Text);
    }

    /// <summary>With the queue off, nothing changes and no line is posted (E-13).</summary>
    [Fact]
    public async Task Pass_Disabled_ChangesNothingAndPostsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct, enabled: false);
        await rig.AppendAsync("m1", KnownIds.Human, "hello", ct);
        rig.Waiting(rig.Nova, "m1", TimeSpan.FromHours(30));
        rig.Waiting(rig.Nova, "gone", TimeSpan.FromHours(1));
        rig.Running(rig.Kai, "m1", "r1", 2);

        await rig.RunAsync(ct);

        Assert.Equal(3, TurnQueueRows.Read(rig.DataDir).Count(row => row.Status != "done"));
        Assert.Empty(await rig.HuddleLinesAsync(ct));
        Assert.Empty(rig.Transcript.TailCounts);
    }

    /// <summary>A failing pass is logged and never stops the host starting.</summary>
    [Fact]
    public async Task Pass_TranscriptThrows_StartAsyncStillCompletes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await rig.AppendAsync("m1", KnownIds.Human, "hello", ct);
        rig.Waiting(rig.Nova, "m1", TimeSpan.FromHours(2));
        rig.Transcript.FailReads = true;

        await rig.RunAsync(ct);

        Assert.Equal(("waiting", null), Outcome(rig, rig.Nova, "m1"));
    }

    /// <summary>The words the poster uses are the ones the parser recognises: the test fails if the two part.</summary>
    [Fact]
    public async Task RestartLine_PostedByThePass_IsRecognisedByTheParser()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await rig.AppendAsync("a", KnownIds.Human, "hello", ct);
        rig.Waiting(rig.Nova, "a", TimeSpan.FromHours(1));

        await rig.RunAsync(ct);

        ChatMessage line = Assert.Single(await rig.HuddleLinesAsync(ct));
        Assert.Equal(RestartLine.Prefix + " 1 Message waiting for Nova.", line.Text);
        Assert.True(MessageDisplay.TryParseRestartLine(line.Text, out bool pickedUp, out string detail));
        Assert.True(pickedUp);
        Assert.Equal("1 Message waiting for Nova.", detail);
    }

    /// <summary>The parser recognises both prefixes, reports which one, and rejects other text.</summary>
    [Fact]
    public void TryParseRestartLine_RecognisesBothPrefixesOnly()
    {
        Assert.True(MessageDisplay.TryParseRestartLine(RestartLine.NotPickedUpPrefix + " Not picked up: 1 Message.", out bool pickedUp, out string detail));
        Assert.False(pickedUp);
        Assert.Equal("Not picked up: 1 Message.", detail);
        Assert.False(MessageDisplay.TryParseRestartLine("Hello there. " + RestartLine.Prefix, out _, out _));
        Assert.False(MessageDisplay.TryParseRestartLine(string.Empty, out _, out _));
    }

    /// <summary>The pure builder writes the exact texts for singulars, plurals, minutes and several Agents.</summary>
    [Fact]
    public void Build_WritesTheExactTexts()
    {
        TimeSpan day = TimeSpan.FromHours(24);
        Assert.Equal(
            "Picked up after a restart. 1 Message waiting for Nova.",
            RestartLine.Build([new RestartLine.Entry("Nova", 1, 0, 0)], day));
        Assert.Equal(
            "Picked up after a restart. 3 Messages waiting for Nova, 2 for Kai, 1 for Juno.",
            RestartLine.Build([new RestartLine.Entry("Nova", 3, 0, 0), new RestartLine.Entry("Kai", 2, 0, 0), new RestartLine.Entry("Juno", 1, 0, 0)], day));
        Assert.Equal(
            "Not picked up after a restart. Not picked up: 2 Messages for Nova that waited more than 90 minutes.",
            RestartLine.Build([new RestartLine.Entry("Nova", 0, 2, 0)], TimeSpan.FromMinutes(90)));
        Assert.Equal(
            "Not picked up after a restart. Not picked up: 2 Messages that Huddle stopped twice while Nova was answering them.",
            RestartLine.Build([new RestartLine.Entry("Nova", 0, 0, 2)], day));
        Assert.Equal(
            "Picked up after a restart. 1 Message waiting for Kai. Not picked up: 1 Message for Nova that waited more than 1 hour; 3 for Kai that waited more than 1 hour; 1 that Huddle stopped twice while Kai was answering it.",
            RestartLine.Build([new RestartLine.Entry("Nova", 0, 1, 0), new RestartLine.Entry("Kai", 1, 3, 1)], TimeSpan.FromHours(1)));
        Assert.Null(RestartLine.Build([], day));
        Assert.Null(RestartLine.Build([new RestartLine.Entry("Nova", 0, 0, 0)], day));
    }

    /// <summary>TakeWaiting closes older rows as expired, returns younger ones oldest first, and marks a cut-off one.</summary>
    [Fact]
    public async Task TakeWaiting_ExpiresOldRowsAndReturnsTheRest()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Waiting(rig.Nova, "old", TimeSpan.FromHours(25));
        rig.Running(rig.Nova, "mid", "rm", 1);
        rig.Queue.Set(rig.Queue.Open(rig.Nova.Id).Single(row => row.MessageId == "mid").Seq, TurnQueueStatus.Waiting, null, rig.Before);
        rig.Waiting(rig.Nova, "new", TimeSpan.FromHours(1));
        rig.Waiting(rig.Kai, "other", TimeSpan.FromHours(30));
        rig.Running(rig.Nova, "busy", "r", 1);

        IReadOnlyList<WaitingTurn> taken = rig.Control.TakeWaiting(rig.Nova.Id, rig.Now);

        Assert.Equal(["mid", "new"], taken.Select(turn => turn.Row.MessageId));
        Assert.Equal([true, false], taken.Select(turn => turn.CutOff));
        Assert.Equal(("done", "expired"), Outcome(rig, rig.Nova, "old"));
        Assert.Equal(("waiting", null), Outcome(rig, rig.Kai, "other"));
        Assert.Equal(("running", null), Outcome(rig, rig.Nova, "busy"));
    }

    /// <summary>A ResumeWithin of minutes is honoured.</summary>
    [Fact]
    public async Task TakeWaiting_LimitInMinutes_ExpiresTenMinuteOldRow()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct, resumeWithin: TimeSpan.FromMinutes(5));
        rig.Waiting(rig.Nova, "old", TimeSpan.FromMinutes(10));
        rig.Waiting(rig.Nova, "new", TimeSpan.FromMinutes(2));

        IReadOnlyList<WaitingTurn> taken = rig.Control.TakeWaiting(rig.Nova.Id, rig.Now);

        Assert.Equal("new", Assert.Single(taken).Row.MessageId);
        Assert.Equal(("done", "expired"), Outcome(rig, rig.Nova, "old"));
    }

    /// <summary>A ResumeWithin of zero means no limit, and a switched-off queue hands out nothing.</summary>
    [Fact]
    public async Task TakeWaiting_ZeroLimitReturnsEverything_DisabledReturnsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig unlimited = await Rig.CreateAsync(ct, resumeWithin: TimeSpan.Zero);
        unlimited.Waiting(unlimited.Nova, "ancient", TimeSpan.FromDays(400));
        Assert.Single(unlimited.Control.TakeWaiting(unlimited.Nova.Id, unlimited.Now));

        using Rig off = await Rig.CreateAsync(ct, enabled: false);
        off.Waiting(off.Nova, "m", TimeSpan.FromHours(1));
        Assert.Empty(off.Control.TakeWaiting(off.Nova.Id, off.Now));
        Assert.Equal(("waiting", null), Outcome(off, off.Nova, "m"));
    }

    private static (string Status, string? Outcome) Outcome(Rig rig, User agent, string messageId)
    {
        TurnQueueRowSnapshot row = TurnQueueRows.Find(rig.DataDir, agent.Id, rig.Room.Id, messageId);
        return (row.Status, row.Outcome);
    }

    private sealed class RecordingStore(IChatStore inner) : IChatStore
    {
        public List<int> TailCounts { get; } = [];

        public int ReadAllCalls { get; private set; }

        public bool FailReads { get; set; }

        public Task AppendAsync(string roomId, ChatMessage message, CancellationToken ct = default) => inner.AppendAsync(roomId, message, ct);

        public Task<IReadOnlyList<ChatMessage>> ReadAllAsync(string roomId, CancellationToken ct = default)
        {
            this.ReadAllCalls++;
            return inner.ReadAllAsync(roomId, ct);
        }

        public Task DeleteAsync(string roomId, CancellationToken ct = default) => inner.DeleteAsync(roomId, ct);

        public Task<bool> HasMessagesAsync(string roomId, CancellationToken ct = default) => inner.HasMessagesAsync(roomId, ct);

        public Task<IReadOnlyList<ChatMessage>> ReadTailAsync(string roomId, int count, CancellationToken ct = default)
        {
            this.TailCounts.Add(count);
            if (this.FailReads)
            {
                throw new InvalidDataException("transcript down");
            }

            return inner.ReadTailAsync(roomId, count, ct);
        }
    }

    private sealed class Rig : IDisposable
    {
        private readonly TempDataDir dir;

        private Rig(TempDataDir dir, SqliteTeamDirectory team, (User Nova, User Kai, User Outsider, Room Room) seeded, bool enabled, TimeSpan resumeWithin)
        {
            this.dir = dir;
            this.Team = team;
            this.Nova = seeded.Nova;
            this.Kai = seeded.Kai;
            this.Outsider = seeded.Outsider;
            this.Room = seeded.Room;
            this.Clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 16, 9, 0, 0, TimeSpan.Zero));
            this.Queue = new TurnQueueStore(dir.Options());
            this.Options = dir.Options(o =>
            {
                o.TurnQueue.Enabled = enabled;
                o.TurnQueue.ResumeWithin = resumeWithin;
                o.TurnQueue.KeepDays = 7;
            });
            FileChatStore file = dir.CreateChatStore();
            this.File = file;
            this.Transcript = new RecordingStore(file);
            RoomEvents events = new(NullLogger<RoomEvents>.Instance);
            this.Chat = new ChatService(
                this.Team,
                file,
                events,
                new FakeMentionAliasSource(),
                this.Options,
                new App.Agents.ProposalStore(events),
                new App.Questions.QuestionStore(events),
                NullLogger<ChatService>.Instance,
                timeProvider: this.Clock);
            this.Control = new TurnQueue(this.Queue, this.Options, NullLogger<TurnQueue>.Instance, new Agency.Huddle.Tests.Desktop.FakeLifetime(), this.Clock);
        }

        public SqliteTeamDirectory Team { get; }

        public FileChatStore File { get; }

        public RecordingStore Transcript { get; }

        public TurnQueueStore Queue { get; }

        public TurnQueue Control { get; }

        public ChatService Chat { get; }

        public FakeTimeProvider Clock { get; }

        public Microsoft.Extensions.Options.IOptions<TeamOptions> Options { get; }

        /// <summary>Agent Names whose Persona is treated as gone.</summary>
        public HashSet<string> Personaless { get; } = new(StringComparer.Ordinal);

        public User Nova { get; }

        public User Kai { get; }

        public User Outsider { get; }

        public Room Room { get; }

        public string DataDir => this.dir.Path;

        public DateTimeOffset Now => this.Clock.GetUtcNow();

        /// <summary>The time rows are written at: an hour before the pass.</summary>
        public DateTimeOffset Before => this.Now.AddHours(-1);

        public static async Task<Rig> CreateAsync(CancellationToken ct, bool enabled = true, TimeSpan? resumeWithin = null)
        {
            TempDataDir dir = new();
            SqliteTeamDirectory team = dir.CreateDirectory();
            await team.InitializeAsync("You", ct);
            User nova = await AddAgentAsync(team, "Nova", ct);
            User kai = await AddAgentAsync(team, "Kai", ct);
            User outsider = await AddAgentAsync(team, "Juno", ct);
            Room room = await team.CreateRoomAsync("Pricing", [KnownIds.Human, nova.Id, kai.Id], ct);
            return new Rig(dir, team, (nova, kai, outsider, room), enabled, resumeWithin ?? TimeSpan.FromHours(24));
        }

        /// <summary>Writes a waiting row for a Message posted <paramref name="age"/> ago, updated an hour before the pass.</summary>
        public void Waiting(User agent, string messageId, TimeSpan age) =>
            _ = this.Queue.Record(agent.Id, messageId, this.Room.Id, this.Now - age, this.Before);

        /// <summary>Writes a row that was sent <paramref name="attempts"/> times.</summary>
        public void Running(User agent, string messageId, string replyId, int attempts, DateTimeOffset? updated = null)
        {
            DateTimeOffset at = updated ?? this.Before;
            _ = this.Queue.Record(agent.Id, messageId, this.Room.Id, this.Now.AddHours(-2), at);
            for (int i = 0; i < attempts; i++)
            {
                this.Queue.Started(agent.Id, this.Room.Id, [messageId], replyId, "conn-1", this.Now.AddHours(-2), at);
            }
        }

        public Task AppendAsync(string id, string senderId, string text, CancellationToken ct) =>
            this.File.AppendAsync(this.Room.Id, new ChatMessage(id, this.Now.AddHours(-2), senderId, senderId, text), ct);

        public async Task RunAsync(CancellationToken ct)
        {
            TurnQueueRecovery recovery = new(
                this.Queue,
                this.Team,
                this.Transcript,
                this.Chat,
                name => !this.Personaless.Contains(name),
                this.Options,
                NullLogger<TurnQueueRecovery>.Instance,
                this.Clock);
            await recovery.StartAsync(ct);
        }

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
