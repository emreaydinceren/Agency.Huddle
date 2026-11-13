using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Agents;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.SecondBrain;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Agents;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.Feed;
using Agency.Huddle.Tests.Library;
using Agency.Huddle.Tests.Tasks;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// Pins the review fixes of <see cref="TeamRenameService"/> (Team Rename Spec §5.3-5.5 and the tr5/tr6 corrections): recovery of an
/// archived temporary folder and of a differently spelled catalog name, the cancelled scope entry, the undo that fails, the Feed
/// Notification of an unfinished rename, the step-9 order, a thrown <see cref="ChatException"/> and the round trip back to a former
/// name. The fixture is a small copy of the one in <c>TeamRenameServiceTests</c> with a Feed, a probing catalog and a switchable membership.
/// </summary>
public sealed class TeamRenameReviewFixTests
{
    private const string Marketing = "Marketing";
    private const string Growth = "Growth";

    /// <summary>A case-only crash between <c>Archive/Old</c> to the temporary folder and the temporary folder to <c>Archive/New</c>: recovery finishes the archived move.</summary>
    [Fact]
    public async Task RecoverAsync_ArchiveTempFolderLeft_FinishesArchivedMoveAndCompletes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(Growth, ct);
        string archived = Path.Combine(f.ArchiveRoot, Growth);
        Directory.CreateDirectory(archived);
        File.WriteAllText(Path.Combine(archived, "old.md"), "archived project");
        _ = f.Renames.Begin(TeamRenameKind.Team, Growth, Growth, "growth");
        Fixture.Respell(f.TeamsRoot, Growth, "growth");
        Directory.Move(archived, Path.Combine(f.ArchiveRoot, TeamRenameService.RenamingPrefix + "growth"));

        await f.Service.RecoverAsync(ct);

        List<string> names = [.. Directory.GetDirectories(f.ArchiveRoot).Select(static p => Path.GetFileName(p))];
        Assert.Contains("growth", names);
        Assert.DoesNotContain(names, static n => n.StartsWith(".huddle-renaming", StringComparison.Ordinal));
        Assert.Equal("archived project", File.ReadAllText(Path.Combine(f.ArchiveRoot, "growth", "old.md")));
        Assert.Empty(f.Renames.Pending());
        Assert.Equal(TeamRenameState.Done, Assert.Single(f.Renames.DoneAfter(0)).State);
    }

    /// <summary>The journal row spells the old name "marketing" while the folders are spelled "Marketing": a crash after the Teams move still moves the archived folder.</summary>
    [Fact]
    public async Task RecoverAsync_CatalogSpellingDiffersFromFolder_MovesArchivedFolderAndCompletes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(Marketing, ct);
        string archived = Path.Combine(f.ArchiveRoot, Marketing);
        Directory.CreateDirectory(archived);
        File.WriteAllText(Path.Combine(archived, "old.md"), "archived project");
        _ = f.Renames.Begin(TeamRenameKind.Team, "marketing", "marketing", Growth);
        Directory.Move(f.TeamFolder(Marketing), f.TeamFolder(Growth));

        await f.Service.RecoverAsync(ct);

        Assert.Equal("archived project", File.ReadAllText(Path.Combine(f.ArchiveRoot, Growth, "old.md")));
        Assert.False(Directory.Exists(archived));
        Assert.Empty(f.Renames.Pending());
        Assert.Equal(TeamRenameState.Done, Assert.Single(f.Renames.DoneAfter(0)).State);
    }

    /// <summary>The journal row spells the old name "marketing" while the folder is spelled "Marketing" and nothing moved: the row is discarded, not left Pending.</summary>
    [Fact]
    public async Task RecoverAsync_CatalogSpellingDiffersAndNothingMoved_DiscardsRow()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(Marketing, ct);
        _ = f.Renames.Begin(TeamRenameKind.Team, "marketing", "marketing", Growth);

        await f.Service.RecoverAsync(ct);

        Assert.Empty(f.Renames.Pending());
        Assert.Empty(f.Renames.DoneAfter(0));
        Assert.True(Directory.Exists(f.TeamFolder(Marketing)));
    }

    /// <summary>A cancellation while waiting for the reconciler releases every pause and discards the row.</summary>
    [Fact]
    public async Task RenameTeamAsync_CancelledWhileSuspending_ReleasesPausesAndDiscardsRow()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        SignallingRewrite rewrite = new();
        using Fixture f = await Fixture.CreateAsync(Marketing, ct, rewrites: [rewrite]);
        await using IAsyncDisposable held = await f.Reconciler.SuspendAsync(ct);
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        Task<TeamRenameOutcome> pending = f.Service.RenameTeamAsync(Marketing, Growth, cts.Token);
        await rewrite.PausedSignal.Task.WaitAsync(ct);
        await cts.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.False(f.IsPaused(Marketing));
        Assert.False(f.IsPaused(Growth));
        Assert.Empty(f.Renames.Pending());
        Assert.Empty(f.Renames.DoneAfter(0));
        Assert.True(Directory.Exists(f.TeamFolder(Marketing)));
    }

    /// <summary>The archived folder is in use and the Teams folder cannot be moved back: the row stays Pending, Teams/New stays, the error says so, and recovery finishes it forward.</summary>
    [Fact]
    public async Task RenameTeamAsync_UndoFails_RowStaysPendingAndRecoveryFinishes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeArchiveFileSystem files = new(new PhysicalArchiveFileSystem());
        using Fixture f = await Fixture.CreateAsync(Marketing, ct, files: files);
        string archived = Path.Combine(f.ArchiveRoot, Marketing);
        Directory.CreateDirectory(archived);
        File.WriteAllText(Path.Combine(archived, "old.md"), "archived project");
        int moves = 0;
        files.OnMove = () =>
        {
            if (++moves == 2)
            {
                files.SharingViolations = int.MaxValue;
            }
        };

        TeamRenameOutcome outcome = await f.Service.RenameTeamAsync(Marketing, Growth, ct);

        Assert.False(outcome.Renamed);
        Assert.Contains("next start", outcome.Error, StringComparison.Ordinal);
        TeamRename row = Assert.Single(f.Renames.Pending());
        Assert.Equal((Marketing, Growth), (row.OldName, row.NewName));
        Assert.True(Directory.Exists(f.TeamFolder(Growth)));
        files.SharingViolations = 0;
        await f.Service.RecoverAsync(ct);
        Assert.Empty(f.Renames.Pending());
        Assert.Equal(TeamRenameState.Done, Assert.Single(f.Renames.DoneAfter(0)).State);
        Assert.Equal("archived project", File.ReadAllText(Path.Combine(f.ArchiveRoot, Growth, "old.md")));
        Assert.Equal([Growth], f.Labels("Ada"));
    }

    /// <summary>A label that will not rewrite: exactly one Feed Notification with the settled text.</summary>
    [Fact]
    public async Task RenameTeamAsync_LabelWontRewrite_PostsOneFeedNotification()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(Marketing, ct, membership: MembershipMode.NoOp);

        TeamRenameOutcome outcome = await f.Service.RenameTeamAsync(Marketing, Growth, ct);

        Assert.False(outcome.Renamed);
        FeedMessage post = Assert.Single(f.Feed.Posts);
        Assert.True(post.Notify);
        string text = Assert.IsType<TeamRenameStuckPayload>(post.Payload).Message;
        Assert.StartsWith("Renaming Marketing to Growth did not finish: ", text, StringComparison.Ordinal);
        Assert.EndsWith(". Huddle will try again at the next start.", text, StringComparison.Ordinal);
    }

    /// <summary>Recovery that leaves a row Pending posts one Feed Notification too.</summary>
    [Fact]
    public async Task RecoverAsync_RowStillPending_PostsOneFeedNotification()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(Marketing, ct, membership: MembershipMode.NoOp);
        _ = f.Renames.Begin(TeamRenameKind.Team, Marketing, Marketing, Growth);
        Directory.Move(f.TeamFolder(Marketing), f.TeamFolder(Growth));

        await f.Service.RecoverAsync(ct);

        Assert.Single(f.Renames.Pending());
        string text = Assert.IsType<TeamRenameStuckPayload>(Assert.Single(f.Feed.Posts).Payload).Message;
        Assert.StartsWith("Renaming Marketing to Growth did not finish: ", text, StringComparison.Ordinal);
        Assert.EndsWith(". Huddle will try again at the next start.", text, StringComparison.Ordinal);
    }

    /// <summary>Correction 7: the pauses are lifted first, then the reconciler suspension ends and reconciles, so the reconcile sees no pause.</summary>
    [Fact]
    public async Task RenameTeamAsync_Finish_LiftsPausesBeforeTheSuspensionReconciles()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(Marketing, ct);
        List<bool> pausedAtRead = [];
        f.Catalog.OnRead = () => pausedAtRead.Add(f.IsPaused(Marketing));

        TeamRenameOutcome outcome = await f.Service.RenameTeamAsync(Marketing, Growth, ct);

        Assert.True(outcome.Renamed);
        Assert.True(pausedAtRead.Count >= 2);
        Assert.False(pausedAtRead[^1]);
    }

    /// <summary>A <see cref="ChatException"/> from the label rewrite is retried and ends as a roll-forward error, never an exception.</summary>
    [Fact]
    public async Task RenameTeamAsync_LabelRewriteThrowsChatException_RetriedAndErrorReturned()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(Marketing, ct, membership: MembershipMode.ThrowChat);

        TeamRenameOutcome outcome = await f.Service.RenameTeamAsync(Marketing, Growth, ct);

        Assert.False(outcome.Renamed);
        Assert.False(string.IsNullOrWhiteSpace(outcome.Error));
        Assert.Equal(4, f.Membership.RenameLabelCalls);
        Assert.Single(f.Renames.Pending());
    }

    /// <summary>Marketing to Growth and back is allowed; the journal resolves Marketing to itself, and a third Team still cannot take Growth.</summary>
    [Fact]
    public async Task RenameTeamAsync_BackToFormerName_AllowedAndThirdTeamStillRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(Marketing, ct);
        Assert.True((await f.Service.RenameTeamAsync(Marketing, Growth, ct)).Renamed);

        TeamRenameOutcome back = await f.Service.RenameTeamAsync(Growth, Marketing, ct);
        TeamRenameOutcome third = await f.Service.RenameTeamAsync("Sales", Growth, ct);

        Assert.True(back.Renamed);
        Assert.Null(back.Error);
        Assert.Equal(Marketing, f.Renames.Resolve(Marketing));
        Assert.True(Directory.Exists(f.TeamFolder(Marketing)));
        Assert.False(third.Renamed);
        Assert.False(string.IsNullOrWhiteSpace(third.Error));
    }

    /// <summary>How <c>RenameLabel</c> behaves in the fixture.</summary>
    private enum MembershipMode
    {
        /// <summary>Rewrites through the real membership.</summary>
        Real,

        /// <summary>Changes nothing.</summary>
        NoOp,

        /// <summary>Throws <see cref="ChatException"/>.</summary>
        ThrowChat,
    }

    /// <summary>A rewrite that names no Team and signals once its Teams were asked for (the pauses are taken then).</summary>
    private sealed class SignallingRewrite : ITeamRenameRewrite
    {
        /// <summary>Gets the signal set when <see cref="TeamsTouched"/> ran.</summary>
        public TaskCompletionSource PausedSignal { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <inheritdoc />
        public IReadOnlyList<string> TeamsTouched(TeamRename rename)
        {
            _ = this.PausedSignal.TrySetResult();
            return [];
        }

        /// <inheritdoc />
        public Task RunAsync(TeamRename rename, CancellationToken ct) => Task.CompletedTask;
    }

    /// <summary>A catalog that runs a callback on every read of <see cref="Teams"/>.</summary>
    /// <param name="inner">The hand-driven catalog.</param>
    private sealed class ProbingCatalog(FakeTeamCatalog inner) : ITeamCatalog
    {
        /// <summary>Gets or sets the callback run on each read of <see cref="Teams"/>.</summary>
        public Action? OnRead { get; set; }

        /// <inheritdoc />
        public IReadOnlyList<TeamSummary> Teams
        {
            get
            {
                this.OnRead?.Invoke();
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

    /// <summary>A membership that counts <c>RenameLabel</c> calls, can change nothing or throw, and otherwise rewrites and flips the fake catalog.</summary>
    /// <param name="inner">The real membership.</param>
    /// <param name="catalog">The catalog that must follow the rename.</param>
    /// <param name="mode">What <c>RenameLabel</c> does.</param>
    private sealed class SwitchMembership(ITeamMembership inner, FakeTeamCatalog catalog, MembershipMode mode) : ITeamMembership
    {
        /// <summary>Gets how many times <see cref="RenameLabel"/> was called.</summary>
        public int RenameLabelCalls { get; private set; }

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
            this.RenameLabelCalls++;
            if (mode == MembershipMode.ThrowChat)
            {
                throw new ChatException("persona-store", "The Persona store refused the edit.");
            }

            if (mode == MembershipMode.NoOp)
            {
                return;
            }

            inner.RenameLabel(oldName, newName);
            catalog.Teams = [.. catalog.Teams.Select(t => string.Equals(t.Name, oldName, StringComparison.OrdinalIgnoreCase) ? t with { Name = newName } : t)];
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly TempDataDir dir;
        private readonly LibraryFileServiceFixture library;
        private readonly PersonaStore personas;
        private readonly TaskStore tasks;
        private readonly RoomEvents events = new(NullLogger<RoomEvents>.Instance);
        private int probes;

        private Fixture(TempDataDir dir, LibraryFileServiceFixture library, PersonaStore personas, TaskStore tasks, SqliteTeamDirectory directory, FakeTeamCatalog fake, TeamFolderProvisioner folders, IArchiveFileSystem files, IReadOnlyList<ITeamRenameRewrite> rewrites, MembershipMode mode)
        {
            this.dir = dir;
            this.library = library;
            this.personas = personas;
            this.tasks = tasks;
            this.Catalog = new ProbingCatalog(fake);
            this.Renames = new TeamRenameStore(dir.Options());
            this.Feed = new RecordingFeedPublisher();
            this.Membership = new SwitchMembership(new TeamMembership(personas, fake, folders, RoleFixtures.Types(dir)), fake, mode);
            ChatService chat = new(
                directory,
                dir.CreateChatStore(),
                this.events,
                new FakeMentionAliasSource { Aliases = [] },
                TestOptions.Create(),
                new ProposalStore(this.events),
                new QuestionStore(this.events),
                NullLogger<ChatService>.Instance)
            {
                TeamCatalog = this.Catalog,
            };
            this.Reconciler = new TeamRoomReconciler(directory, chat, this.Catalog, this.events, NullLogger<TeamRoomReconciler>.Instance);
            this.TeamsRoot = library.RootStore.Roots.Single(r => r.Kind == LibraryRootKind.Teams).FullPath;
            this.ArchiveRoot = library.RootStore.Roots.Single(r => r.Kind == LibraryRootKind.Archive).FullPath;
            System.IO.Directory.CreateDirectory(this.ArchiveRoot);
            this.Service = new TeamRenameService(
                library.RootStore,
                this.Catalog,
                this.Membership,
                folders,
                tasks,
                new TaskIdAllocator(dir.Options()),
                new CadenceStateStore(dir.Options()),
                personas,
                directory,
                files,
                this.Renames,
                this.Reconciler,
                rewrites,
                new TeamOperationGate(),
                NullLogger<TeamRenameService>.Instance,
                feed: this.Feed);
        }

        public TeamRenameService Service { get; }

        public TeamRenameStore Renames { get; }

        public RecordingFeedPublisher Feed { get; }

        public SwitchMembership Membership { get; }

        public ProbingCatalog Catalog { get; }

        public TeamRoomReconciler Reconciler { get; }

        public string TeamsRoot { get; }

        public string ArchiveRoot { get; }

        /// <summary>Builds the fixture: the Team and a Sales Team on disk, three members and a Room database.</summary>
        /// <param name="team">The Team to create.</param>
        /// <param name="ct">Cancels the database set-up.</param>
        /// <param name="files">The file system the service moves with; the real disk when null.</param>
        /// <param name="rewrites">The rewrites registered with the service.</param>
        /// <param name="membership">What <c>RenameLabel</c> does.</param>
        public static async Task<Fixture> CreateAsync(string team, CancellationToken ct, IArchiveFileSystem? files = null, IReadOnlyList<ITeamRenameRewrite>? rewrites = null, MembershipMode membership = MembershipMode.Real)
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
            File.WriteAllText(Path.Combine(teamsRoot, team, "notes.md"), "# Notes\n");
            System.IO.Directory.CreateDirectory(Path.Combine(teamsRoot, "Sales"));
            catalog.Teams =
            [
                new TeamSummary(team, [], ["Ada", "Nova"], HasFolder: true) { Manager = "Ada" },
                new TeamSummary("Sales", [], [], HasFolder: true),
            ];
            SqliteTeamDirectory directory = new(dir.Options());
            await directory.InitializeAsync("You", ct);
            foreach (string agent in new[] { "Ada", "Nova" })
            {
                _ = await directory.UpsertAgentUserAsync(agent, null, ct);
            }

            TaskStore tasks = TestTaskStore.CreateTaskStore(dir, personas);
            return new Fixture(dir, library, personas, tasks, directory, catalog, folders, files ?? new PhysicalArchiveFileSystem(), rewrites ?? [], membership);
        }

        /// <summary>The path <c>Teams/&lt;name&gt;</c>.</summary>
        /// <param name="name">The folder name.</param>
        public string TeamFolder(string name) => Path.Combine(this.TeamsRoot, name);

        /// <summary>Respells a folder through a scratch name, as a case-only rename does on a case-insensitive disk.</summary>
        /// <param name="parent">The folder that holds it.</param>
        /// <param name="from">The current name.</param>
        /// <param name="to">The new spelling.</param>
        public static void Respell(string parent, string from, string to)
        {
            string scratch = Path.Combine(parent, ".scratch-respell");
            System.IO.Directory.Move(Path.Combine(parent, from), scratch);
            System.IO.Directory.Move(scratch, Path.Combine(parent, to));
        }

        /// <summary>The Team labels in the named Persona's definition, as the store holds them now.</summary>
        /// <param name="name">The Persona's name.</param>
        public IReadOnlyList<string> Labels(string name)
        {
            Persona? persona = this.personas.Get(name);
            Assert.NotNull(persona);
            Assert.True(PersonaFrontmatter.TryReadIdentity(persona.Text, out PersonaIdentity? identity, out _));
            return identity.Teams;
        }

        /// <summary>Tells whether a Task write for the Team is refused now, by trying one.</summary>
        /// <param name="teamName">The Team.</param>
        public bool IsPaused(string teamName)
        {
            string id = string.Create(CultureInfo.InvariantCulture, $"PROBE-{++this.probes:0000}");
            TaskItem candidate = TestTasks.Make(id: id, location: new TaskLocation(teamName, null, false), path: string.Empty);
            try
            {
                _ = this.tasks.Create(candidate, TaskFileFormat.Compose(candidate));
                return false;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
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
