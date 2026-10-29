using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Agents;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Elicitation;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.Feed;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// Tests for <see cref="TeamRoomReconciler"/> (Team chats spec §6.4): the Team Room per Team, filling it
/// as Agents join, archiving Rooms of a vanished Team, and the guards around all three. Every test
/// calls <see cref="TeamRoomReconciler.ReconcileAsync"/> directly, so none waits on a timer.
/// </summary>
public sealed class TeamRoomReconcilerTests
{
    /// <summary>A Team with a connected member gets a Team Room named after it, holding the Human and the member (T1).</summary>
    [Fact]
    public async Task Reconcile_TeamWithMember_CreatesTeamRoomWithHumanAndMember()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(["Nova", "Ada"], ct);
        f.Catalog.Teams = [Team("Marketing", "Nova", "Ada")];

        await f.Reconciler.ReconcileAsync(ct);

        Room? room = await f.Directory.FindTeamRoomAsync("Marketing", ct);
        Assert.NotNull(room);
        Assert.Equal("Marketing", room.Name);
        Assert.Equal("Marketing", room.Team);
        IReadOnlyList<User> members = await f.Directory.GetRoomMembersAsync(room.Id, ct);
        Assert.Equal(3, members.Count);
        Assert.Contains(members, m => m.Id == KnownIds.Human);
        Assert.Contains(members, m => m.Name == "Nova");
        Assert.Contains(members, m => m.Name == "Ada");
    }

    /// <summary>Creating a Team Room posts one room-created item by Huddle naming the Human and the members; finding it again posts nothing (Feed spec §6.5.3).</summary>
    [Fact]
    public async Task Reconcile_CreatingTeamRoom_PostsRoomCreatedAsHuddleOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(["Nova", "Ada"], ct);
        RecordingFeedPublisher feed = new();
        TeamRoomReconciler reconciler = f.NewReconciler(feed);
        f.Catalog.Teams = [Team("Marketing", "Nova", "Ada")];

        await reconciler.ReconcileAsync(ct);
        await reconciler.ReconcileAsync(ct);

        FeedMessage post = Assert.Single(feed.Posts, message => message.SourceType == FeedSourceType.RoomCreated);
        Room room = await f.TeamRoomAsync("Marketing", ct);
        Assert.Equal(room.Id, post.TargetKey);
        Assert.Equal(FeedActor.Huddle, post.Actor);
        RoomCreatedPayload payload = Assert.IsType<RoomCreatedPayload>(post.Payload);
        Assert.Equal(["You", "Nova", "Ada"], payload.Members);
        Assert.Equal("Marketing", payload.Team);
    }

    /// <summary>A Team that exists only because a Persona label names it (no folder) still gets its Team Room (T2).</summary>
    [Fact]
    public async Task Reconcile_LabelBornTeamWithoutFolder_CreatesTeamRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(["Nova"], ct);
        f.Catalog.Teams = [new TeamSummary("Research", [], ["Nova"], false)];

        await f.Reconciler.ReconcileAsync(ct);

        Assert.NotNull(await f.Directory.FindTeamRoomAsync("Research", ct));
    }

    /// <summary>A new member of a Team that already has a Team Room is invited into it (T3).</summary>
    [Fact]
    public async Task Reconcile_NewMember_IsInvitedIntoExistingTeamRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(["Nova", "Kim"], ct);
        f.Catalog.Teams = [Team("Marketing", "Nova")];
        await f.Reconciler.ReconcileAsync(ct);
        f.Catalog.Teams = [Team("Marketing", "Nova", "Kim")];

        await f.Reconciler.ReconcileAsync(ct);

        Room room = await f.TeamRoomAsync("Marketing", ct);
        IReadOnlyList<User> members = await f.Directory.GetRoomMembersAsync(room.Id, ct);
        Assert.Contains(members, m => m.Name == "Kim");
        Assert.Single(await f.Directory.GetRoomsForTeamAsync("Marketing", ct));
    }

    /// <summary>A member leaving the Team stays in the Team Room and nothing is posted (T4).</summary>
    [Fact]
    public async Task Reconcile_MemberLeavesTeam_StaysInRoom_AndNothingIsPosted()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(["Nova", "Kim"], ct);
        f.Catalog.Teams = [Team("Marketing", "Nova", "Kim")];
        await f.Reconciler.ReconcileAsync(ct);
        Room room = await f.TeamRoomAsync("Marketing", ct);
        int before = (await f.Store.ReadTailAsync(room.Id, 100, ct)).Count;
        f.Catalog.Teams = [Team("Marketing", "Nova")];

        await f.Reconciler.ReconcileAsync(ct);

        IReadOnlyList<User> members = await f.Directory.GetRoomMembersAsync(room.Id, ct);
        Assert.Contains(members, m => m.Name == "Kim");
        Assert.Equal(before, (await f.Store.ReadTailAsync(room.Id, 100, ct)).Count);
    }

    /// <summary>Every non-archived Room scoped to a Team that left the catalog is archived (T11).</summary>
    [Fact]
    public async Task Reconcile_TeamLeavesCatalog_ArchivesItsRooms()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(["Nova", "Ada"], ct);
        f.Catalog.Teams = [Team("Marketing", "Nova"), Team("Sales", "Ada")];
        await f.Reconciler.ReconcileAsync(ct);
        User? nova = await f.Directory.FindUserByNameAsync("Nova", ct);
        Assert.NotNull(nova);
        Room chat = await f.Directory.CreateRoomAsync("Nova", [KnownIds.Human, nova.Id], "Marketing", false, ct);
        f.Catalog.Teams = [Team("Sales", "Ada")];

        await f.Reconciler.ReconcileAsync(ct);

        Assert.All(await f.Directory.GetRoomsForTeamAsync("Marketing", ct), r => Assert.True(r.Archived));
        Room? reread = await f.Directory.GetRoomAsync(chat.Id, ct);
        Assert.True(reread?.Archived);
        Assert.False((await f.TeamRoomAsync("Sales", ct)).Archived);
    }

    /// <summary>An Archived Team Room is left alone: not unarchived, not recreated, not filled (T13).</summary>
    [Fact]
    public async Task Reconcile_ArchivedTeamRoom_IsLeftAlone()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(["Nova", "Kim"], ct);
        f.Catalog.Teams = [Team("Marketing", "Nova")];
        await f.Reconciler.ReconcileAsync(ct);
        Room room = await f.TeamRoomAsync("Marketing", ct);
        await f.Chat.SetRoomArchivedAsync(room.Id, true, ct);
        f.Catalog.Teams = [Team("Marketing", "Nova", "Kim")];

        await f.Reconciler.ReconcileAsync(ct);

        Assert.Single(await f.Directory.GetRoomsForTeamAsync("Marketing", ct));
        Room found = await f.TeamRoomAsync("Marketing", ct);
        Assert.True(found.Archived);
        IReadOnlyList<User> members = await f.Directory.GetRoomMembersAsync(room.Id, ct);
        Assert.DoesNotContain(members, m => m.Name == "Kim");
    }

    /// <summary>Running again, or against a fresh reconciler over the same database (a restart), creates nothing new (T16).</summary>
    [Fact]
    public async Task Reconcile_RunTwiceAndAfterRestart_CreatesNothingNew()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(["Nova"], ct);
        f.Catalog.Teams = [Team("Marketing", "Nova")];
        await f.Reconciler.ReconcileAsync(ct);
        await f.Reconciler.ReconcileAsync(ct);
        TeamRoomReconciler restarted = f.NewReconciler();

        await restarted.ReconcileAsync(ct);

        Assert.Single(await f.Directory.GetRoomsForTeamAsync("Marketing", ct));
        Assert.Single(await f.Directory.GetRoomsAsync(ct), r => r.Team is not null);
    }

    /// <summary>An empty catalog changes nothing at all: a not-yet-loaded catalog must not archive every scoped Room.</summary>
    [Fact]
    public async Task Reconcile_EmptyCatalog_ChangesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(["Nova"], ct);
        f.Catalog.Teams = [Team("Marketing", "Nova")];
        await f.Reconciler.ReconcileAsync(ct);
        f.Catalog.Teams = [];

        await f.Reconciler.ReconcileAsync(ct);

        Assert.False((await f.TeamRoomAsync("Marketing", ct)).Archived);
    }

    /// <summary>A Team with no members, or none who has connected yet, gets no Room.</summary>
    [Fact]
    public async Task Reconcile_TeamWithNoConnectedMembers_CreatesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(["Nova"], ct);
        f.Catalog.Teams = [new TeamSummary("Empty", [], [], true), Team("Ghosts", "NeverConnected")];

        await f.Reconciler.ReconcileAsync(ct);

        Assert.Null(await f.Directory.FindTeamRoomAsync("Empty", ct));
        Assert.Null(await f.Directory.FindTeamRoomAsync("Ghosts", ct));
    }

    /// <summary>Two reconciles started at once create one Team Room.</summary>
    [Fact]
    public async Task Reconcile_TwoAtOnce_CreateOneRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(["Nova"], ct);
        f.Catalog.Teams = [Team("Marketing", "Nova")];

        await Task.WhenAll(f.Reconciler.ReconcileAsync(ct), f.NewReconciler().ReconcileAsync(ct), f.Reconciler.ReconcileAsync(ct));

        Assert.Single(await f.Directory.GetRoomsForTeamAsync("Marketing", ct));
    }

    /// <summary>Raising the catalog's <c>Changed</c> runs a reconcile, and a failure inside it never escapes the handler.</summary>
    [Fact]
    public async Task CatalogChanged_RunsReconcile()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(["Nova"], ct);
        f.Catalog.Teams = [];
        await f.Reconciler.StartAsync(ct);
        f.Catalog.Teams = [Team("Marketing", "Nova")];

        f.Catalog.Raise();
        await f.Reconciler.LastRun;

        Assert.NotNull(await f.Directory.FindTeamRoomAsync("Marketing", ct));
        await f.Reconciler.StopAsync(ct);
        Assert.Equal(0, f.Catalog.ChangedSubscriberCount);
    }

    /// <summary>A User row appearing (<c>RoomsChanged</c>) reconciles, so a member who connects late is added.</summary>
    [Fact]
    public async Task RoomsChanged_AddsMemberWhoConnectedLate()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(["Nova"], ct);
        f.Catalog.Teams = [Team("Marketing", "Nova", "Late")];
        await f.Reconciler.StartAsync(ct);
        await f.Reconciler.LastRun;
        await f.Directory.UpsertAgentUserAsync("Late", null, ct);

        f.Events.PublishRoomsChanged();
        await f.Reconciler.LastRun;

        Room room = await f.TeamRoomAsync("Marketing", ct);
        Assert.Contains(await f.Directory.GetRoomMembersAsync(room.Id, ct), m => m.Name == "Late");
        await f.Reconciler.StopAsync(ct);
    }

    /// <summary>Starting the host reconciles once, without any event.</summary>
    [Fact]
    public async Task Start_ReconcilesOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(["Nova"], ct);
        f.Catalog.Teams = [Team("Marketing", "Nova")];

        await f.Reconciler.StartAsync(ct);
        await f.Reconciler.LastRun;

        Assert.NotNull(await f.Directory.FindTeamRoomAsync("Marketing", ct));
        await f.Reconciler.StopAsync(ct);
    }

    /// <summary>Stopping or signalling a disposed reconciler is harmless: no throw, no further run.</summary>
    [Fact]
    public async Task Disposed_StopAndSignal_DoNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(["Nova"], ct);
        f.Catalog.Teams = [Team("Marketing", "Nova")];
        TeamRoomReconciler reconciler = f.NewReconciler();
        await reconciler.StartAsync(ct);
        await reconciler.LastRun;
        reconciler.Dispose();
        f.Catalog.Teams = [Team("Marketing", "Nova"), Team("Sales", "Nova")];

        f.Catalog.Raise();
        await reconciler.StopAsync(ct);

        Assert.Null(await f.Directory.FindTeamRoomAsync("Sales", ct));
        Assert.Equal(0, f.Catalog.ChangedSubscriberCount);
    }

    /// <summary>A Room the Human unarchives after its Team left the catalog stays unarchived on the next reconcile (T12).</summary>
    [Fact]
    public async Task Reconcile_HumanUnarchivesRoomOfVanishedTeam_StaysUnarchived()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(["Nova", "Ada"], ct);
        f.Catalog.Teams = [Team("Marketing", "Nova"), Team("Sales", "Ada")];
        await f.Reconciler.StartAsync(ct);
        await f.Reconciler.LastRun;
        Room teamRoom = await f.TeamRoomAsync("Marketing", ct);
        f.Catalog.Teams = [Team("Sales", "Ada")];
        f.Catalog.Raise();
        await f.Reconciler.LastRun;
        Assert.True((await f.TeamRoomAsync("Marketing", ct)).Archived);

        _ = await f.Chat.SetRoomArchivedAsync(teamRoom.Id, false, ct);
        await f.Reconciler.LastRun;

        Assert.False((await f.TeamRoomAsync("Marketing", ct)).Archived);
        await f.Reconciler.StopAsync(ct);
    }

    /// <summary>A Team that drops out of the catalog and returns gets its Team Room back, unarchived and filled (D-10 does not strand it).</summary>
    [Fact]
    public async Task Reconcile_TeamVanishesAndReturns_UnarchivesTeamRoomAndInvitesNewMember()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(["Nova", "Ada", "Kim"], ct);
        f.Catalog.Teams = [Team("Marketing", "Nova"), Team("Sales", "Ada")];
        await f.Reconciler.ReconcileAsync(ct);
        f.Catalog.Teams = [Team("Sales", "Ada")];
        await f.Reconciler.ReconcileAsync(ct);
        Assert.True((await f.TeamRoomAsync("Marketing", ct)).Archived);
        f.Catalog.Teams = [Team("Marketing", "Nova", "Kim"), Team("Sales", "Ada")];

        await f.Reconciler.ReconcileAsync(ct);

        Room room = await f.TeamRoomAsync("Marketing", ct);
        Assert.False(room.Archived);
        Assert.Contains(await f.Directory.GetRoomMembersAsync(room.Id, ct), m => m.Name == "Kim");
        Assert.Single(await f.Directory.GetRoomsForTeamAsync("Marketing", ct));
    }

    /// <summary>A Team Room the Human archived stays archived through a vanish and a return of its Team (D-10).</summary>
    [Fact]
    public async Task Reconcile_HumanArchivedTeamRoom_StaysArchivedThroughVanishAndReturn()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(["Nova", "Ada"], ct);
        f.Catalog.Teams = [Team("Marketing", "Nova"), Team("Sales", "Ada")];
        await f.Reconciler.ReconcileAsync(ct);
        Room room = await f.TeamRoomAsync("Marketing", ct);
        _ = await f.Chat.SetRoomArchivedAsync(room.Id, true, ct);
        await f.Reconciler.ReconcileAsync(ct);
        Assert.True((await f.TeamRoomAsync("Marketing", ct)).Archived);
        f.Catalog.Teams = [Team("Sales", "Ada")];
        await f.Reconciler.ReconcileAsync(ct);
        f.Catalog.Teams = [Team("Marketing", "Nova"), Team("Sales", "Ada")];

        await f.Reconciler.ReconcileAsync(ct);

        Assert.True((await f.TeamRoomAsync("Marketing", ct)).Archived);
        Assert.Single(await f.Directory.GetRoomsForTeamAsync("Marketing", ct));
    }

    /// <summary>The first run seeds from the stored Rooms: a Room scoped to a Team missing from the catalog is archived once, then left alone.</summary>
    [Fact]
    public async Task Reconcile_FirstRun_ArchivesRoomOfTeamAbsentSinceBeforeStartup_Once()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(["Nova"], ct);
        User? nova = await f.Directory.FindUserByNameAsync("Nova", ct);
        Assert.NotNull(nova);
        Room ghost = await f.Directory.CreateRoomAsync("Ghost chat", [KnownIds.Human, nova.Id], "Ghost", false, ct);
        f.Catalog.Teams = [Team("Marketing", "Nova")];

        await f.Reconciler.ReconcileAsync(ct);

        Room? archived = await f.Directory.GetRoomAsync(ghost.Id, ct);
        Assert.True(archived?.Archived);
        await f.Directory.SetRoomArchivedAsync(ghost.Id, false, ct);
        await f.Reconciler.ReconcileAsync(ct);
        Room? again = await f.Directory.GetRoomAsync(ghost.Id, ct);
        Assert.False(again?.Archived);
    }

    /// <summary>A catalog spelling that differs only in case from the stored Team creates no second Team Room and archives nothing.</summary>
    [Fact]
    public async Task Reconcile_CatalogSpellingDiffersInCase_CreatesNothingAndArchivesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(["Nova"], ct);
        f.Catalog.Teams = [Team("Marketing", "Nova")];
        await f.Reconciler.ReconcileAsync(ct);
        f.Catalog.Teams = [Team("marketing", "Nova")];
        TeamRoomReconciler restarted = f.NewReconciler();

        await restarted.ReconcileAsync(ct);

        Assert.Single(await f.Directory.GetRoomsForTeamAsync("Marketing", ct));
        Assert.Single(await f.Directory.GetRoomsAsync(ct), r => r.Team is not null);
        Assert.False((await f.TeamRoomAsync("Marketing", ct)).Archived);
    }

    /// <summary>Many signals while a run is in flight collapse into at most one follow-up run, and the end state is correct.</summary>
    [Fact]
    public async Task Signals_WhileRunInFlight_CoalesceIntoOneFollowUpRun()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(["Nova"], ct);
        f.Catalog.Teams = [Team("Marketing", "Nova")];
        using GatedCatalog gated = new(f.Catalog);
        TeamRoomReconciler reconciler = f.NewReconciler(gated, new RecordingLogger<TeamRoomReconciler>());
        gated.Close();
        await reconciler.StartAsync(ct);
        await gated.Entered.WaitAsync(ct);

        for (int i = 0; i < 10; i++)
        {
            f.Catalog.Raise();
        }

        gated.Open();
        await reconciler.LastRun;

        Assert.InRange(gated.Reads, 1, 2);
        Assert.Single(await f.Directory.GetRoomsForTeamAsync("Marketing", ct));
        await reconciler.StopAsync(ct);
    }

    /// <summary>Disposing while a run is in flight neither throws nor logs a failure once the run ends.</summary>
    [Fact]
    public async Task Dispose_WhileRunInFlight_LogsNoFailure()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(["Nova"], ct);
        f.Catalog.Teams = [Team("Marketing", "Nova")];
        using GatedCatalog gated = new(f.Catalog);
        RecordingLogger<TeamRoomReconciler> log = new();
        TeamRoomReconciler reconciler = f.NewReconciler(gated, log);
        gated.Close();
        await reconciler.StartAsync(ct);
        await gated.Entered.WaitAsync(ct);

        reconciler.Dispose();
        gated.Open();
        await reconciler.LastRun;

        Assert.Equal(0, log.CountAtLeast(LogLevel.Warning));
    }

    /// <summary>Stopping while a run is in flight waits for it, then disposing logs no failure.</summary>
    [Fact]
    public async Task Stop_WhileRunInFlight_ThenDispose_LogsNoFailure()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(["Nova"], ct);
        f.Catalog.Teams = [Team("Marketing", "Nova")];
        using GatedCatalog gated = new(f.Catalog);
        RecordingLogger<TeamRoomReconciler> log = new();
        TeamRoomReconciler reconciler = f.NewReconciler(gated, log);
        gated.Close();
        await reconciler.StartAsync(ct);
        await gated.Entered.WaitAsync(ct);

        Task stopping = reconciler.StopAsync(ct);
        gated.Open();
        await stopping;
        reconciler.Dispose();

        Assert.Equal(0, log.CountAtLeast(LogLevel.Warning));
    }

    /// <summary>Builds a Team summary with the given member names.</summary>
    /// <param name="name">The Team name.</param>
    /// <param name="members">The member Persona names.</param>
    private static TeamSummary Team(string name, params string[] members) => new(name, [], members, true);

    /// <summary>A real directory, chat service and hand-driven catalog over a temp data directory.</summary>
    private sealed class Fixture : IDisposable
    {
        private readonly TempDataDir dir;

        private Fixture(TempDataDir dir, SqliteTeamDirectory directory)
        {
            this.dir = dir;
            this.Directory = directory;
            this.Store = dir.CreateChatStore();
            this.Events = new RoomEvents(NullLogger<RoomEvents>.Instance);
            this.Chat = new ChatService(
                directory,
                this.Store,
                this.Events,
                new FakeMentionAliasSource { Aliases = [] },
                TestOptions.Create(),
                new ProposalStore(this.Events),
                new QuestionStore(this.Events),
                NullLogger<ChatService>.Instance)
            {
                TeamCatalog = this.Catalog,
            };
            this.Reconciler = this.NewReconciler();
        }

        public SqliteTeamDirectory Directory { get; }

        public FileChatStore Store { get; }

        public RoomEvents Events { get; }

        public ChatService Chat { get; }

        public FakeTeamCatalog Catalog { get; } = new();

        public TeamRoomReconciler Reconciler { get; }

        /// <summary>Creates the directory and a connected User for each of <paramref name="agents"/>.</summary>
        /// <param name="agents">The Persona names that have a User row.</param>
        /// <param name="ct">Cancels the setup.</param>
        public static async Task<Fixture> CreateAsync(string[] agents, CancellationToken ct)
        {
            TempDataDir dir = new();
            SqliteTeamDirectory directory = new(dir.Options());
            await directory.InitializeAsync("You", ct);
            foreach (string agent in agents)
            {
                await directory.UpsertAgentUserAsync(agent, null, ct);
            }

            return new Fixture(dir, directory);
        }

        /// <summary>The Team Room of <paramref name="team"/>; fails the test when there is none.</summary>
        /// <param name="team">The Team name.</param>
        /// <param name="ct">Cancels the read.</param>
        public async Task<Room> TeamRoomAsync(string team, CancellationToken ct)
        {
            Room? room = await this.Directory.FindTeamRoomAsync(team, ct);
            Assert.NotNull(room);
            return room;
        }

        /// <summary>Another reconciler over the same services, as a second host start would build.</summary>
        public TeamRoomReconciler NewReconciler() =>
            new(this.Directory, this.Chat, this.Catalog, this.Events, NullLogger<TeamRoomReconciler>.Instance);

        /// <summary>A reconciler over the same services that tells <paramref name="feed"/> about the Rooms it creates.</summary>
        /// <param name="feed">The Feed publisher.</param>
        public TeamRoomReconciler NewReconciler(IFeedPublisher feed) =>
            new(this.Directory, this.Chat, this.Catalog, this.Events, NullLogger<TeamRoomReconciler>.Instance, feed);

        /// <summary>A reconciler over the same services but another catalog and logger.</summary>
        /// <param name="catalog">The catalog it reads.</param>
        /// <param name="logger">The logger it reports to.</param>
        public TeamRoomReconciler NewReconciler(ITeamCatalog catalog, ILogger<TeamRoomReconciler> logger) =>
            new(this.Directory, this.Chat, catalog, this.Events, logger);

        /// <inheritdoc />
        public void Dispose() => this.dir.Dispose();
    }

    /// <summary>Wraps a catalog so a read of <see cref="Teams"/> can be held shut, and counts the reads (one per run).</summary>
    private sealed class GatedCatalog(FakeTeamCatalog inner) : ITeamCatalog, IDisposable
    {
        private readonly ManualResetEventSlim gate = new(true);
        private int reads;

        /// <summary>Released once for each read of <see cref="Teams"/> that has started.</summary>
        public SemaphoreSlim Entered { get; } = new(0);

        /// <summary>How many times <see cref="Teams"/> was read.</summary>
        public int Reads => Volatile.Read(ref this.reads);

        /// <inheritdoc />
        public IReadOnlyList<TeamSummary> Teams
        {
            get
            {
                _ = Interlocked.Increment(ref this.reads);
                _ = this.Entered.Release();
                this.gate.Wait();
                return inner.Teams;
            }
        }

        /// <inheritdoc />
        public event Action? Changed
        {
            add => inner.Changed += value;
            remove => inner.Changed -= value;
        }

        /// <summary>Makes the next reads of <see cref="Teams"/> wait until <see cref="Open"/>.</summary>
        public void Close() => this.gate.Reset();

        /// <summary>Lets every waiting and later read through.</summary>
        public void Open() => this.gate.Set();

        /// <inheritdoc />
        public TeamSummary? Find(string team) => inner.Find(team);

        /// <inheritdoc />
        public bool ProjectExists(string team, string project) => inner.ProjectExists(team, project);

        /// <inheritdoc />
        public void Dispose()
        {
            this.gate.Dispose();
            this.Entered.Dispose();
        }
    }
}
