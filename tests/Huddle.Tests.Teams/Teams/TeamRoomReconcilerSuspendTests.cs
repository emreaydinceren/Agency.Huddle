using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Teams;
using Agency.Huddle.App.Agents;
using Agency.Huddle.Tests.Factories;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// Tests for <see cref="TeamRoomReconciler.SuspendAsync"/> (Team Rename spec §5.2, R3): while the scope is held no
/// reconcile creates or archives a Team Room, and disposing it runs exactly one reconcile. Nothing sleeps: a reconcile
/// started under the scope is shown to be blocked by its task not being complete.
/// </summary>
public sealed class TeamRoomReconcilerSuspendTests
{
    /// <summary>While suspended a Team leaves the catalog and another joins: no Room is archived or created and a started reconcile stays pending; after dispose both effects land.</summary>
    [Fact]
    public async Task SuspendAsync_CatalogChangesWhileHeld_NothingHappensUntilDispose()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(["Nova", "Ada"], ct);
        f.Catalog.Teams = [Team("A", "Nova")];
        await f.Reconciler.ReconcileAsync(ct);
        IAsyncDisposable scope = await f.Reconciler.SuspendAsync(ct);
        f.Catalog.Teams = [Team("B", "Ada")];

        Task pending = f.Reconciler.ReconcileAsync(ct);

        Assert.False(pending.IsCompleted);
        Assert.False((await f.TeamRoomAsync("A", ct)).Archived);
        Assert.Null(await f.Directory.FindTeamRoomAsync("B", ct));
        await scope.DisposeAsync();
        await pending;
        Assert.True((await f.TeamRoomAsync("A", ct)).Archived);
        Assert.False((await f.TeamRoomAsync("B", ct)).Archived);
    }

    /// <summary>R3: the Team Room's scope is moved to Growth and the catalog flips Marketing to Growth under the scope; after dispose nothing is archived and Growth has exactly the one Room.</summary>
    [Fact]
    public async Task SuspendAsync_TeamRoomScopeMovedAndCatalogRenamed_ArchivesNothingAndCreatesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(["Nova"], ct);
        f.Catalog.Teams = [Team("Marketing", "Nova")];
        await f.Reconciler.ReconcileAsync(ct);
        Room original = await f.TeamRoomAsync("Marketing", ct);
        IAsyncDisposable scope = await f.Reconciler.SuspendAsync(ct);

        f.MoveScope("Marketing", "Growth");
        f.Catalog.Teams = [Team("Growth", "Nova")];
        await scope.DisposeAsync();

        Room growth = await f.TeamRoomAsync("Growth", ct);
        Assert.Equal(original.Id, growth.Id);
        Assert.False(growth.Archived);
        Assert.Single(await f.Directory.GetRoomsForTeamAsync("Growth", ct));
        Assert.Empty(await f.Directory.GetRoomsForTeamAsync("Marketing", ct));
        Assert.Single(await f.Directory.GetRoomsAsync(ct), r => r.Team is not null);
    }

    /// <summary>Disposing the scope runs exactly one reconcile, and none ran while it was held.</summary>
    [Fact]
    public async Task SuspendAsync_Dispose_RunsExactlyOneReconcile()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(["Nova"], ct);
        f.Catalog.Teams = [Team("Marketing", "Nova")];
        CountingCatalog counting = new(f.Catalog);
        TeamRoomReconciler reconciler = f.NewReconciler(counting);
        IAsyncDisposable scope = await reconciler.SuspendAsync(ct);
        Assert.Equal(0, counting.Reads);

        await scope.DisposeAsync();

        Assert.Equal(1, counting.Reads);
        Assert.NotNull(await f.Directory.FindTeamRoomAsync("Marketing", ct));
    }

    /// <summary>Disposing the scope twice neither throws nor reconciles a second time.</summary>
    [Fact]
    public async Task SuspendAsync_DisposeTwice_IsHarmless()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(["Nova"], ct);
        f.Catalog.Teams = [Team("Marketing", "Nova")];
        CountingCatalog counting = new(f.Catalog);
        TeamRoomReconciler reconciler = f.NewReconciler(counting);
        IAsyncDisposable scope = await reconciler.SuspendAsync(ct);
        await scope.DisposeAsync();

        Exception? thrown = await Record.ExceptionAsync(() => scope.DisposeAsync().AsTask());

        Assert.Null(thrown);
        Assert.Equal(1, counting.Reads);
        await reconciler.ReconcileAsync(ct);
        Assert.Equal(2, counting.Reads);
    }

    /// <summary>Disposing the scope after the reconciler itself was disposed does not throw and does not reconcile.</summary>
    [Fact]
    public async Task SuspendAsync_DisposeAfterReconcilerDisposed_DoesNotThrowOrReconcile()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(["Nova"], ct);
        f.Catalog.Teams = [Team("Marketing", "Nova")];
        CountingCatalog counting = new(f.Catalog);
        TeamRoomReconciler reconciler = f.NewReconciler(counting);
        IAsyncDisposable scope = await reconciler.SuspendAsync(ct);
        reconciler.Dispose();

        Exception? thrown = await Record.ExceptionAsync(() => scope.DisposeAsync().AsTask());

        Assert.Null(thrown);
        Assert.Equal(0, counting.Reads);
        Assert.Null(await f.Directory.FindTeamRoomAsync("Marketing", ct));
    }

    /// <summary>Builds a Team summary with the given member names.</summary>
    /// <param name="name">The Team name.</param>
    /// <param name="members">The member Persona names.</param>
    private static TeamSummary Team(string name, params string[] members) => new(name, [], members, true);

    /// <summary>Wraps a catalog and counts reads of <see cref="Teams"/>, which the reconciler makes once per run.</summary>
    private sealed class CountingCatalog(FakeTeamCatalog inner) : ITeamCatalog
    {
        private int reads;

        /// <summary>How many times <see cref="Teams"/> was read.</summary>
        public int Reads => Volatile.Read(ref this.reads);

        /// <inheritdoc />
        public IReadOnlyList<TeamSummary> Teams
        {
            get
            {
                _ = Interlocked.Increment(ref this.reads);
                return inner.Teams;
            }
        }

        /// <inheritdoc />
        public event Action? Changed
        {
            add => inner.Changed += value;
            remove => inner.Changed -= value;
        }

        /// <inheritdoc />
        public TeamSummary? Find(string team) => inner.Find(team);

        /// <inheritdoc />
        public bool ProjectExists(string team, string project) => inner.ProjectExists(team, project);
    }

    /// <summary>A real directory, chat service and hand-driven catalog over a temp data directory (the same arrangement as the reconciler tests).</summary>
    private sealed class Fixture : IDisposable
    {
        private readonly TempDataDir dir;

        private Fixture(TempDataDir dir, SqliteTeamDirectory directory)
        {
            this.dir = dir;
            this.Directory = directory;
            RoomEvents events = new(NullLogger<RoomEvents>.Instance);
            this.Events = events;
            this.Chat = new ChatService(
                directory,
                dir.CreateChatStore(),
                events,
                new FakeMentionAliasSource { Aliases = [] },
                TestOptions.Create(),
                new ProposalStore(events),
                new QuestionStore(events),
                NullLogger<ChatService>.Instance)
            {
                TeamCatalog = this.Catalog,
            };
            this.Reconciler = this.NewReconciler(this.Catalog);
        }

        public SqliteTeamDirectory Directory { get; }

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

        /// <summary>A reconciler over the same services reading <paramref name="catalog"/>.</summary>
        /// <param name="catalog">The catalog it reads.</param>
        public TeamRoomReconciler NewReconciler(ITeamCatalog catalog) =>
            new(this.Directory, this.Chat, catalog, this.Events, NullLogger<TeamRoomReconciler>.Instance);

        /// <summary>Rewrites the scope rows of <paramref name="from"/> to <paramref name="to"/>, as tr2's RenameTeamScopeAsync will; the directory has no such call yet.</summary>
        /// <param name="from">The old Team name.</param>
        /// <param name="to">The new Team name.</param>
        public void MoveScope(string from, string to)
        {
            string connectionString = SqliteTable.Open(this.dir.Options().Value, "SELECT 1;");
            using SqliteConnection connection = SqliteTable.Connect(connectionString);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "UPDATE room_teams SET team = $to WHERE team = $from;";
            _ = command.Parameters.AddWithValue("$to", to);
            _ = command.Parameters.AddWithValue("$from", from);
            Assert.Equal(1, command.ExecuteNonQuery());
        }

        /// <inheritdoc />
        public void Dispose() => this.dir.Dispose();
    }
}
