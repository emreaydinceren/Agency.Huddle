using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Agents;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.SecondBrain;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Agents;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.Library;
using Agency.Huddle.Tests.Tasks;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// Pins <see cref="TeamRenameService.RecoverAsync"/> (Team Rename Spec R16 and §5.4, corrections-batch2 tr6): each crash state is
/// arranged by hand (a Pending journal row plus the folders and rows as they would be at that point) and recovery decides the
/// step from disk, in the order Old, the temporary folder, New, then rolls forward idempotently.
/// </summary>
public sealed class TeamRenameRecoveryTests
{
    private const string Marketing = "Marketing";
    private const string Growth = "Growth";

    /// <summary>R16: the folder moved but the labels, Rooms and prefix did not; recovery rolls forward and finishes.</summary>
    [Fact]
    public async Task RecoverAsync_FolderMovedRowsNot_RollsForwardAndCompletes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(Marketing, ct);
        TaskId first = f.Prefixes.Next(Marketing, 0);
        _ = await f.Directory.CreateRoomAsync("Room 1", [KnownIds.Human], Marketing, false, ct: ct);
        _ = f.Renames.Begin(TeamRenameKind.Team, Marketing, Marketing, Growth);
        Directory.Move(f.TeamFolder(Marketing), f.TeamFolder(Growth));

        await f.Service.RecoverAsync(ct);

        Assert.Equal([Growth], f.Labels("Ada"));
        Assert.Equal([Growth, "Other"], f.Labels("Nova"));
        Assert.Single(await f.Directory.GetRoomsForTeamAsync(Growth, ct));
        Assert.Empty(await f.Directory.GetRoomsForTeamAsync(Marketing, ct));
        TaskId next = f.Prefixes.Next(Growth, 0);
        Assert.Equal((first.Prefix, first.Number + 1), (next.Prefix, next.Number));
        TeamRename row = Assert.Single(f.Renames.DoneAfter(0));
        Assert.Equal((Marketing, Growth, TeamRenameState.Done), (row.OldName, row.NewName, row.State));
        Assert.Empty(f.Renames.Pending());
        Assert.False(Directory.Exists(f.TeamFolder(Marketing)));
        Assert.True(File.Exists(Path.Combine(f.TeamFolder(Growth), "notes.md")));
    }

    /// <summary>The move never happened (Old is still there, no temporary folder, no New): the row is discarded and nothing else changes.</summary>
    [Fact]
    public async Task RecoverAsync_MoveNeverHappened_DiscardsRowAndLeavesTeamIntact()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(Marketing, ct);
        _ = f.Renames.Begin(TeamRenameKind.Team, Marketing, Marketing, Growth);

        await f.Service.RecoverAsync(ct);

        Assert.Empty(f.Renames.Pending());
        Assert.Empty(f.Renames.DoneAfter(0));
        Assert.True(File.Exists(Path.Combine(f.TeamFolder(Marketing), "notes.md")));
        Assert.False(Directory.Exists(f.TeamFolder(Growth)));
        Assert.Equal([Marketing], f.Labels("Ada"));
        Assert.Equal([Marketing, "Other"], f.Labels("Nova"));
        Assert.Equal([Marketing], f.Labels("Bo"));
    }

    /// <summary>A case-only rename that crashed with the temporary folder left: the folder ends spelled New, no temporary folder stays, the row is Done.</summary>
    [Fact]
    public async Task RecoverAsync_TempFolderLeft_FinishesMoveAndCompletes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(Growth, ct);
        _ = f.Renames.Begin(TeamRenameKind.Team, Growth, Growth, "growth");
        Directory.Move(f.TeamFolder(Growth), f.TeamFolder(TeamRenameService.RenamingPrefix + "growth"));

        await f.Service.RecoverAsync(ct);

        List<string> names = [.. Directory.GetDirectories(f.TeamsRoot).Select(static p => Path.GetFileName(p))];
        Assert.Contains("growth", names);
        Assert.DoesNotContain(Growth, names);
        Assert.DoesNotContain(names, static n => n.StartsWith(".huddle-renaming", StringComparison.Ordinal));
        Assert.True(File.Exists(Path.Combine(f.TeamFolder("growth"), "notes.md")));
        Assert.Equal(["growth"], f.Labels("Ada"));
        Assert.Equal(TeamRenameState.Done, Assert.Single(f.Renames.DoneAfter(0)).State);
        Assert.Empty(f.Renames.Pending());
    }

    /// <summary>Teams/New exists but Archive/Old was left behind: the archive folder moves to Archive/New and the row is Done.</summary>
    [Fact]
    public async Task RecoverAsync_ArchiveOldLeftBehind_MovesArchiveAndCompletes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(Marketing, ct);
        string archived = Path.Combine(f.ArchiveRoot, Marketing);
        Directory.CreateDirectory(archived);
        File.WriteAllText(Path.Combine(archived, "old.md"), "archived project");
        _ = f.Renames.Begin(TeamRenameKind.Team, Marketing, Marketing, Growth);
        Directory.Move(f.TeamFolder(Marketing), f.TeamFolder(Growth));

        await f.Service.RecoverAsync(ct);

        Assert.False(Directory.Exists(archived));
        Assert.Equal("archived project", File.ReadAllText(Path.Combine(f.ArchiveRoot, Growth, "old.md")));
        Assert.Equal(TeamRenameState.Done, Assert.Single(f.Renames.DoneAfter(0)).State);
        Assert.Empty(f.Renames.Pending());
    }

    /// <summary>Everything was done but the row was still Pending (a crash before Complete): recovery duplicates nothing and marks it Done.</summary>
    [Fact]
    public async Task RecoverAsync_AlreadyDoneButPending_IsIdempotent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(Marketing, ct);
        TaskId first = f.Prefixes.Next(Marketing, 0);
        _ = await f.Directory.CreateRoomAsync("Room 1", [KnownIds.Human], Marketing, false, ct: ct);
        _ = f.Renames.Begin(TeamRenameKind.Team, Marketing, Marketing, Growth);
        Directory.Move(f.TeamFolder(Marketing), f.TeamFolder(Growth));
        _ = await f.Directory.RenameTeamScopeAsync(Marketing, Growth, ct);
        f.Prefixes.RenameTeam(Marketing, Growth);
        f.Membership.RenameLabel(Marketing, Growth);
        string[] before = [.. Directory.GetDirectories(f.TeamsRoot).Select(static p => Path.GetFileName(p)).Order(StringComparer.Ordinal)];

        await f.Service.RecoverAsync(ct);

        Assert.Equal([Growth], f.Labels("Ada"));
        Assert.Equal([Growth, "Other"], f.Labels("Nova"));
        Assert.Single(await f.Directory.GetRoomsForTeamAsync(Growth, ct));
        TaskId next = f.Prefixes.Next(Growth, 0);
        Assert.Equal((first.Prefix, first.Number + 1), (next.Prefix, next.Number));
        Assert.Equal<IEnumerable<string>>(before, Directory.GetDirectories(f.TeamsRoot).Select(static p => Path.GetFileName(p)).Order(StringComparer.Ordinal));
        Assert.Equal(TeamRenameState.Done, Assert.Single(f.Renames.DoneAfter(0)).State);
        Assert.Empty(f.Renames.Pending());
    }

    /// <summary>Two Pending rows are recovered in id order, each once.</summary>
    [Fact]
    public async Task RecoverAsync_TwoPendingRows_RecoveredInIdOrder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RecordingRewrite rewrite = new();
        using Fixture f = await Fixture.CreateAsync(Marketing, ct, rewrite);
        _ = f.Renames.Begin(TeamRenameKind.Team, Marketing, Marketing, Growth);
        _ = f.Renames.Begin(TeamRenameKind.Team, "Sales", "Sales", "Revenue");
        Directory.Move(f.TeamFolder(Marketing), f.TeamFolder(Growth));
        Directory.Move(f.TeamFolder("Sales"), f.TeamFolder("Revenue"));

        await f.Service.RecoverAsync(ct);

        Assert.Equal([Marketing, "Sales"], rewrite.Calls.Select(static c => c.OldName));
        Assert.Equal([TeamRenameState.Done, TeamRenameState.Done], f.Renames.DoneAfter(0).Select(static r => r.State));
        Assert.Equal([Marketing, "Sales"], f.Renames.DoneAfter(0).Select(static r => r.OldName));
        Assert.Empty(f.Renames.Pending());
    }

    /// <summary>The labels still say Marketing after the roll-forward (the rewrite changes nothing): the row stays Pending and the folder stays at New; a second recovery with a working membership completes it.</summary>
    [Fact]
    public async Task RecoverAsync_LabelsWontRewrite_RowStaysPending()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(Marketing, ct);
        _ = f.Renames.Begin(TeamRenameKind.Team, Marketing, Marketing, Growth);
        Directory.Move(f.TeamFolder(Marketing), f.TeamFolder(Growth));
        f.Switch.Skip = true;

        await f.Service.RecoverAsync(ct);

        TeamRename pending = Assert.Single(f.Renames.Pending());
        Assert.Equal((Marketing, Growth), (pending.OldName, pending.NewName));
        Assert.Empty(f.Renames.DoneAfter(0));
        Assert.True(Directory.Exists(f.TeamFolder(Growth)));
        Assert.False(Directory.Exists(f.TeamFolder(Marketing)));
        Assert.Equal([Marketing], f.Labels("Ada"));

        f.Switch.Skip = false;
        await f.Service.RecoverAsync(ct);

        Assert.Equal([Growth], f.Labels("Ada"));
        Assert.Equal(TeamRenameState.Done, Assert.Single(f.Renames.DoneAfter(0)).State);
        Assert.Empty(f.Renames.Pending());
        Assert.True(Directory.Exists(f.TeamFolder(Growth)));
    }

    /// <summary>A membership that rewrites labels through the real one unless <see cref="Skip"/> is set, when <see cref="RenameLabel"/> changes nothing.</summary>
    /// <param name="inner">The real membership.</param>
    private sealed class SwitchableMembership(ITeamMembership inner) : ITeamMembership
    {
        /// <summary>Gets or sets whether <see cref="RenameLabel"/> does nothing.</summary>
        public bool Skip { get; set; }

        /// <inheritdoc />
        public MembershipResult Add(string team, string personaName) => inner.Add(team, personaName);

        /// <inheritdoc />
        public MembershipResult Remove(string team, string personaName) => inner.Remove(team, personaName);

        /// <inheritdoc />
        public MembershipResult RemoveForArchive(string team, string personaName) => inner.RemoveForArchive(team, personaName);

        /// <inheritdoc />
        public MembershipResult ReplaceManager(string team, string newManager) => inner.ReplaceManager(team, newManager);

        /// <inheritdoc />
        public void RenameLabel(string oldName, string newName)
        {
            if (!this.Skip)
            {
                inner.RenameLabel(oldName, newName);
            }
        }
    }

    /// <summary>A rewrite fake that records the renames it ran for.</summary>
    private sealed class RecordingRewrite : ITeamRenameRewrite
    {
        private readonly List<TeamRename> calls = [];

        /// <summary>Gets the renames it ran for, in order.</summary>
        public IReadOnlyList<TeamRename> Calls => this.calls;

        /// <inheritdoc />
        public IReadOnlyList<string> TeamsTouched(TeamRename rename) => [];

        /// <inheritdoc />
        public Task RunAsync(TeamRename rename, CancellationToken ct)
        {
            this.calls.Add(rename);
            return Task.CompletedTask;
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly TempDataDir dir;
        private readonly LibraryFileServiceFixture library;
        private readonly PersonaStore personas;
        private readonly TaskStore tasks;

        private Fixture(TempDataDir dir, LibraryFileServiceFixture library, PersonaStore personas, TaskStore tasks, SqliteTeamDirectory directory, FakeTeamCatalog catalog, TeamFolderProvisioner folders, IEnumerable<ITeamRenameRewrite> rewrites)
        {
            this.dir = dir;
            this.library = library;
            this.personas = personas;
            this.tasks = tasks;
            this.Directory = directory;
            this.Prefixes = new TaskIdAllocator(dir.Options());
            CadenceStateStore cadences = new(dir.Options());
            this.Renames = new TeamRenameStore(dir.Options());
            this.Membership = new TeamMembership(personas, catalog, folders, RoleFixtures.Types(dir));
            this.Switch = new SwitchableMembership(this.Membership);
            RoomEvents events = new(NullLogger<RoomEvents>.Instance);
            ChatService chat = new(
                directory,
                dir.CreateChatStore(),
                events,
                new FakeMentionAliasSource { Aliases = [] },
                TestOptions.Create(),
                new ProposalStore(events),
                new QuestionStore(events),
                NullLogger<ChatService>.Instance)
            {
                TeamCatalog = catalog,
            };
            this.Reconciler = new TeamRoomReconciler(directory, chat, catalog, events, NullLogger<TeamRoomReconciler>.Instance);
            this.TeamsRoot = library.RootStore.Roots.Single(r => r.Kind == LibraryRootKind.Teams).FullPath;
            this.ArchiveRoot = library.RootStore.Roots.Single(r => r.Kind == LibraryRootKind.Archive).FullPath;
            System.IO.Directory.CreateDirectory(this.ArchiveRoot);
            this.Service = new TeamRenameService(
                library.RootStore,
                catalog,
                this.Switch,
                folders,
                tasks,
                this.Prefixes,
                cadences,
                personas,
                directory,
                new PhysicalArchiveFileSystem(),
                this.Renames,
                this.Reconciler,
                rewrites,
                new TeamOperationGate(),
                NullLogger<TeamRenameService>.Instance);
        }

        public TeamRenameService Service { get; }

        public TeamRenameStore Renames { get; }

        public TeamMembership Membership { get; }

        public SwitchableMembership Switch { get; }

        public TaskIdAllocator Prefixes { get; }

        public SqliteTeamDirectory Directory { get; }

        public TeamRoomReconciler Reconciler { get; }

        public string TeamsRoot { get; }

        public string ArchiveRoot { get; }

        /// <summary>Builds the fixture: the Team and a Sales Team on disk, three members (Ada, Nova, Bo) and a Room database with their Users.</summary>
        /// <param name="team">The Team to create.</param>
        /// <param name="ct">Cancels the database set-up.</param>
        /// <param name="rewrite">An optional step-6 rewrite to register.</param>
        public static async Task<Fixture> CreateAsync(string team, CancellationToken ct, ITeamRenameRewrite? rewrite = null)
        {
            TempDataDir dir = new();
            LibraryFileServiceFixture library = LibraryFileServiceFixture.Attach(dir.Path);
            PersonaStore personas = dir.CreatePersonaStore();
            FakeTeamCatalog catalog = new();
            TeamFolderProvisioner folders = new(personas, library.RootStore, library.Resolver, NullLogger<TeamFolderProvisioner>.Instance, catalog);
            string teamsRoot = library.RootStore.Roots.Single(r => r.Kind == LibraryRootKind.Teams).FullPath;
            TeamFile file = new(new VersionedRef("web", "1.2.0"), "Ada", null, null, string.Empty);
            Assert.Equal(TeamCreateOutcome.Created, folders.CreateTeam(team, file).Outcome);
            _ = personas.Add(new PersonaIdentity("Ada", "Lead", "ada", [team]), "Leads.");
            _ = personas.Add(new PersonaIdentity("Nova", "Dev", "nova", [team, "Other"]), "Builds.");
            _ = personas.Add(new PersonaIdentity("Bo", "Dev", "bo", [team]), "Helps.");
            string home = Path.Combine(teamsRoot, team);
            File.WriteAllText(Path.Combine(home, "notes.md"), "# Notes\n");
            System.IO.Directory.CreateDirectory(Path.Combine(teamsRoot, "Sales"));
            catalog.Teams =
            [
                new TeamSummary(team, [], ["Ada", "Nova", "Bo"], HasFolder: true) { Manager = "Ada" },
                new TeamSummary("Sales", [], [], HasFolder: true),
            ];
            SqliteTeamDirectory directory = new(dir.Options());
            await directory.InitializeAsync("You", ct);
            foreach (string agent in new[] { "Ada", "Nova", "Bo" })
            {
                _ = await directory.UpsertAgentUserAsync(agent, null, ct);
            }

            TaskStore tasks = TestTaskStore.CreateTaskStore(dir, personas);
            ITeamRenameRewrite[] rewrites = rewrite is null ? [] : [rewrite];
            return new Fixture(dir, library, personas, tasks, directory, catalog, folders, rewrites);
        }

        /// <summary>The path <c>Teams/&lt;name&gt;</c>.</summary>
        /// <param name="name">The folder name.</param>
        public string TeamFolder(string name) => Path.Combine(this.TeamsRoot, name);

        /// <summary>The Team labels in the named Persona's definition, as the store holds them now.</summary>
        /// <param name="name">The Persona's name.</param>
        public IReadOnlyList<string> Labels(string name)
        {
            Persona? persona = this.personas.Get(name);
            Assert.NotNull(persona);
            Assert.True(PersonaFrontmatter.TryReadIdentity(persona.Text, out PersonaIdentity? identity, out _));
            return identity.Teams;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.Reconciler.Dispose();
            this.tasks.Dispose();
            this.library.Dispose();
            this.dir.Dispose();
        }
    }
}
