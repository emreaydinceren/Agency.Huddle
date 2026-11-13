using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Agents;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.SecondBrain;
using Agency.Huddle.Tests.Agents;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.Library;
using Agency.Huddle.Tests.Tasks;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// Pins two review fixes in <see cref="TeamRenameService"/> (batch 3): a cancellation that arrives at the Room line never fails a rename
/// that is already Done, and a rename does not start while another row is still Pending.
/// </summary>
public sealed class TeamRenameBatch3FixTests
{
    /// <summary>A Prompt source that cancels the caller's token and then throws, as a post cancelled mid-flight does.</summary>
    /// <param name="cts">The source cancelled when the line is rendered.</param>
    private sealed class CancellingPrompts(CancellationTokenSource cts) : IPromptSource
    {
        /// <inheritdoc />
        public string Render(string key, IReadOnlyDictionary<string, string> values)
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        }

        /// <inheritdoc />
        public string Raw(string key) => string.Empty;

        /// <inheritdoc />
        public bool IsOverridden(string key) => false;
    }

    private sealed class Rig : IDisposable
    {
        private readonly TempDataDir dir;
        private readonly LibraryFileServiceFixture library;
        private readonly TaskStore tasks;
        private readonly TeamRoomReconciler reconciler;

        private Rig(TempDataDir dir, LibraryFileServiceFixture library, TaskStore tasks, TeamRoomReconciler reconciler, TeamRenameStore renames, TeamRenameService service)
        {
            this.dir = dir;
            this.library = library;
            this.tasks = tasks;
            this.reconciler = reconciler;
            this.Renames = renames;
            this.Service = service;
        }

        public TeamRenameStore Renames { get; }

        public TeamRenameService Service { get; }

        public static async Task<Rig> CreateAsync(CancellationTokenSource cts, bool cancelAtPost, CancellationToken ct)
        {
            TempDataDir dir = new();
            LibraryFileServiceFixture library = LibraryFileServiceFixture.Attach(dir.Path);
            PersonaStore personas = dir.CreatePersonaStore();
            FakeTeamCatalog catalog = new();
            TeamFolderProvisioner folders = new(personas, library.RootStore, library.Resolver, NullLogger<TeamFolderProvisioner>.Instance, catalog);
            TeamFile file = new(new VersionedRef("web", "1.2.0"), "Ada", null, null, string.Empty);
            Assert.Equal(TeamCreateOutcome.Created, folders.CreateTeam("Marketing", file).Outcome);
            _ = personas.Add(new PersonaIdentity("Ada", "Lead", "ada", ["Marketing"]), "Leads.");
            catalog.Teams = [new TeamSummary("Marketing", ["Alpha"], ["Ada"], HasFolder: true) { Manager = "Ada" }];
            SqliteTeamDirectory directory = new(dir.Options());
            await directory.InitializeAsync("You", ct);
            _ = await directory.UpsertAgentUserAsync("Ada", null, ct);
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
            TeamRoomReconciler reconciler = new(directory, chat, catalog, events, NullLogger<TeamRoomReconciler>.Instance);
            await reconciler.ReconcileAsync(ct);
            System.IO.Directory.CreateDirectory(library.RootStore.Roots.Single(r => r.Kind == LibraryRootKind.Archive).FullPath);
            TaskStore tasks = TestTaskStore.CreateTaskStore(dir, personas);
            TeamRenameStore renames = new(dir.Options());
            TeamRenameService service = new(
                library.RootStore,
                catalog,
                new FollowingMembership(new TeamMembership(personas, catalog, folders, RoleFixtures.Types(dir)), catalog),
                folders,
                tasks,
                new TaskIdAllocator(dir.Options()),
                new CadenceStateStore(dir.Options()),
                personas,
                directory,
                new PhysicalArchiveFileSystem(),
                renames,
                reconciler,
                [],
                new TeamOperationGate(),
                NullLogger<TeamRenameService>.Instance,
                chat: chat,
                prompts: cancelAtPost ? new CancellingPrompts(cts) : new EmptyPrompts());
            return new Rig(dir, library, tasks, reconciler, renames, service);
        }

        public void Dispose()
        {
            this.reconciler.Dispose();
            this.tasks.Dispose();
            this.library.Dispose();
            this.dir.Dispose();
        }
    }

    private sealed class EmptyPrompts : IPromptSource
    {
        /// <inheritdoc />
        public string Render(string key, IReadOnlyDictionary<string, string> values) => "renamed";

        /// <inheritdoc />
        public string Raw(string key) => string.Empty;

        /// <inheritdoc />
        public bool IsOverridden(string key) => false;
    }

    /// <summary>The label rewrite the rename calls, applied to the fake catalog so the reconciler sees the new name.</summary>
    /// <param name="inner">The real membership.</param>
    /// <param name="catalog">The catalog that follows the rename.</param>
    private sealed class FollowingMembership(ITeamMembership inner, FakeTeamCatalog catalog) : ITeamMembership
    {
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
            inner.RenameLabel(oldName, newName);
            catalog.Teams = [.. catalog.Teams.Select(t => string.Equals(t.Name, oldName, StringComparison.OrdinalIgnoreCase) ? t with { Name = newName } : t)];
        }
    }

    /// <summary>A cancellation that arrives while the Room line is posted leaves the rename reported as done.</summary>
    [Fact]
    public async Task RenameTeamAsync_CancelledAtTheRoomLine_StillRenamed()
    {
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using Rig rig = await Rig.CreateAsync(cts, cancelAtPost: true, TestContext.Current.CancellationToken);

        TeamRenameOutcome outcome = await rig.Service.RenameTeamAsync("Marketing", "Growth", cts.Token);

        Assert.True(outcome.Renamed);
        Assert.Equal(TeamRenameState.Done, Assert.Single(rig.Renames.DoneAfter(0)).State);
    }

    /// <summary>A rename is refused while another rename row is Pending, so the Turn hook's highest-id rule cannot miss one.</summary>
    [Fact]
    public async Task RenameTeamAsync_AnotherRowPending_IsRefused()
    {
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using Rig rig = await Rig.CreateAsync(cts, cancelAtPost: false, TestContext.Current.CancellationToken);
        _ = rig.Renames.Begin(TeamRenameKind.Team, "Other", "Other", "Another");

        TeamRenameOutcome outcome = await rig.Service.RenameTeamAsync("Marketing", "Growth", cts.Token);

        Assert.False(outcome.Renamed);
        Assert.Equal("Another rename is still finishing. Try again after Huddle restarts.", outcome.Error);
    }

    /// <summary>A Project rename is refused while any other rename row is Pending, with the same sentence a Team rename gets.</summary>
    [Fact]
    public async Task RenameProjectAsync_AnotherRowPending_IsRefused()
    {
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using Rig rig = await Rig.CreateAsync(cts, cancelAtPost: false, TestContext.Current.CancellationToken);
        _ = rig.Renames.Begin(TeamRenameKind.Team, "Other", "Other", "Another");

        TeamRenameOutcome outcome = await rig.Service.RenameProjectAsync("Marketing", "Alpha", "Beta", cts.Token);

        Assert.False(outcome.Renamed);
        Assert.Equal("Another rename is still finishing. Try again after Huddle restarts.", outcome.Error);
    }
}
