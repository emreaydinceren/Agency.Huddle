using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Agents;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.SecondBrain;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Agents;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.Feed;
using Agency.Huddle.Tests.Library;
using Agency.Huddle.Tests.Tasks;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// Pins step 8 of the Team rename (Team Rename Spec R8, §6.4, corrections-batch3 tr10 item 2): the Team Room line
/// <c>room.teamRenamed</c> that Huddle posts after the journal row is Done and the Task writes are unpaused, by a rename and by
/// recovery, and the dedicated Feed source <c>team-rename-stuck</c> for a rename that did not finish. The fixture is a small copy
/// of the one in <c>TeamRenameReviewFixTests</c> with a real Chat service whose directory a test can swap for an empty one.
/// </summary>
public sealed class TeamRenameRoomLineTests
{
    private const string Marketing = "Marketing";
    private const string Growth = "Growth";
    private const string Line = "This Team was renamed from **Marketing** to **Growth**.";

    /// <summary>R8: the Team Room's last Message is from Huddle and reads exactly as the Prompt renders.</summary>
    [Fact]
    public async Task RenameTeamAsync_TeamRoom_GetsTheRenamedLineFromHuddle()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(ct);
        Room room = await f.CreateTeamRoomAsync(ct);

        TeamRenameOutcome outcome = await f.Service.RenameTeamAsync(Marketing, Growth, ct);

        Assert.True(outcome.Renamed);
        IReadOnlyList<ChatMessage> messages = await f.Transcript(room.Id, ct);
        ChatMessage last = messages[^1];
        Assert.Equal(KnownIds.Huddle, last.SenderId);
        Assert.Equal(Line, last.Text);
    }

    /// <summary>R8: when the line is posted the journal row is already Done and neither Team's Task writes are paused.</summary>
    [Fact]
    public async Task RenameTeamAsync_TeamRoom_PostsAfterRowDoneAndAfterUnpause()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(ct);
        _ = await f.CreateTeamRoomAsync(ct);
        List<(int Pending, int Done, bool PausedOld, bool PausedNew)> seen = [];
        f.Events.MessagePosted += _ => seen.Add((f.Renames.Pending().Count, f.Renames.DoneAfter(0).Count, f.IsPaused(Marketing), f.IsPaused(Growth)));

        _ = await f.Service.RenameTeamAsync(Marketing, Growth, ct);

        Assert.Equal((0, 1, false, false), Assert.Single(seen));
    }

    /// <summary>R8: a Team with no Team Room gets no post and the rename still succeeds.</summary>
    [Fact]
    public async Task RenameTeamAsync_NoTeamRoom_PostsNothingAndRenames()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(ct, staleCatalog: true);
        int lines = 0;
        f.Events.MessagePosted += e => lines += e.Message.SenderId == KnownIds.Huddle && e.Message.Text == Line ? 1 : 0;

        TeamRenameOutcome outcome = await f.Service.RenameTeamAsync(Marketing, Growth, ct);

        Assert.True(outcome.Renamed);
        Assert.Null(await f.FindTeamRoomAsync(Growth, ct));
        Assert.Equal(0, lines);
    }

    /// <summary>R8: a post that throws is caught and logged; the rename is still reported as done.</summary>
    [Fact]
    public async Task RenameTeamAsync_PostThrows_StillRenamed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(ct, brokenChat: true);
        Room room = await f.CreateTeamRoomAsync(ct);

        TeamRenameOutcome outcome = await f.Service.RenameTeamAsync(Marketing, Growth, ct);

        Assert.True(outcome.Renamed);
        Assert.Null(outcome.Error);
        Assert.Empty(await f.Transcript(room.Id, ct));
        Assert.Equal(TeamRenameState.Done, Assert.Single(f.Renames.DoneAfter(0)).State);
    }

    /// <summary>R8: recovery that completes a Pending row posts the line once.</summary>
    [Fact]
    public async Task RecoverAsync_CompletesRow_PostsTheLineOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(ct);
        Room room = await f.CreateTeamRoomAsync(ct);
        _ = f.Renames.Begin(TeamRenameKind.Team, Marketing, Marketing, Growth);
        Directory.Move(f.TeamFolder(Marketing), f.TeamFolder(Growth));

        await f.Service.RecoverAsync(ct);

        Assert.Empty(f.Renames.Pending());
        ChatMessage only = Assert.Single(await f.Transcript(room.Id, ct));
        Assert.Equal((KnownIds.Huddle, Line), (only.SenderId, only.Text));
    }

    /// <summary>Recovery that discards a row (the move never happened) posts nothing.</summary>
    [Fact]
    public async Task RecoverAsync_DiscardsRow_PostsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(ct);
        Room room = await f.CreateTeamRoomAsync(ct);
        _ = f.Renames.Begin(TeamRenameKind.Team, Marketing, Marketing, Growth);

        await f.Service.RecoverAsync(ct);

        Assert.Empty(await f.Transcript(room.Id, ct));
    }

    /// <summary>The stuck-rename notice is its own Feed source, <c>team-rename-stuck</c>, carrying the sentence and the new name.</summary>
    [Fact]
    public async Task RenameTeamAsync_StepFails_PostsTeamRenameStuckNotice()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(ct, failingRewrite: true);

        TeamRenameOutcome outcome = await f.Service.RenameTeamAsync(Marketing, Growth, ct);

        Assert.False(outcome.Renamed);
        FeedMessage post = Assert.Single(f.Feed.Posts);
        Assert.Equal(FeedSourceType.TeamRenameStuck, post.SourceType);
        Assert.Equal("team-rename-stuck", FeedSourceTypes.ToWire(post.SourceType));
        Assert.True(post.Notify);
        TeamRenameStuckPayload payload = Assert.IsType<TeamRenameStuckPayload>(post.Payload);
        Assert.Equal(Growth, payload.Team);
        Assert.StartsWith("Renaming Marketing to Growth did not finish: ", payload.Message, StringComparison.Ordinal);
        Assert.EndsWith(". Huddle will try again at the next start.", payload.Message, StringComparison.Ordinal);
    }

    /// <summary>A rename that did not finish posts no Room line, because the row is still Pending.</summary>
    [Fact]
    public async Task RenameTeamAsync_StepFails_PostsNoRoomLine()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(ct, failingRewrite: true);
        Room room = await f.CreateTeamRoomAsync(ct);

        _ = await f.Service.RenameTeamAsync(Marketing, Growth, ct);

        Assert.Empty(await f.Transcript(room.Id, ct));
    }

    /// <summary>A rewrite that always throws.</summary>
    private sealed class FailingRewrite : ITeamRenameRewrite
    {
        /// <inheritdoc />
        public IReadOnlyList<string> TeamsTouched(TeamRename rename) => [];

        /// <inheritdoc />
        public Task RunAsync(TeamRename rename, CancellationToken ct) => throw new InvalidOperationException("The files are locked.");
    }

    /// <summary>The catalog-default Prompt text, rendered the way the real source does.</summary>
    private sealed class DefaultPrompts : IPromptSource
    {
        /// <inheritdoc />
        public string Render(string key, IReadOnlyDictionary<string, string> values) => PromptRenderer.Render(this.Raw(key), values);

        /// <inheritdoc />
        public string Raw(string key) => PromptCatalog.All.Single(definition => definition.Key == key).Default;

        /// <inheritdoc />
        public bool IsOverridden(string key) => false;
    }

    /// <summary>A membership that rewrites the labels and flips the fake catalog, so the reconciler sees the new name.</summary>
    /// <param name="inner">The real membership.</param>
    /// <param name="catalog">The catalog that must follow the rename.</param>
    /// <param name="follow">Whether the catalog follows; when false it keeps the old name, so the reconciler makes no Room for the new one.</param>
    private sealed class FollowingMembership(ITeamMembership inner, FakeTeamCatalog catalog, bool follow = true) : ITeamMembership
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
            if (!follow)
            {
                return;
            }

            catalog.Teams =[.. catalog.Teams.Select(t => string.Equals(t.Name, oldName, StringComparison.OrdinalIgnoreCase) ? t with { Name = newName } : t)];
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly TempDataDir dir;
        private readonly TempDataDir? foreignDir;
        private readonly LibraryFileServiceFixture library;
        private readonly TaskStore tasks;
        private readonly SqliteTeamDirectory directory;
        private int probes;

        private Fixture(TempDataDir dir, TempDataDir? foreignDir, LibraryFileServiceFixture library, PersonaStore personas, TaskStore tasks, SqliteTeamDirectory directory, SqliteTeamDirectory chatDirectory, FakeTeamCatalog catalog, TeamFolderProvisioner folders, bool failingRewrite, bool staleCatalog)
        {
            this.dir = dir;
            this.foreignDir = foreignDir;
            this.library = library;
            this.tasks = tasks;
            this.directory = directory;
            this.Events = new RoomEvents(NullLogger<RoomEvents>.Instance);
            this.Renames = new TeamRenameStore(dir.Options());
            this.Feed = new RecordingFeedPublisher();
            this.Chat = this.BuildChat(chatDirectory, catalog);
            ChatService reconcilerChat = this.BuildChat(directory, catalog);
            this.Reconciler = new TeamRoomReconciler(directory, reconcilerChat, catalog, this.Events, NullLogger<TeamRoomReconciler>.Instance);
            this.TeamsRoot = library.RootStore.Roots.Single(r => r.Kind == LibraryRootKind.Teams).FullPath;
            System.IO.Directory.CreateDirectory(library.RootStore.Roots.Single(r => r.Kind == LibraryRootKind.Archive).FullPath);
            this.Service = new TeamRenameService(
                library.RootStore,
                catalog,
                new FollowingMembership(new TeamMembership(personas, catalog, folders, RoleFixtures.Types(dir)), catalog, !staleCatalog),
                folders,
                tasks,
                new TaskIdAllocator(dir.Options()),
                new CadenceStateStore(dir.Options()),
                personas,
                directory,
                new PhysicalArchiveFileSystem(),
                this.Renames,
                this.Reconciler,
                failingRewrite ? [new FailingRewrite()] : [],
                new TeamOperationGate(),
                NullLogger<TeamRenameService>.Instance,
                feed: this.Feed,
                chat: this.Chat,
                prompts: new DefaultPrompts());
        }

        public TeamRenameService Service { get; }

        public TeamRenameStore Renames { get; }

        public RecordingFeedPublisher Feed { get; }

        public RoomEvents Events { get; }

        public ChatService Chat { get; }

        public TeamRoomReconciler Reconciler { get; }

        public string TeamsRoot { get; }

        /// <summary>Builds the fixture: the Team and a Sales Team on disk, two members and a Room database.</summary>
        /// <param name="ct">Cancels the database set-up.</param>
        /// <param name="brokenChat">Gives the service a Chat service over an empty Room database, so every post fails.</param>
        /// <param name="failingRewrite">Registers a step-6 rewrite that always throws.</param>
        /// <param name="staleCatalog">Keeps the catalog on the old name, so the reconciler makes no Team Room for the new one.</param>
        public static async Task<Fixture> CreateAsync(CancellationToken ct, bool brokenChat = false, bool failingRewrite = false, bool staleCatalog = false)
        {
            TempDataDir dir = new();
            LibraryFileServiceFixture library = LibraryFileServiceFixture.Attach(dir.Path);
            PersonaStore personas = dir.CreatePersonaStore();
            FakeTeamCatalog catalog = new();
            TeamFolderProvisioner folders = new(personas, library.RootStore, library.Resolver, NullLogger<TeamFolderProvisioner>.Instance, catalog);
            string teamsRoot = library.RootStore.Roots.Single(r => r.Kind == LibraryRootKind.Teams).FullPath;
            TeamFile file = new(new VersionedRef("web", "1.2.0"), "Ada", null, null, string.Empty);
            Assert.Equal(TeamCreateOutcome.Created, folders.CreateTeam(Marketing, file).Outcome);
            _ = personas.Add(new PersonaIdentity("Ada", "Lead", "ada", [Marketing]), "Leads.");
            _ = personas.Add(new PersonaIdentity("Nova", "Dev", "nova", [Marketing, "Other"]), "Builds.");
            File.WriteAllText(Path.Combine(teamsRoot, Marketing, "notes.md"), "# Notes\n");
            catalog.Teams =
            [
                new TeamSummary(Marketing, [], ["Ada", "Nova"], HasFolder: true) { Manager = "Ada" },
            ];
            SqliteTeamDirectory directory = new(dir.Options());
            await directory.InitializeAsync("You", ct);
            foreach (string agent in new[] { "Ada", "Nova" })
            {
                _ = await directory.UpsertAgentUserAsync(agent, null, ct);
            }

            TempDataDir? foreign = brokenChat ? new TempDataDir() : null;
            SqliteTeamDirectory chatDirectory = directory;
            if (foreign is not null)
            {
                chatDirectory = new SqliteTeamDirectory(foreign.Options());
                await chatDirectory.InitializeAsync("You", ct);
            }

            TaskStore tasks = TestTaskStore.CreateTaskStore(dir, personas);
            return new Fixture(dir, foreign, library, personas, tasks, directory, chatDirectory, catalog, folders, failingRewrite, staleCatalog);
        }

        /// <summary>The path <c>Teams/&lt;name&gt;</c>.</summary>
        /// <param name="name">The folder name.</param>
        public string TeamFolder(string name) => Path.Combine(this.TeamsRoot, name);

        /// <summary>Creates the Team Room through the reconciler, as the running app does.</summary>
        /// <param name="ct">Cancels the database work.</param>
        public async Task<Room> CreateTeamRoomAsync(CancellationToken ct)
        {
            await this.Reconciler.ReconcileAsync(ct);
            Room? room = await this.directory.FindTeamRoomAsync(Marketing, ct);
            Assert.NotNull(room);
            return room;
        }

        /// <summary>Finds the Team's Team Room in the Room database.</summary>
        /// <param name="team">The Team.</param>
        /// <param name="ct">Cancels the read.</param>
        public Task<Room?> FindTeamRoomAsync(string team, CancellationToken ct) => this.directory.FindTeamRoomAsync(team, ct);

        /// <summary>The Room's Messages, oldest first.</summary>
        /// <param name="roomId">The Room.</param>
        /// <param name="ct">Cancels the read.</param>
        public Task<IReadOnlyList<ChatMessage>> Transcript(string roomId, CancellationToken ct) => this.dir.CreateChatStore().ReadAllAsync(roomId, ct);

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
            this.foreignDir?.Dispose();
            this.dir.Dispose();
        }

        private ChatService BuildChat(SqliteTeamDirectory forDirectory, FakeTeamCatalog catalog) =>
            new(
                forDirectory,
                this.dir.CreateChatStore(),
                this.Events,
                new FakeMentionAliasSource { Aliases = [] },
                TestOptions.Create(),
                new ProposalStore(this.Events),
                new QuestionStore(this.Events),
                NullLogger<ChatService>.Instance)
            {
                TeamCatalog = catalog,
            };
    }
}
