using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.SecondBrain;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Agents;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.Feed;
using Agency.Huddle.Tests.Library;
using Agency.Huddle.Tests.Tasks;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// Archiving a Team on one volume (Spec 6.7 and 8.4 as settled by corrections-12): the roster is written
/// into <c>_team.md</c>, the folder moves whole, the Team's Rooms are archived and recorded, and only then are
/// the member labels removed. The fixture is the real <see cref="TeamFolderProvisioner"/>, <see cref="TaskStore"/>,
/// <see cref="PersonaStore"/> and <see cref="SqliteTeamDirectory"/> over a temp data directory; the membership is a
/// recording fake except in the rows that ask for the real <see cref="TeamMembership"/>.
/// </summary>
public sealed class ArchiveTeamTests
{
    private const string Home = "Home";

    /// <summary>The folder goes: <c>Teams/Home</c> is gone and <c>Archive/Home</c> holds every file and folder with the same bytes, except <c>_team.md</c>.</summary>
    [Fact]
    public async Task ArchiveTeamAsync_SameVolume_MovesFolderWithIdenticalBytes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(labelOnly: false, ct);
        Dictionary<string, string> before = Fixture.Snapshot(f.TeamFolder);

        ArchiveOutcome outcome = await f.Service.ArchiveTeamAsync(Home, ct);

        Assert.True(outcome.Done);
        Assert.Null(outcome.Refusal);
        Assert.False(Directory.Exists(f.TeamFolder));
        // Seeded: notes.md, memory/lesson.md, raw/Tasks/HOME-0001/spec.bin (7 bytes incl. 0x00 and 0xFF), three Task files under _tasks.
        Assert.Contains("notes.md", before.Keys);
        Assert.Contains("raw/Tasks/HOME-0001/spec.bin", before.Keys);
        Assert.Equal(before, Fixture.Snapshot(f.ArchivedFolder));
    }

    /// <summary>The archived <c>_team.md</c> lists the former members, manager included, in ordinal order, drops the manager and keeps the Team type.</summary>
    [Fact]
    public async Task ArchiveTeamAsync_Roster_WrittenIntoArchivedTeamFile()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(labelOnly: false, ct);

        _ = await f.Service.ArchiveTeamAsync(Home, ct);

        TeamFile file = f.ReadArchivedTeamFile();
        // Catalog members Nova, Ada (set in that order) -> ordinal sort -> Ada, Nova; Ada is the manager and is in the roster.
        Assert.Equal(["Ada", "Nova"], file.ArchivedMembers);
        Assert.Null(file.Manager);
        Assert.Equal(new VersionedRef("web", "1.2.0"), file.TeamType);
        Assert.Contains("archivedMembers: ['Ada', 'Nova']", File.ReadAllText(Path.Combine(f.ArchivedFolder, TeamPaths.TeamFileName)), StringComparison.Ordinal);
    }

    /// <summary>Every member, the manager too, loses the label through <c>RemoveForArchive</c>, never the manager-guarded <c>Remove</c>, and only after the folder has moved.</summary>
    [Fact]
    public async Task ArchiveTeamAsync_Members_LabelsRemovedAfterTheMove()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(labelOnly: false, ct);
        List<(bool SourceGone, bool ArchiveHasTeamFile)> seen = [];
        f.Membership.OnCall = _ => seen.Add((!Directory.Exists(f.TeamFolder), File.Exists(Path.Combine(f.ArchivedFolder, TeamPaths.TeamFileName))));

        _ = await f.Service.ArchiveTeamAsync(Home, ct);

        Assert.Equal(
            [new MembershipCall("RemoveForArchive", Home, "Ada"), new MembershipCall("RemoveForArchive", Home, "Nova")],
            f.Membership.Calls.OrderBy(c => c.Name, StringComparer.Ordinal));
        Assert.Equal(2, seen.Count);
        Assert.All(seen, s => Assert.True(s.SourceGone && s.ArchiveHasTeamFile));
    }

    /// <summary>The Team's Rooms are archived and their ids recorded as <c>archivedRooms</c>; another Team's Room is left alone.</summary>
    [Fact]
    public async Task ArchiveTeamAsync_TeamRooms_ArchivedAndRecorded()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(labelOnly: false, ct);
        Room homeRoom = await f.CreateRoomAsync(Home, ct);
        Room otherRoom = await f.CreateRoomAsync("Other", ct);

        _ = await f.Service.ArchiveTeamAsync(Home, ct);

        Assert.Equal([homeRoom.Id], f.ReadArchivedTeamFile().ArchivedRooms);
        IReadOnlyList<Room> rooms = await f.Directory.GetRoomsAsync(ct);
        Assert.True(rooms.Single(r => r.Id == homeRoom.Id).Archived);
        Assert.False(rooms.Single(r => r.Id == otherRoom.Id).Archived);
    }

    /// <summary>The archive holds exactly the Team's folder afterwards. (Task 113 adds the staging folder and pins that no dot folder is left.)</summary>
    [Fact]
    public async Task ArchiveTeamAsync_Done_ArchiveHoldsOnlyTheTeamFolder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(labelOnly: false, ct);

        _ = await f.Service.ArchiveTeamAsync(Home, ct);

        Assert.Equal([Home], Directory.GetDirectories(f.ArchiveRoot).Select(Path.GetFileName));
    }

    /// <summary>An empty <c>Archive/Home</c> left behind does not block the move: the folder still goes over whole and the archive is done.</summary>
    [Fact]
    public async Task ArchiveTeamAsync_EmptyArchiveFolderPresent_StillMoves()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(labelOnly: false, ct);
        Directory.CreateDirectory(f.ArchivedFolder);
        Dictionary<string, string> before = Fixture.Snapshot(f.TeamFolder);

        ArchiveOutcome outcome = await f.Service.ArchiveTeamAsync(Home, ct);

        Assert.True(outcome.Done);
        Assert.Null(outcome.Refusal);
        Assert.False(Directory.Exists(f.TeamFolder));
        Assert.Equal(before, Fixture.Snapshot(f.ArchivedFolder));
        Assert.True(File.Exists(Path.Combine(f.ArchivedFolder, TeamPaths.TeamFileName)));
    }

    /// <summary>Through the real <see cref="TeamMembership"/> the manager loses the label too, and the archived <c>_team.md</c> no longer names a manager.</summary>
    [Fact]
    public async Task ArchiveTeamAsync_RealMembership_ManagerLosesLabelAndTeamFileManagerCleared()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(labelOnly: false, ct, realMembership: true);
        Assert.Equal(["Home"], f.TeamLabels("Ada")); // Seeded: Ada's only Team is Home and she is the catalog's manager.

        ArchiveOutcome outcome = await f.Service.ArchiveTeamAsync(Home, ct);

        Assert.True(outcome.Done);
        Assert.Empty(f.TeamLabels("Ada"));
        Assert.Equal(["Other"], f.TeamLabels("Nova")); // Seeded [Home, Other]; only Home goes.
        Assert.Null(f.ReadArchivedTeamFile().Manager);
    }

    /// <summary>A Team with a label but no <c>_team.md</c> gets one created in the archive, with no Team type, the roster and the same other files.</summary>
    [Fact]
    public async Task ArchiveTeamAsync_LabelOnlyTeam_GetsTeamFile()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(labelOnly: true, ct);
        Assert.False(File.Exists(Path.Combine(f.TeamFolder, TeamPaths.TeamFileName)));
        Dictionary<string, string> before = Fixture.Snapshot(f.TeamFolder);

        ArchiveOutcome outcome = await f.Service.ArchiveTeamAsync(Home, ct);

        Assert.True(outcome.Done);
        TeamFile file = f.ReadArchivedTeamFile();
        Assert.Equal(VersionedRef.None, file.TeamType);
        Assert.Equal(["Ada", "Nova"], file.ArchivedMembers);
        Assert.Equal(before, Fixture.Snapshot(f.ArchivedFolder));
    }

    /// <summary>A second archive of the same Team is refused, in any case, and removes no further labels.</summary>
    [Theory]
    [InlineData("Home")]
    [InlineData("home")]
    public async Task ArchiveTeamAsync_AlreadyArchived_Refused(string again)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(labelOnly: false, ct);
        _ = await f.Service.ArchiveTeamAsync(Home, ct);
        int callsAfterFirst = f.Membership.Calls.Count;

        ArchiveOutcome outcome = await f.Service.ArchiveTeamAsync(again, ct);

        Assert.False(outcome.Done);
        Assert.False(string.IsNullOrWhiteSpace(outcome.Refusal));
        Assert.Equal(2, callsAfterFirst); // Ada and Nova.
        Assert.Equal(callsAfterFirst, f.Membership.Calls.Count);
    }

    /// <summary>The destination takes its spelling from the folder on disk, not from the request.</summary>
    [Fact]
    public async Task ArchiveTeamAsync_RequestInOtherCase_UsesFolderSpelling()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(labelOnly: false, ct);

        ArchiveOutcome outcome = await f.Service.ArchiveTeamAsync("hOME", ct);

        Assert.True(outcome.Done);
        Assert.Equal([Home], Directory.GetDirectories(f.ArchiveRoot).Select(Path.GetFileName));
        Assert.True(File.Exists(Path.Combine(f.ArchivedFolder, TeamPaths.TeamFileName)));
    }

    /// <summary>A Team with no <c>_team.md</c> in the archive but an archived Project there is not "archived": the Project folders merge into the Team's archive.</summary>
    [Fact]
    public async Task ArchiveTeamAsync_AlreadyArchivedProject_MergedIntoTeamArchive()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(labelOnly: false, ct);
        string oldProject = Path.Combine(f.ArchivedFolder, "Old");
        Directory.CreateDirectory(oldProject);
        File.WriteAllText(Path.Combine(oldProject, "plan.md"), "old plan");

        ArchiveOutcome outcome = await f.Service.ArchiveTeamAsync(Home, ct);

        Assert.True(outcome.Done);
        Assert.Equal("old plan", File.ReadAllText(Path.Combine(f.ArchivedFolder, "Old", "plan.md")));
        Assert.True(File.Exists(Path.Combine(f.ArchivedFolder, "notes.md")));
        Assert.True(File.Exists(Path.Combine(f.ArchivedFolder, TeamPaths.TeamFileName)));
    }

    /// <summary>A live Project whose name an archived Project of the Team already holds refuses the whole archive: nothing moves and no label is removed.</summary>
    [Fact]
    public async Task ArchiveTeamAsync_ProjectNameClash_RefusedUntouched()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(labelOnly: false, ct);
        Directory.CreateDirectory(Path.Combine(f.TeamFolder, "Old"));
        Directory.CreateDirectory(Path.Combine(f.ArchivedFolder, "Old"));
        Dictionary<string, string> before = Fixture.Snapshot(f.TeamFolder);

        ArchiveOutcome outcome = await f.Service.ArchiveTeamAsync(Home, ct);

        Assert.False(outcome.Done);
        Assert.False(string.IsNullOrWhiteSpace(outcome.Refusal));
        Assert.Equal(before, Fixture.Snapshot(f.TeamFolder));
        Assert.Empty(f.Membership.Calls);
        Assert.False(File.Exists(Path.Combine(f.ArchivedFolder, TeamPaths.TeamFileName)));
    }

    /// <summary>A Team that does not exist is refused and nothing is created in the archive.</summary>
    [Fact]
    public async Task ArchiveTeamAsync_UnknownTeam_Refused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(labelOnly: false, ct);

        ArchiveOutcome outcome = await f.Service.ArchiveTeamAsync("Nowhere", ct);

        Assert.False(outcome.Done);
        Assert.False(string.IsNullOrWhiteSpace(outcome.Refusal));
        Assert.Empty(Directory.GetDirectories(f.ArchiveRoot));
        Assert.Empty(f.Membership.Calls);
    }

    /// <summary>The confirmation counts the Team's open Tasks and those waiting on the Human, and names the Agents whose only Team this is.</summary>
    [Fact]
    public async Task Confirm_HomeTeam_CountsTasksAndNamesAgentsWithoutATeam()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(labelOnly: false, ct);

        ArchiveConfirmation confirmation = f.Service.Confirm(Home);

        // Home Tasks seeded: HOME-0001 ToDo, HOME-0002 NeedsYou, HOME-0003 Done (closed) -> open 2, waiting 1.
        // Other's OTHR-0001 ToDo is not Home's. Ada has only Home; Nova has Home and Other -> only Ada has no Team left.
        Assert.Equal(2, confirmation.OpenTasks);
        Assert.Equal(1, confirmation.NeedsYou);
        Assert.Equal(["Ada"], confirmation.NoTeamNames);
        Assert.Equal(
            "Archive Home? 2 open Tasks and 1 waiting on you will leave the Boards. These Agents will have no Team: Ada.",
            confirmation.Text(Home));
    }

    /// <summary>The confirmation text is the settled T16 wording and omits the second sentence when every Agent keeps a Team.</summary>
    [Fact]
    public void Text_NoAgentsLeftWithoutATeam_OmitsTheNamesSentence()
    {
        ArchiveConfirmation confirmation = new(0, 0, []);

        string text = confirmation.Text("Home");

        Assert.Equal("Archive Home? 0 open Tasks and 0 waiting on you will leave the Boards.", text);
    }

    /// <summary>Several Agents are listed comma-separated, in the order given.</summary>
    [Fact]
    public void Text_SeveralNames_ListedWithCommas()
    {
        ArchiveConfirmation confirmation = new(5, 2, ["Ada", "Zed"]);

        string text = confirmation.Text("Home");

        Assert.Equal(
            "Archive Home? 5 open Tasks and 2 waiting on you will leave the Boards. These Agents will have no Team: Ada, Zed.",
            text);
    }

    /// <summary>Real stores over a temp data directory with the Home Team seeded, and a service wired to them.</summary>
    internal sealed class Fixture : IDisposable
    {
        private readonly TempDataDir dir;
        private readonly LibraryFileServiceFixture library;
        private readonly TaskStore tasks;
        private readonly PersonaStore personas;

        private Fixture(TempDataDir dir, LibraryFileServiceFixture library, PersonaStore personas, SqliteTeamDirectory directory, TaskStore tasks, TeamFolderProvisioner folders, FakeTeamCatalog catalog, bool realMembership)
        {
            this.dir = dir;
            this.library = library;
            this.tasks = tasks;
            this.personas = personas;
            this.Directory = directory;
            this.Catalog = catalog;
            this.Membership = new RecordingTeamMembership();
            ITeamMembership membership = realMembership
                ? new TeamMembership(personas, catalog, folders, RoleFixtures.Types(dir))
                : this.Membership;
            this.Service = new ArchiveService(
                library.RootStore,
                catalog,
                membership,
                folders,
                tasks,
                personas,
                directory,
                new PhysicalArchiveFileSystem(),
                new RecordingFeedPublisher(),
                NullLogger<ArchiveService>.Instance);
            this.TeamsRoot = library.RootStore.Roots.Single(r => r.Kind == LibraryRootKind.Teams).FullPath;
            this.ArchiveRoot = library.RootStore.Roots.Single(r => r.Kind == LibraryRootKind.Archive).FullPath;
            System.IO.Directory.CreateDirectory(this.ArchiveRoot);
        }

        public ArchiveService Service { get; }

        public RecordingTeamMembership Membership { get; }

        public SqliteTeamDirectory Directory { get; }

        /// <summary>Gets the catalog the service reads; a test edits <see cref="FakeTeamCatalog.Teams"/> to say what the catalog knows after the archive.</summary>
        public FakeTeamCatalog Catalog { get; }

        /// <summary>Gets the Persona store, so a test can delete a member.</summary>
        public PersonaStore Personas => this.personas;

        public string TeamsRoot { get; }

        public string ArchiveRoot { get; }

        public string TeamFolder => Path.Combine(this.TeamsRoot, Home);

        public string ArchivedFolder => Path.Combine(this.ArchiveRoot, Home);

        /// <summary>Builds the fixture: the Home and Other Teams on disk, Ada (Home's manager) and Nova as Personas, and Tasks, files and a Room database seeded.</summary>
        /// <param name="labelOnly">When <see langword="true"/> Home has a folder but no <c>_team.md</c>.</param>
        /// <param name="ct">Cancels the database set-up.</param>
        /// <param name="realMembership">When <see langword="true"/> the service removes labels through the real <see cref="TeamMembership"/>.</param>
        public static async Task<Fixture> CreateAsync(bool labelOnly, CancellationToken ct, bool realMembership = false)
        {
            TempDataDir dir = new();
            LibraryFileServiceFixture library = LibraryFileServiceFixture.Attach(dir.Path);
            PersonaStore personas = dir.CreatePersonaStore();
            FakeTeamCatalog catalog = new();
            TeamFolderProvisioner folders = new(personas, library.RootStore, library.Resolver, NullLogger<TeamFolderProvisioner>.Instance, catalog);
            string teamsRoot = library.RootStore.Roots.Single(r => r.Kind == LibraryRootKind.Teams).FullPath;
            string home = Path.Combine(teamsRoot, Home);
            if (labelOnly)
            {
                System.IO.Directory.CreateDirectory(home);
            }
            else
            {
                TeamFile file = new(new VersionedRef("web", "1.2.0"), "Ada", null, null, string.Empty);
                Assert.Equal(TeamCreateOutcome.Created, folders.CreateTeam(Home, file).Outcome);
            }

            _ = personas.Add(new PersonaIdentity("Ada", "Lead", "ada", [Home]), "Leads.");
            _ = personas.Add(new PersonaIdentity("Nova", "Dev", "nova", [Home, "Other"]), "Builds.");
            catalog.Teams = [new TeamSummary(Home, [], ["Nova", "Ada"], HasFolder: true) { Manager = "Ada" }, new TeamSummary("Other", [], ["Nova"], HasFolder: true)];
            SeedFiles(home);
            System.IO.Directory.CreateDirectory(Path.Combine(teamsRoot, "Other"));
            SeedTasks(teamsRoot);
            SqliteTeamDirectory directory = new(dir.Options());
            await directory.InitializeAsync("You", ct);
            _ = await directory.UpsertAgentUserAsync("Ada", null, ct);
            _ = await directory.UpsertAgentUserAsync("Nova", null, ct);
            TaskStore tasks = TestTaskStore.CreateTaskStore(dir, personas);
            return new Fixture(dir, library, personas, directory, tasks, folders, catalog, realMembership);
        }

        /// <summary>Lists every folder and file under <paramref name="root"/> except <c>_team.md</c>, keyed by forward-slash relative path; a file maps to its hex bytes, a folder to a marker.</summary>
        /// <param name="root">The folder to list.</param>
        public static Dictionary<string, string> Snapshot(string root)
        {
            Dictionary<string, string> entries = new(StringComparer.Ordinal);
            foreach (string path in System.IO.Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                if (relative == TeamPaths.TeamFileName)
                {
                    continue;
                }

                entries[relative] = System.IO.Directory.Exists(path) ? "<dir>" : Convert.ToHexString(File.ReadAllBytes(path));
            }

            return entries;
        }

        /// <summary>Parses <c>Archive/Home/_team.md</c>, failing the test when it is missing or unreadable.</summary>
        public TeamFile ReadArchivedTeamFile()
        {
            Assert.True(TeamFile.TryParse(File.ReadAllText(Path.Combine(this.ArchivedFolder, TeamPaths.TeamFileName)), out TeamFile? file));
            return file;
        }

        /// <summary>The Team labels in the named Persona's definition, as the store holds them now.</summary>
        /// <param name="name">The Persona's name.</param>
        public IReadOnlyList<string> TeamLabels(string name)
        {
            Persona? persona = this.personas.Get(name);
            Assert.NotNull(persona);
            Assert.True(PersonaFrontmatter.TryReadIdentity(persona.Text, out PersonaIdentity? identity, out _));
            return identity.Teams;
        }

        /// <summary>Creates a Room of the Team with the Human and Nova in it.</summary>
        /// <param name="team">The Team the Room belongs to.</param>
        /// <param name="ct">Cancels the database write.</param>
        public async Task<Room> CreateRoomAsync(string team, CancellationToken ct)
        {
            User? nova = await this.Directory.FindUserByNameAsync("Nova", ct);
            Assert.NotNull(nova);
            return await this.Directory.CreateRoomAsync(team + " room", [KnownIds.Human, nova.Id], team, true, ct);
        }

        public void Dispose()
        {
            this.tasks.Dispose();
            this.library.Dispose();
            this.dir.Dispose();
        }

        private static void SeedFiles(string home)
        {
            System.IO.Directory.CreateDirectory(Path.Combine(home, "memory"));
            System.IO.Directory.CreateDirectory(Path.Combine(home, "raw", "Tasks", "HOME-0001"));
            File.WriteAllText(Path.Combine(home, "notes.md"), "# Home notes\n");
            File.WriteAllText(Path.Combine(home, "memory", "lesson.md"), "a lesson");
            File.WriteAllBytes(Path.Combine(home, "raw", "Tasks", "HOME-0001", "spec.bin"), [0, 1, 2, 254, 255, 10, 13]);
        }

        private static void SeedTasks(string teamsRoot)
        {
            WriteTask(teamsRoot, "Home", "HOME-0001", TaskState.ToDo, closed: false);
            WriteTask(teamsRoot, "Home", "HOME-0002", TaskState.NeedsYou, closed: false);
            WriteTask(teamsRoot, "Home", "HOME-0003", TaskState.Done, closed: true);
            WriteTask(teamsRoot, "Other", "OTHR-0001", TaskState.ToDo, closed: false);
        }

        private static void WriteTask(string teamsRoot, string team, string id, TaskState state, bool closed)
        {
            string relative = TestTaskStore.RelativePath(team, null, closed, id + ".md");
            TaskItem task = TestTasks.Make(id: id, status: state, location: new TaskLocation(team, null, closed), closedAt: closed ? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) : null);
            _ = TestTaskStore.WriteTask(teamsRoot, relative, task);
        }
    }
}
