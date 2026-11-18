using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Goldfish;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Factories;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Agency.Huddle.Tests.Services;

/// <summary>Pins what <c>ChatService</c> does for goldfish mode (spec 6.7, 6.8; slice 3 step 3): the comeback, the stamp on posts, the explicit stamp on the result, and the Mention notification.</summary>
public sealed class ChatServiceGoldfishTests
{
    /// <summary>"@Nova comeback" from the Human ends the possession CalledBack by the sender's name, and the comeback Message itself owes nobody a Turn.</summary>
    [Fact]
    public async Task PostAsync_ComebackByName_EndsCalledBackAndOwesNoTurnForTheComeback()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Queue.Subscribe(rig.Nova.Id);
        rig.Insert(Rig.Make("p1", rig.Nova.Id, rig.Room.Id, rig.Now));

        ChatMessage comeback = await rig.Chat.PostAsync(rig.Room.Id, KnownIds.Human, "@Nova comeback", ct: ct);

        Assert.Equal([(rig.Nova.Id, "p1", GoldfishExit.CalledBack)], rig.Link.Ends);
        Possession row = rig.Row("p1");
        Assert.Equal((PossessionState.Ended, GoldfishExit.CalledBack, "You"), (row.State, row.Exit, row.ExitBy));
        IReadOnlyList<ChatMessage> lines = await rig.HuddleLinesAsync(rig.Room.Id, ct);
        TurnQueueRowSnapshot owed = Assert.Single(TurnQueueRows.Read(rig.DataDir));
        Assert.Equal((rig.Nova.Id, lines[1].Id), (owed.AgentId, owed.MessageId));
        Assert.NotEqual(comeback.Id, owed.MessageId);
    }

    /// <summary>An Alias of the Agent works as its Name does.</summary>
    [Fact]
    public async Task PostAsync_ComebackByAlias_EndsCalledBack()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct, aliases: [new MentionAlias("nv", "Nova")]);
        rig.Insert(Rig.Make("p1", rig.Nova.Id, rig.Room.Id, rig.Now));

        _ = await rig.Chat.PostAsync(rig.Room.Id, KnownIds.Human, "@nv comeback", ct: ct);

        Assert.Equal([(rig.Nova.Id, "p1", GoldfishExit.CalledBack)], rig.Link.Ends);
        Assert.Equal(GoldfishExit.CalledBack, rig.Row("p1").Exit);
    }

    /// <summary>Case and the run of white space around and inside the phrase do not matter.</summary>
    [Theory]
    [InlineData("@NOVA COMEBACK")]
    [InlineData("  @nova    comeback  ")]
    [InlineData("@Nova\tcomeBack\n")]
    public async Task PostAsync_ComebackAnyCaseAndSpacing_Matches(string text)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(Rig.Make("p1", rig.Nova.Id, rig.Room.Id, rig.Now));

        _ = await rig.Chat.PostAsync(rig.Room.Id, KnownIds.Human, text, ct: ct);

        Assert.Equal([(rig.Nova.Id, "p1", GoldfishExit.CalledBack)], rig.Link.Ends);
    }

    /// <summary>Any Agent Member may call another back, and the exit is by that Agent's name.</summary>
    [Fact]
    public async Task PostAsync_ComebackFromAnotherAgent_EndsCalledBackByThatAgent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(Rig.Make("p1", rig.Nova.Id, rig.Room.Id, rig.Now));

        _ = await rig.Chat.PostAsync(rig.Room.Id, rig.Kai.Id, "@Nova comeback", ct: ct);

        Assert.Equal(("Kai", GoldfishExit.CalledBack), (rig.Row("p1").ExitBy, rig.Row("p1").Exit));
    }

    /// <summary>A full stop, a longer sentence or a second Mention is an ordinary Message: the possession stays open and the Mention owes its Turn.</summary>
    [Theory]
    [InlineData("@Nova comeback.")]
    [InlineData("@Nova please comeback")]
    [InlineData("@Nova comeback now")]
    [InlineData("@Nova @Kai comeback")]
    public async Task PostAsync_NotExactlyTheComeback_IsAnOrdinaryMessage(string text)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Queue.Subscribe(rig.Nova.Id);
        rig.Insert(Rig.Make("p1", rig.Nova.Id, rig.Room.Id, rig.Now));

        ChatMessage posted = await rig.Chat.PostAsync(rig.Room.Id, KnownIds.Human, text, ct: ct);

        Assert.Equal(PossessionState.Working, rig.Row("p1").State);
        Assert.Empty(rig.Link.Ends);
        Assert.Empty(await rig.HuddleLinesAsync(rig.Room.Id, ct));
        Assert.Equal(posted.Id, Assert.Single(TurnQueueRows.Read(rig.DataDir)).MessageId);
    }

    /// <summary>The comeback names another Member who is not in goldfish mode: that Member gets the not-in-mode line and the possessed Agent is left alone.</summary>
    [Fact]
    public async Task PostAsync_ComebackNamesOtherMember_PossessedAgentStays()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(Rig.Make("p1", rig.Nova.Id, rig.Room.Id, rig.Now));

        _ = await rig.Chat.PostAsync(rig.Room.Id, KnownIds.Human, "@Kai comeback", ct: ct);

        Assert.Equal(PossessionState.Working, rig.Row("p1").State);
        Assert.Equal([GoldfishLine.NotInMode("Kai")], (await rig.HuddleLinesAsync(rig.Room.Id, ct)).Select(line => line.Text));
    }

    /// <summary>A comeback posted by the goldfish session itself (a stamped post) never matches, so an Agent in goldfish mode cannot call a peer back.</summary>
    [Fact]
    public async Task PostAsync_ComebackFromStampedGoldfishPost_DoesNotMatch()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(Rig.Make("p1", rig.Nova.Id, rig.Room.Id, rig.Now));
        rig.Insert(Rig.Make("p2", rig.Kai.Id, rig.Room.Id, rig.Now));

        ChatMessage posted = await rig.Chat.PostAsync(rig.Room.Id, rig.Nova.Id, "@Kai comeback", ct: ct);

        Assert.Equal("p1", posted.Goldfish);
        Assert.Equal(PossessionState.Working, rig.Row("p2").State);
        Assert.Empty(rig.Link.Ends);
        Assert.Empty(await rig.HuddleLinesAsync(rig.Room.Id, ct));
    }

    /// <summary>A comeback for an Agent not in goldfish mode is answered with the not-in-mode line, posted by Huddle, and that line owes nobody a Turn.</summary>
    [Fact]
    public async Task PostAsync_ComebackNotInMode_PostsNotInModeLineOwingNobody()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Queue.Subscribe(rig.Nova.Id);

        _ = await rig.Chat.PostAsync(rig.Room.Id, KnownIds.Human, "@Nova comeback", ct: ct);

        ChatMessage line = Assert.Single(await rig.HuddleLinesAsync(rig.Room.Id, ct));
        Assert.Equal(GoldfishLine.NotInMode("Nova"), line.Text);
        Assert.Empty(TurnQueueRows.Read(rig.DataDir));
    }

    /// <summary>E-8: a second comeback finds the possession already ended and gets the not-in-mode line; the possession returned once.</summary>
    [Fact]
    public async Task PostAsync_SecondComeback_GetsNotInMode()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(Rig.Make("p1", rig.Nova.Id, rig.Room.Id, rig.Now));

        _ = await rig.Chat.PostAsync(rig.Room.Id, KnownIds.Human, "@Nova comeback", ct: ct);
        _ = await rig.Chat.PostAsync(rig.Room.Id, KnownIds.Human, "@Nova comeback", ct: ct);

        IReadOnlyList<ChatMessage> lines = await rig.HuddleLinesAsync(rig.Room.Id, ct);
        Assert.Equal(3, lines.Count);
        Assert.Equal(GoldfishLine.NotInMode("Nova"), lines[2].Text);
        Assert.Single(rig.Link.Ends);
    }

    /// <summary>The possession may be in another Room: the comeback still ends it, and the return Messages land in the possession's Room, not where the comeback was said.</summary>
    [Fact]
    public async Task PostAsync_ComebackForPossessionInAnotherRoom_EndsItThere()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(Rig.Make("p1", rig.Nova.Id, rig.Other.Id, rig.Now));

        _ = await rig.Chat.PostAsync(rig.Room.Id, KnownIds.Human, "@Nova comeback", ct: ct);

        Assert.Equal(GoldfishExit.CalledBack, rig.Row("p1").Exit);
        Assert.Equal(2, (await rig.HuddleLinesAsync(rig.Other.Id, ct)).Count);
        Assert.Empty(await rig.HuddleLinesAsync(rig.Room.Id, ct));
    }

    /// <summary>Huddle's own post is never a comeback, even with the exact words.</summary>
    [Fact]
    public async Task PostAsHuddleAsync_ComebackWords_AreNotACallBack()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(Rig.Make("p1", rig.Nova.Id, rig.Room.Id, rig.Now));

        _ = await rig.Chat.PostAsHuddleAsync(rig.Room.Id, "@Nova comeback", ct);

        Assert.Equal(PossessionState.Working, rig.Row("p1").State);
        Assert.Empty(rig.Link.Ends);
    }

    /// <summary>Without a supervisor wired in, "@Nova comeback" is an ordinary Message that Mentions Nova.</summary>
    [Fact]
    public async Task PostAsync_NoGoldfishSupervisor_ComebackIsOrdinary()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct, wired: false);
        rig.Queue.Subscribe(rig.Nova.Id);

        ChatMessage posted = await rig.Chat.PostAsync(rig.Room.Id, KnownIds.Human, "@Nova comeback", ct: ct);

        Assert.Equal(posted.Id, Assert.Single(TurnQueueRows.Read(rig.DataDir)).MessageId);
        Assert.Null(posted.Goldfish);
    }

    /// <summary>An Agent's post into the Room it is possessed in carries the possession id, in the result and in the Transcript, for Working and Waiting alike.</summary>
    [Theory]
    [InlineData("Working")]
    [InlineData("Waiting")]
    public async Task PostAsync_AgentIntoItsPossessedRoom_IsStamped(string stateName)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(Rig.Make("p1", rig.Nova.Id, rig.Room.Id, rig.Now) with { State = Enum.Parse<PossessionState>(stateName) });

        ChatMessage posted = await rig.Chat.PostAsync(rig.Room.Id, rig.Nova.Id, "found it", ct: ct);

        Assert.Equal("p1", posted.Goldfish);
        Assert.Equal("p1", (await rig.File.ReadAllAsync(rig.Room.Id, ct)).Single(message => message.Id == posted.Id).Goldfish);
    }

    /// <summary>A Starting possession (the warm Turn is still running), another Room, another Agent and the Human are all unstamped.</summary>
    [Fact]
    public async Task PostAsync_StartingOtherRoomOtherSender_IsNotStamped()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(Rig.Make("p1", rig.Nova.Id, rig.Room.Id, rig.Now) with { State = PossessionState.Starting });

        ChatMessage starting = await rig.Chat.PostAsync(rig.Room.Id, rig.Nova.Id, "still warm", ct: ct);
        _ = rig.Store.SetState("p1", PossessionState.Working, PossessionState.Starting);
        ChatMessage elsewhere = await rig.Chat.PostAsync(rig.Other.Id, rig.Nova.Id, "in another room", ct: ct);
        ChatMessage peer = await rig.Chat.PostAsync(rig.Room.Id, rig.Kai.Id, "a peer", ct: ct);
        ChatMessage human = await rig.Chat.PostAsync(rig.Room.Id, KnownIds.Human, "the human", ct: ct);

        Assert.Equal<string?>([null, null, null, null], [starting.Goldfish, elsewhere.Goldfish, peer.Goldfish, human.Goldfish]);
    }

    /// <summary>The result Message and the return row after it are both stamped with the explicit possession id (slice 4, correction A1).</summary>
    [Fact]
    public async Task EndAsync_ResultMessageAndReturnRow_BothCarryTheStamp()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(Rig.Make("p1", rig.Nova.Id, rig.Room.Id, rig.Now));

        _ = await rig.Supervisor.EndAsync("p1", GoldfishExit.Finished, null, ct);

        IReadOnlyList<ChatMessage> lines = await rig.HuddleLinesAsync(rig.Room.Id, ct);
        Assert.Equal<string?>(["p1", "p1"], [lines[0].Goldfish, lines[1].Goldfish]);
    }

    /// <summary>A Mention of a Waiting Agent in its possession's Room puts the possession back to Working.</summary>
    [Fact]
    public async Task PostAsync_MentionOfWaitingAgent_MovesToWorking()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(Rig.Make("p1", rig.Nova.Id, rig.Room.Id, rig.Now) with { State = PossessionState.Waiting });

        _ = await rig.Chat.PostAsync(rig.Room.Id, KnownIds.Human, "@Nova any news?", ct: ct);

        Assert.Equal(PossessionState.Working, rig.Row("p1").State);
    }

    /// <summary>A supervisor that fails (its table is gone) is logged and never fails the post: the Message is in the Transcript, for a Mention and for a comeback.</summary>
    [Theory]
    [InlineData("@Nova any news?")]
    [InlineData("@Nova comeback")]
    public async Task PostAsync_SupervisorFails_PostStillSucceeds(string text)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.DropGoldfishTable();

        ChatMessage posted = await rig.Chat.PostAsync(rig.Room.Id, KnownIds.Human, text, ct: ct);

        Assert.Contains(await rig.File.ReadAllAsync(rig.Room.Id, ct), message => message.Id == posted.Id); // contains-ok: the Message id is unique
    }

    private sealed class RecordingLink : IGoldfishRunnerLink
    {
        public List<(string AgentId, string PossessionId, GoldfishExit Exit)> Ends { get; } = [];

        public Task SendBeginAsync(string agentId, GoldfishBegin begin, CancellationToken ct) => Task.CompletedTask;

        public Task SendEndAsync(string agentId, string possessionId, GoldfishExit exit, CancellationToken ct)
        {
            this.Ends.Add((agentId, possessionId, exit));
            return Task.CompletedTask;
        }
    }

    private sealed class Rig : IDisposable
    {
        private readonly TempDataDir dir;

        private Rig(TempDataDir dir, SqliteTeamDirectory team, (User Nova, User Kai, Room Room, Room Other) seeded, IReadOnlyList<MentionAlias> aliases, bool wired)
        {
            this.dir = dir;
            this.Nova = seeded.Nova;
            this.Kai = seeded.Kai;
            this.Room = seeded.Room;
            this.Other = seeded.Other;
            this.Clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.Zero));
            this.Queue = new TurnQueueStore(dir.Options());
            Microsoft.Extensions.Options.IOptions<TeamOptions> options = dir.Options(o =>
            {
                o.Goldfish.Enabled = true;
                o.Goldfish.KeepDays = 30;
            });
            this.File = dir.CreateChatStore();
            RoomEvents events = new(NullLogger<RoomEvents>.Instance);
            this.Chat = new ChatService(
                team,
                this.File,
                events,
                new FakeMentionAliasSource { Aliases = aliases },
                options,
                new App.Agents.ProposalStore(events),
                new App.Questions.QuestionStore(events),
                NullLogger<ChatService>.Instance,
                timeProvider: this.Clock)
            {
                OwedTurns = new OwedTurnPolicy(new RoomFollows()),
                Ledger = new TurnQueue(this.Queue, options, NullLogger<TurnQueue>.Instance, new Agency.Huddle.Tests.Desktop.FakeLifetime(), this.Clock),
            };
            this.Store = new GoldfishStore(options);
            this.Supervisor = new GoldfishSupervisor(this.Store, team, this.Chat, this.Link, options, NullLogger<GoldfishSupervisor>.Instance, this.Clock);
            if (wired)
            {
                this.Chat.Goldfish = this.Supervisor;
            }
        }

        public FileChatStore File { get; }

        public TurnQueueStore Queue { get; }

        public ChatService Chat { get; }

        public FakeTimeProvider Clock { get; }

        public GoldfishStore Store { get; }

        public RecordingLink Link { get; } = new();

        public GoldfishSupervisor Supervisor { get; }

        public User Nova { get; }

        public User Kai { get; }

        public Room Room { get; }

        public Room Other { get; }

        public string DataDir => this.dir.Path;

        public DateTimeOffset Now => this.Clock.GetUtcNow();

        public static async Task<Rig> CreateAsync(CancellationToken ct, IReadOnlyList<MentionAlias>? aliases = null, bool wired = true)
        {
            TempDataDir dir = new();
            SqliteTeamDirectory team = dir.CreateDirectory();
            await team.InitializeAsync("You", ct);
            User nova = await AddAgentAsync(team, "Nova", ct);
            User kai = await AddAgentAsync(team, "Kai", ct);
            Room room = await team.CreateRoomAsync("Pricing", [KnownIds.Human, nova.Id, kai.Id], ct);
            Room other = await team.CreateRoomAsync("Hiring", [KnownIds.Human, nova.Id, kai.Id], ct);
            return new Rig(dir, team, (nova, kai, room, other), aliases ?? [], wired);
        }

        /// <summary>An open possession in the Working state, brief "Look into it".</summary>
        public static Possession Make(string id, string agentId, string roomId, DateTimeOffset startedAt) =>
            new(id, agentId, roomId, null, "Look into it", PossessionState.Working, null, null, startedAt, null, 0, 0m, null, null);

        public void Insert(Possession possession) => Assert.True(this.Store.TryInsert(possession));

        public Possession Row(string id) => this.Store.Get(id) ?? throw new InvalidOperationException($"No possession '{id}'.");

        public async Task<IReadOnlyList<ChatMessage>> HuddleLinesAsync(string roomId, CancellationToken ct) =>
            [.. (await this.File.ReadAllAsync(roomId, ct)).Where(message => string.Equals(message.SenderId, KnownIds.Huddle, StringComparison.Ordinal))];

        /// <summary>Drops the possession table so every supervisor call throws.</summary>
        public void DropGoldfishTable()
        {
            SqliteConnection.ClearAllPools();
            using SqliteConnection connection = new($"Data Source={System.IO.Path.Combine(this.dir.Path, "team.db")}");
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "DROP TABLE goldfish_possessions";
            _ = command.ExecuteNonQuery();
        }

        public void Dispose() => this.dir.Dispose();

        private static async Task<User> AddAgentAsync(SqliteTeamDirectory team, string name, CancellationToken ct)
        {
            User? agent = await team.UpsertAgentUserAsync(name, null, ct);
            Assert.NotNull(agent);
            return agent;
        }
    }
}
