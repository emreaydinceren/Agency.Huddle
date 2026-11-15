using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.AgentsMdChanges;
using Agency.Huddle.App.Agents;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.DocHistory;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Helpers;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.SecondBrain;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Agents;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.Library;
using Agency.Huddle.Tests.Tasks;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// Pins <see cref="TeamRenameService.RenameProjectAsync"/> (Team Rename Spec R12, §5.5 Project rule, §6.4 Project line,
/// corrections-batch3 tr11 items 1, 2, 4, 5, 7, 8): the folder move, the Room, View and Task-file rewrites, the refusals,
/// the case-only rename, a move that fails, recovery from disk and the Team Room line. The fixture is a real folder provisioner,
/// Task store, directory, journal and Chat service over a temp data directory, with the real Views and Task-file rewrites.
/// </summary>
public sealed class TeamRenameProjectTests
{
    private const string Growth = "Growth";
    private const string Launch = "Launch";
    private const string Launch2027 = "Launch-2027";
    private const string Line = "Project **Launch** was renamed to **Launch-2027**.";

    /// <summary>R12: the folder moves with its Tasks, Rooms and Views follow, the row is Done, no Persona file changes and the Team Room gets exactly the rendered line.</summary>
    [Fact]
    public async Task RenameProjectAsync_Launch_MovesFolderRewritesRoomsAndViewsAndPostsTheLine()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(ct);
        Room teamRoom = await f.CreateTeamRoomAsync(ct);
        Room projectRoom = await f.Directory.CreateRoomAsync("Launch room", [KnownIds.Human], Growth, false, Launch, ct);
        byte[] taskBefore = File.ReadAllBytes(f.TaskPath(Launch));
        int personaChanges = 0;
        f.Personas.PersonasChanged += () => personaChanges++;

        TeamRenameOutcome outcome = await f.Service.RenameProjectAsync(Growth, Launch, Launch2027, ct);

        Assert.True(outcome.Renamed);
        Assert.Null(outcome.Error);
        Assert.False(Directory.Exists(f.ProjectFolder(Launch)));
        Assert.Equal(taskBefore, File.ReadAllBytes(f.TaskPath(Launch2027)));
        Assert.Equal(Launch2027, (await f.Directory.GetRoomsForTeamAsync(Growth, ct)).Single(r => r.Id == projectRoom.Id).Project);
        TaskView? view = f.Views.Get("v1");
        Assert.NotNull(view);
        Assert.Equal([new ProjectRef(Growth, Launch2027), new ProjectRef("Sales", Launch)], view.Filter.Projects);
        TeamRename row = Assert.Single(f.Renames.DoneAfter(0));
        Assert.Equal((TeamRenameKind.Project, Growth, Launch, Launch2027, TeamRenameState.Done), (row.Kind, row.Team, row.OldName, row.NewName, row.State));
        Assert.Empty(f.Renames.Pending());
        Assert.Equal(0, personaChanges);
        ChatMessage last = (await f.Transcript(teamRoom.Id, ct))[^1];
        Assert.Equal(KnownIds.Huddle, last.SenderId);
        Assert.Equal(Line, last.Text);
    }

    /// <summary>§5.5: a name that exists in the Team (in any case), is archived, is reserved by an earlier rename, is memory, raw or outputs, or starts with an underscore is refused and nothing changes.</summary>
    /// <param name="newName">The refused name.</param>
    [Theory]
    [InlineData("Beta")]
    [InlineData("bETA")]
    [InlineData("Old")]
    [InlineData("old")]
    [InlineData("Gone")]
    [InlineData("memory")]
    [InlineData("Raw")]
    [InlineData("OUTPUTS")]
    [InlineData("_hidden")]
    public async Task RenameProjectAsync_RefusedName_NothingChanged(string newName)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(ct);
        long earlier = f.Renames.Begin(TeamRenameKind.Project, Growth, "Gone", "Elsewhere");
        f.Renames.Complete(earlier);
        Dictionary<string, string> before = Fixture.Snapshot(f.TeamFolder);

        TeamRenameOutcome outcome = await f.Service.RenameProjectAsync(Growth, Launch, newName, ct);

        Assert.False(outcome.Renamed);
        Assert.False(string.IsNullOrWhiteSpace(outcome.Error));
        Assert.Equal(before, Fixture.Snapshot(f.TeamFolder));
        Assert.Empty(f.Renames.Pending());
        Assert.Single(f.Renames.DoneAfter(0));
    }

    /// <summary>The UI's live check gives exactly the refusal <c>RenameProjectAsync</c> gives for a live Project of the Team (any case), an archived one, a reserved name, a reserved folder name or an underscore.</summary>
    /// <param name="newName">The refused name.</param>
    [Theory]
    [InlineData("Beta")]
    [InlineData("bETA")]
    [InlineData("Old")]
    [InlineData("old")]
    [InlineData("Gone")]
    [InlineData("memory")]
    [InlineData("_hidden")]
    [InlineData(Launch)]
    public async Task CheckProjectName_RefusedName_SameSentenceAsRenameProjectAsync(string newName)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(ct);
        long earlier = f.Renames.Begin(TeamRenameKind.Project, Growth, "Gone", "Elsewhere");
        f.Renames.Complete(earlier);

        string? check = f.Service.CheckProjectName(Growth, Launch, newName);
        TeamRenameOutcome outcome = await f.Service.RenameProjectAsync(Growth, Launch, newName, ct);

        Assert.False(string.IsNullOrWhiteSpace(check));
        Assert.Equal(outcome.Error, check);
    }

    /// <summary>The live check for a Project the Team does not have names the Team and the Project, as the rename does.</summary>
    [Fact]
    public async Task CheckProjectName_UnknownProject_SameSentenceAsRenameProjectAsync()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(ct);

        string? check = f.Service.CheckProjectName(Growth, "Nowhere", Launch2027);
        TeamRenameOutcome outcome = await f.Service.RenameProjectAsync(Growth, "Nowhere", Launch2027, ct);

        Assert.Equal($"{Growth} has no Project named \"Nowhere\".", check);
        Assert.Equal(outcome.Error, check);
    }

    /// <summary>The live check refuses while any rename row is Pending, as the rename does.</summary>
    [Fact]
    public async Task CheckProjectName_AnyRowPending_Refused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(ct);
        _ = f.Renames.Begin(TeamRenameKind.Team, "Alpha", "Alpha", "Beta2");

        string? check = f.Service.CheckProjectName(Growth, Launch, Launch2027);
        TeamRenameOutcome outcome = await f.Service.RenameProjectAsync(Growth, Launch, Launch2027, ct);

        Assert.False(string.IsNullOrWhiteSpace(check));
        Assert.Equal(outcome.Error, check);
    }

    /// <summary>A fresh name and a case-only change are acceptable, so the check returns no problem.</summary>
    [Fact]
    public async Task CheckProjectName_FreshNameAndCaseOnly_ReturnNull()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(ct);

        Assert.Null(f.Service.CheckProjectName(Growth, Launch, Launch2027));
        Assert.Null(f.Service.CheckProjectName(Growth, Launch, "launch"));
    }

    /// <summary>§5.5: Launch to launch succeeds through a temporary name, leaves no temporary folder and journals one Done row.</summary>
    [Fact]
    public async Task RenameProjectAsync_CaseOnly_SucceedsViaTempAndLeavesNoTemp()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(ct);

        TeamRenameOutcome outcome = await f.Service.RenameProjectAsync(Growth, Launch, "launch", ct);

        Assert.True(outcome.Renamed);
        Assert.Contains("launch", Directory.GetDirectories(f.TeamFolder).Select(static p => Path.GetFileName(p)));
        Assert.DoesNotContain(Launch, Directory.GetDirectories(f.TeamFolder).Select(static p => Path.GetFileName(p)));
        Assert.DoesNotContain(
            Directory.GetDirectories(f.TeamFolder).Select(static p => Path.GetFileName(p)),
            static n => n.StartsWith(TeamRenameService.RenamingPrefix, StringComparison.Ordinal));
        Assert.Equal(TeamRenameState.Done, Assert.Single(f.Renames.DoneAfter(0)).State);
    }

    /// <summary>R12 attachments: another Team's Task citing the Project folder is rewritten with one change-log line; a sibling Project whose name starts the same is untouched.</summary>
    [Fact]
    public async Task RenameProjectAsync_OtherTeamsTaskCitesProjectFolder_RewrittenOnceAndSiblingUntouched()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(ct);
        string cited = Path.Combine(f.TeamsRoot, Growth, Launch, "x.md");
        string sibling = Path.Combine(f.TeamsRoot, Growth, "Launch2", "y.md");
        string path = f.WriteResearchTask($"See `{cited}` and `{sibling}`.");

        TeamRenameOutcome outcome = await f.Service.RenameProjectAsync(Growth, Launch, Launch2027, ct);

        Assert.True(outcome.Renamed);
        string text = await File.ReadAllTextAsync(path, ct);
        Assert.Contains($"`{Path.Combine(f.TeamsRoot, Growth, Launch2027, "x.md")}`", text, StringComparison.Ordinal);
        Assert.Contains($"`{sibling}`", text, StringComparison.Ordinal);
        Assert.Single(text.Split('\n'), static line => line.TrimEnd('\r').Contains("| Huddle | event | Project renamed from Launch to Launch-2027", StringComparison.Ordinal));
    }

    /// <summary>A move that fails (the destination folder is in use): Renamed is false, the Project folder and the Room are as they were and the journal is empty.</summary>
    [Fact]
    public async Task RenameProjectAsync_FolderInUse_NothingChangedJournalEmpty()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeArchiveFileSystem files = new(new PhysicalArchiveFileSystem()) { InUseFor = Launch2027 };
        using Fixture f = await Fixture.CreateAsync(ct, files);
        Room projectRoom = await f.Directory.CreateRoomAsync("Launch room", [KnownIds.Human], Growth, false, Launch, ct);

        TeamRenameOutcome outcome = await f.Service.RenameProjectAsync(Growth, Launch, Launch2027, ct);

        Assert.False(outcome.Renamed);
        Assert.False(string.IsNullOrWhiteSpace(outcome.Error));
        Assert.True(File.Exists(f.TaskPath(Launch)));
        Assert.False(Directory.Exists(f.ProjectFolder(Launch2027)));
        Assert.Equal(Launch, (await f.Directory.GetRoomsForTeamAsync(Growth, ct)).Single(r => r.Id == projectRoom.Id).Project);
        Assert.Empty(f.Renames.Pending());
        Assert.Empty(f.Renames.DoneAfter(0));
    }

    /// <summary>§5.4: the folder moved and the Room rows did not; RecoverAsync rolls the Project row forward, completes it and posts the line.</summary>
    [Fact]
    public async Task RecoverAsync_ProjectFolderMovedRowsNot_CompletesTheRename()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(ct);
        Room teamRoom = await f.CreateTeamRoomAsync(ct);
        Room projectRoom = await f.Directory.CreateRoomAsync("Launch room", [KnownIds.Human], Growth, false, Launch, ct);
        _ = f.Renames.Begin(TeamRenameKind.Project, Growth, Launch, Launch2027);
        Directory.Move(f.ProjectFolder(Launch), f.ProjectFolder(Launch2027));

        await f.Service.RecoverAsync(ct);

        Assert.Equal(Launch2027, (await f.Directory.GetRoomsForTeamAsync(Growth, ct)).Single(r => r.Id == projectRoom.Id).Project);
        Assert.Equal([new ProjectRef(Growth, Launch2027), new ProjectRef("Sales", Launch)], f.Views.Get("v1")?.Filter.Projects);
        Assert.Empty(f.Renames.Pending());
        Assert.Equal(TeamRenameState.Done, Assert.Single(f.Renames.DoneAfter(0)).State);
        Assert.Equal(Line, (await f.Transcript(teamRoom.Id, ct))[^1].Text);
    }

    /// <summary>Tr11 item 5: while the rewrites run, Task writes of the Team are paused; once the rename is done they are not.</summary>
    [Fact]
    public async Task RenameProjectAsync_RewriteSeam_TeamIsPausedThenReleased()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(ct);
        bool? pausedDuring = null;
        f.Probe.OnRun = () => pausedDuring = f.IsPaused(Growth);

        TeamRenameOutcome outcome = await f.Service.RenameProjectAsync(Growth, Launch, Launch2027, ct);

        Assert.True(outcome.Renamed);
        Assert.True(pausedDuring);
        Assert.False(f.IsPaused(Growth));
    }

    /// <summary>Tr11 item 5: the AGENTS.md ledger entry, the raw baseline row and the doc-history of the Project folder are found under the new name and not under the old; a sibling Project is not moved.</summary>
    [Fact]
    public async Task RenameProjectAsync_Ledgers_FollowTheProjectFolder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(ct);
        f.ObserveAgentsMd("Growth/Launch/AGENTS.md");
        f.ObserveAgentsMd("Growth/Launch2/AGENTS.md");
        string? hash = f.Ledger.FirstSightHash("Growth/Launch/AGENTS.md");
        f.Baselines.Record("Teams/Growth/Launch/x.md", 5, DateTimeOffset.UnixEpoch);
        f.Baselines.Record("Teams/Growth/Launch2/y.md", 5, DateTimeOffset.UnixEpoch);
        f.SeedHistory("teams", Growth, Launch, "x.md.jsonl");
        f.SeedHistory("teams", Growth, "Launch2", "y.md.jsonl");

        TeamRenameOutcome outcome = await f.Service.RenameProjectAsync(Growth, Launch, Launch2027, ct);

        Assert.True(outcome.Renamed);
        Assert.NotNull(hash);
        Assert.Equal(hash, f.Ledger.FirstSightHash("Growth/Launch-2027/AGENTS.md"));
        Assert.Null(f.Ledger.FirstSightHash("Growth/Launch/AGENTS.md"));
        Assert.NotNull(f.Ledger.FirstSightHash("Growth/Launch2/AGENTS.md"));
        Assert.True(f.Baselines.TryGet("Teams/Growth/Launch-2027/x.md", out _));
        Assert.False(f.Baselines.TryGet("Teams/Growth/Launch/x.md", out _));
        Assert.True(f.Baselines.TryGet("Teams/Growth/Launch2/y.md", out _));
        Assert.True(File.Exists(f.HistoryFile("teams", Growth, Launch2027, "x.md.jsonl")));
        Assert.False(File.Exists(f.HistoryFile("teams", Growth, Launch, "x.md.jsonl")));
        Assert.True(File.Exists(f.HistoryFile("teams", Growth, "Launch2", "y.md.jsonl")));
    }

    /// <summary>Tr11 item 5: an Agent's Subscribed path under the Project moves to the new name; its <c>team:Growth</c> key is unchanged, and so are a sibling Project's and another Team's paths.</summary>
    [Fact]
    public async Task RenameProjectAsync_FileState_SubscribedPathMovesAndTeamKeyStays()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(ct);
        DateTimeOffset stamp = new(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);
        FolderSnapshot snapshot = new(new Dictionary<string, FileEntry>(FolderSnapshot.PathComparer) { ["plan.md"] = new FileEntry(1, stamp) });
        f.Files.Save(
            "Ada",
            new FileState(
                ["Teams/Growth/Launch/docs", "Teams/Growth/Launch2/docs", "Teams/Sales/Launch"],
                new Dictionary<string, RoomBaseline>(StringComparer.Ordinal)
                {
                    ["room-1"] = new RoomBaseline(new Dictionary<string, FolderSnapshot>(StringComparer.OrdinalIgnoreCase) { ["team:Growth"] = snapshot }),
                },
                new Dictionary<string, IReadOnlyDictionary<string, FileWriter>>(StringComparer.OrdinalIgnoreCase)));

        TeamRenameOutcome outcome = await f.Service.RenameProjectAsync(Growth, Launch, Launch2027, ct);

        Assert.True(outcome.Renamed);
        FileState? state = f.Files.Load("Ada");
        Assert.NotNull(state);
        Assert.Equal(["Teams/Growth/Launch-2027/docs", "Teams/Growth/Launch2/docs", "Teams/Sales/Launch"], state.Subscribed);
        Assert.True(state.Rooms["room-1"].Folders.ContainsKey("team:Growth"));
    }

    /// <summary>A rewrite that runs a callback, so a test can look at the world while the rewrites run.</summary>
    private sealed class ProbeRewrite : ITeamRenameRewrite
    {
        /// <summary>Gets or sets the callback run by <see cref="RunAsync"/>.</summary>
        public Action? OnRun { get; set; }

        /// <inheritdoc />
        public IReadOnlyList<string> TeamsTouched(TeamRename rename) => [];

        /// <inheritdoc />
        public Task RunAsync(TeamRename rename, CancellationToken ct)
        {
            this.OnRun?.Invoke();
            return Task.CompletedTask;
        }
    }

    /// <summary>The prompt text rendered from the catalog default, as the real source does.</summary>
    private sealed class DefaultPrompts : IPromptSource
    {
        /// <inheritdoc />
        public string Render(string key, IReadOnlyDictionary<string, string> values) => PromptRenderer.Render(this.Raw(key), values);

        /// <inheritdoc />
        public string Raw(string key) => PromptCatalog.All.Single(definition => definition.Key == key).Default;

        /// <inheritdoc />
        public bool IsOverridden(string key) => false;
    }

    private sealed class Fixture : IDisposable
    {
        private readonly TempDataDir dir;
        private readonly LibraryFileServiceFixture library;
        private readonly TaskStore tasks;
        private int probes;
        private readonly RoomEvents events = new(NullLogger<RoomEvents>.Instance);

        private Fixture(TempDataDir dir, LibraryFileServiceFixture library, PersonaStore personas, TaskStore tasks, SqliteTeamDirectory directory, FakeTeamCatalog catalog, TeamFolderProvisioner folders, ViewStore views, IArchiveFileSystem files)
        {
            this.dir = dir;
            this.library = library;
            this.Personas = personas;
            this.tasks = tasks;
            this.Directory = directory;
            this.Views = views;
            this.Renames = new TeamRenameStore(dir.Options());
            HelperSettingsResolver settings = new(new HelperSettingsStore(dir.Options()), dir.Options(), NullLogger<HelperSettingsResolver>.Instance);
            this.Ledger = new AgentsMdLedger(
                static () => throw new InvalidOperationException("The dispatcher must not be reached."),
                static () => throw new InvalidOperationException("The Helper must not be reached."),
                settings,
                new HelperBudget(new HelperUsageStore(dir.Options())),
                dir.Options(),
                NullLogger<AgentsMdLedger>.Instance);
            this.Files = new FileStateStore(dir.Options(), NullLogger<FileStateStore>.Instance);
            this.Baselines = new RawBaselineStore(dir.Options());
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
                TeamCatalog = catalog,
            };
            this.Reconciler = new TeamRoomReconciler(directory, chat, catalog, this.events, NullLogger<TeamRoomReconciler>.Instance);
            this.TeamsRoot = library.RootStore.Roots.Single(r => r.Kind == LibraryRootKind.Teams).FullPath;
            System.IO.Directory.CreateDirectory(library.RootStore.Roots.Single(r => r.Kind == LibraryRootKind.Archive).FullPath);
            this.Service = new TeamRenameService(
                library.RootStore,
                catalog,
                new TeamMembership(personas, catalog, folders, RoleFixtures.Types(dir)),
                folders,
                tasks,
                new TaskIdAllocator(dir.Options()),
                new CadenceStateStore(dir.Options()),
                personas,
                directory,
                files,
                this.Renames,
                this.Reconciler,
                [
                    new ViewsRenameRewrite(views),
                    new TaskFileRenameRewrite(dir.Options()),
                    new LedgerRenameRewrite(this.Ledger, this.Files, this.Baselines, dir.Options()),
                    new DocHistoryRenameRewrite(new DocHistoryStore(dir.Options(), NullLogger<DocHistoryStore>.Instance)),
                    this.Probe,
                ],
                new TeamOperationGate(),
                NullLogger<TeamRenameService>.Instance,
                chat: chat,
                prompts: new DefaultPrompts());
        }

        public TeamRenameService Service { get; }

        public TeamRenameStore Renames { get; }

        public SqliteTeamDirectory Directory { get; }

        public PersonaStore Personas { get; }

        public ViewStore Views { get; }

        public AgentsMdLedger Ledger { get; }

        public FileStateStore Files { get; }

        public RawBaselineStore Baselines { get; }

        public ProbeRewrite Probe { get; } = new();

        public TeamRoomReconciler Reconciler { get; }

        public string TeamsRoot { get; }

        public string TeamFolder => Path.Combine(this.TeamsRoot, Growth);

        /// <summary>Builds the fixture: Team Growth with Projects Launch (one Task), Beta and an archived Old, a Sales Team, one View, a Room database.</summary>
        /// <param name="ct">Cancels the database set-up.</param>
        /// <param name="files">The file system the service moves with; the real disk when null.</param>
        public static async Task<Fixture> CreateAsync(CancellationToken ct, IArchiveFileSystem? files = null)
        {
            TempDataDir dir = new();
            LibraryFileServiceFixture library = LibraryFileServiceFixture.Attach(dir.Path);
            PersonaStore personas = dir.CreatePersonaStore();
            FakeTeamCatalog catalog = new();
            TeamFolderProvisioner folders = new(personas, library.RootStore, library.Resolver, NullLogger<TeamFolderProvisioner>.Instance, catalog);
            string teamsRoot = library.RootStore.Roots.Single(r => r.Kind == LibraryRootKind.Teams).FullPath;
            string archiveRoot = library.RootStore.Roots.Single(r => r.Kind == LibraryRootKind.Archive).FullPath;
            TeamFile file = new(new VersionedRef("web", "1.2.0"), "Ada", null, null, string.Empty);
            Assert.Equal(TeamCreateOutcome.Created, folders.CreateTeam(Growth, file).Outcome);
            _ = personas.Add(new PersonaIdentity("Ada", "Lead", "ada", [Growth]), "Leads.");
            foreach (string project in new[] { Launch, "Beta" })
            {
                System.IO.Directory.CreateDirectory(Path.Combine(teamsRoot, Growth, project));
            }

            System.IO.Directory.CreateDirectory(Path.Combine(archiveRoot, Growth, "Old"));
            System.IO.Directory.CreateDirectory(Path.Combine(teamsRoot, "Research"));
            File.WriteAllText(Path.Combine(teamsRoot, Growth, Launch, "notes.md"), "# Notes\n");
            catalog.Teams =
            [
                new TeamSummary(Growth, [Launch, "Beta"], ["Ada"], HasFolder: true) { Manager = "Ada" },
                new TeamSummary("Research", [], [], HasFolder: true),
            ];
            SqliteTeamDirectory directory = new(dir.Options());
            await directory.InitializeAsync("You", ct);
            _ = await directory.UpsertAgentUserAsync("Ada", null, ct);
            TaskItem task = TestTasks.Make(id: "GROW-0001", location: new TaskLocation(Growth, Launch, false), path: string.Empty);
            _ = TestTaskStore.WriteTask(TestTaskStore.Root(dir), Path.Combine(Growth, Launch, "_tasks", "GROW-0001.md"), task);
            ViewStore views = new(dir.Options(), NullLogger<ViewStore>.Instance);
            _ = views.Save(new TaskView
            {
                Id = "v1",
                Name = "v1",
                Kind = ViewKind.List,
                Filter = new TaskFilter { Teams = [Growth], Projects = [new ProjectRef(Growth, Launch), new ProjectRef("Sales", Launch)] },
            });
            TaskStore tasks = TestTaskStore.CreateTaskStore(dir, personas);
            return new Fixture(dir, library, personas, tasks, directory, catalog, folders, views, files ?? new PhysicalArchiveFileSystem());
        }

        /// <summary>Lists every folder and file under <paramref name="root"/>, keyed by forward-slash relative path; a file maps to its hex bytes.</summary>
        /// <param name="root">The folder to list.</param>
        public static Dictionary<string, string> Snapshot(string root)
        {
            Dictionary<string, string> entries = new(StringComparer.Ordinal);
            foreach (string path in System.IO.Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                entries[relative] = System.IO.Directory.Exists(path) ? "<dir>" : Convert.ToHexString(File.ReadAllBytes(path));
            }

            return entries;
        }

        /// <summary>The path <c>Teams/Growth/&lt;project&gt;</c>.</summary>
        /// <param name="project">The Project folder name.</param>
        public string ProjectFolder(string project) => Path.Combine(this.TeamsRoot, Growth, project);

        /// <summary>The path of the seeded Task file inside the Project folder.</summary>
        /// <param name="project">The Project folder name.</param>
        public string TaskPath(string project) => Path.Combine(this.ProjectFolder(project), "_tasks", "GROW-0001.md");

        /// <summary>Writes a Research Task whose description is <paramref name="description"/> and returns its path.</summary>
        /// <param name="description">The Task description.</param>
        public string WriteResearchTask(string description)
        {
            TaskItem task = TestTasks.Make(id: "RESE-0001", location: new TaskLocation("Research", null, false), description: description, path: string.Empty);
            return TestTaskStore.WriteTask(TestTaskStore.Root(this.dir), Path.Combine("Research", "_tasks", "RESE-0001.md"), task);
        }

        /// <summary>Writes an AGENTS.md under the Teams folder and records it in the ledger.</summary>
        /// <param name="relativePath">The path relative to the Teams folder, with <c>/</c> separators.</param>
        public void ObserveAgentsMd(string relativePath)
        {
            string full = Path.Combine(this.TeamsRoot, relativePath);
            _ = System.IO.Directory.CreateDirectory(Path.GetDirectoryName(full) ?? this.TeamsRoot);
            File.WriteAllText(full, "Lead with the number.\n");
            _ = this.Ledger.Observe(new AgentsMdFile(AgentsMdLevel.Team, Growth, null, full, relativePath));
        }

        /// <summary>Writes a placeholder doc-history file under the data directory's <c>doc-history</c> folder.</summary>
        /// <param name="segments">The path segments below <c>doc-history</c>.</param>
        public void SeedHistory(params string[] segments)
        {
            string file = this.HistoryFile(segments);
            _ = System.IO.Directory.CreateDirectory(Path.GetDirectoryName(file) ?? this.dir.Path);
            File.WriteAllText(file, "{}\n");
        }

        /// <summary>The path of a doc-history file.</summary>
        /// <param name="segments">The path segments below <c>doc-history</c>.</param>
        public string HistoryFile(params string[] segments) => Path.Combine([this.dir.Path, "doc-history", .. segments]);

        /// <summary>Tells whether a Task write for the Team is refused now, by trying one.</summary>
        /// <param name="team">The Team.</param>
        public bool IsPaused(string team)
        {
            string id = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"PROBE-{++this.probes:0000}");
            TaskItem candidate = TestTasks.Make(id: id, location: new TaskLocation(team, null, false), path: string.Empty);
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

        /// <summary>Creates the Team Room through the reconciler, as the running app does.</summary>
        /// <param name="ct">Cancels the database work.</param>
        public async Task<Room> CreateTeamRoomAsync(CancellationToken ct)
        {
            await this.Reconciler.ReconcileAsync(ct);
            Room? room = await this.Directory.FindTeamRoomAsync(Growth, ct);
            Assert.NotNull(room);
            return room;
        }

        /// <summary>The Room's Messages, oldest first.</summary>
        /// <param name="roomId">The Room.</param>
        /// <param name="ct">Cancels the read.</param>
        public Task<IReadOnlyList<ChatMessage>> Transcript(string roomId, CancellationToken ct) => this.dir.CreateChatStore().ReadAllAsync(roomId, ct);

        /// <inheritdoc />
        public void Dispose()
        {
            this.Reconciler.Dispose();
            this.tasks.Dispose();
            this.Views.Dispose();
            this.library.Dispose();
            this.dir.Dispose();
        }
    }
}
