using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Tasks;

namespace Agency.Huddle.Tests.Library;

/// <summary>
/// Pins <see cref="ITeamFolders.CreateTeam"/>, <see cref="ITeamFolders.UpdateTeamFile"/> and
/// <see cref="ITeamFolders.ReadTeamFile"/> on <see cref="TeamFolderProvisioner"/> (HR-1, Spec §Hiring
/// "New team"): the Team folder gets its <c>_team.md</c>, an existing file is never overwritten, and
/// an update rewrites only what the callback changes.
/// </summary>
public sealed class TeamFolderProvisionerCreateTeamTests
{
    private static TeamFile Sample(string? manager = "Ada") =>
        new(new VersionedRef("development", "1"), manager, "C:\\Work\\Alpha", "The alpha product", "\nSome notes.\n")
        {
            UnknownLines = ["custom: 'kept'"],
        };

    /// <summary>A new valid name creates the Team folder and writes <c>_team.md</c> holding the composed file.</summary>
    [Fact]
    public void CreateTeam_NewName_CreatesFolderAndFile()
    {
        using Fixture fixture = new();
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();
        TeamFile file = Sample();

        TeamCreateResult result = provisioner.CreateTeam("Alpha", file);

        string expected = Path.Combine(fixture.TeamsRoot, "Alpha", TeamPaths.TeamFileName);
        Assert.Equal(TeamCreateOutcome.Created, result.Outcome);
        Assert.Equal(expected, result.Path);
        Assert.Equal(file.Compose(), File.ReadAllText(expected, Encoding.UTF8));
    }

    /// <summary>An existing empty Team folder, created the ordinary way (as <c>BuildTeam</c> does), still gets <c>raw</c>, <c>memory</c> and <c>outputs</c>.</summary>
    [Fact]
    public void CreateTeam_ExistingEmptyFolder_EnsuresSecondBrainFolders()
    {
        using Fixture fixture = new();
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();
        string folder = Path.Combine(fixture.TeamsRoot, "Home");
        Directory.CreateDirectory(folder);

        TeamCreateResult result = provisioner.CreateTeam("Home", Sample());

        Assert.Equal(TeamCreateOutcome.Created, result.Outcome);
        Assert.True(Directory.Exists(Path.Combine(folder, "raw")));
        Assert.True(Directory.Exists(Path.Combine(folder, "memory")));
        Assert.True(Directory.Exists(Path.Combine(folder, "outputs")));
    }

    /// <summary>The archive's call (<c>leaveExistingAlone</c>) writes <c>_team.md</c> into an existing folder and adds no folder beside it.</summary>
    [Fact]
    public void CreateTeam_LeaveExistingAlone_AddsOnlyTheTeamFile()
    {
        using Fixture fixture = new();
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();
        string folder = Path.Combine(fixture.TeamsRoot, "Home");
        Directory.CreateDirectory(folder);

        TeamCreateResult result = provisioner.CreateTeam("Home", Sample(), leaveExistingAlone: true);

        Assert.Equal(TeamCreateOutcome.Created, result.Outcome);
        Assert.Empty(Directory.GetDirectories(folder));
        Assert.True(File.Exists(Path.Combine(folder, TeamPaths.TeamFileName)));
    }

    /// <summary>A second call for the same name returns <c>AlreadyExists</c> and leaves the first file byte-identical.</summary>
    [Fact]
    public void CreateTeam_SecondCall_AlreadyExistsAndFileUntouched()
    {
        using Fixture fixture = new();
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();
        provisioner.CreateTeam("Alpha", Sample());
        string path = Path.Combine(fixture.TeamsRoot, "Alpha", TeamPaths.TeamFileName);
        byte[] before = File.ReadAllBytes(path);

        TeamCreateResult second = provisioner.CreateTeam("alpha", Sample("Someone Else"));

        Assert.Equal(TeamCreateOutcome.AlreadyExists, second.Outcome);
        Assert.Null(second.Path);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    /// <summary>Names differing from an existing Team folder only by case are refused as <c>AlreadyExists</c> on any file system, leaving the first file byte-identical.</summary>
    [Theory]
    [InlineData("ALPHA")]
    [InlineData("alpha")]
    [InlineData("aLPha")]
    public void CreateTeam_CaseVariantOfExistingFolder_AlreadyExistsOnAnyOs(string variant)
    {
        using Fixture fixture = new();
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();
        provisioner.CreateTeam("Alpha", Sample());
        string path = Path.Combine(fixture.TeamsRoot, "Alpha", TeamPaths.TeamFileName);
        byte[] before = File.ReadAllBytes(path);

        TeamCreateResult result = provisioner.CreateTeam(variant, Sample("Someone Else"));

        Assert.Equal(TeamCreateOutcome.AlreadyExists, result.Outcome);
        Assert.Null(result.Path);
        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.Equal(["Alpha"], Directory.GetDirectories(fixture.TeamsRoot).Select(Path.GetFileName));
    }

    /// <summary>A name that differs from an existing Team only by case is refused as invalid and writes nothing.</summary>
    [Fact]
    public void CreateTeam_NameDiffersByCaseFromExistingTeam_InvalidNameAndNothingWritten()
    {
        using Fixture fixture = new("Dev");
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();

        TeamCreateResult result = provisioner.CreateTeam("dev", Sample());

        Assert.Equal(TeamCreateOutcome.InvalidName, result.Outcome);
        Assert.Null(result.Path);
        Assert.False(File.Exists(Path.Combine(fixture.TeamsRoot, "Dev", TeamPaths.TeamFileName)));
    }

    /// <summary>A Team folder that exists without a <c>_team.md</c> (a Persona-label Team) receives the file.</summary>
    [Fact]
    public void CreateTeam_FolderWithoutTeamFile_WritesFile()
    {
        using Fixture fixture = new("Business");
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();

        TeamCreateResult result = provisioner.CreateTeam("Business", Sample());

        Assert.Equal(TeamCreateOutcome.Created, result.Outcome);
        Assert.True(File.Exists(Path.Combine(fixture.TeamsRoot, "Business", TeamPaths.TeamFileName)));
    }

    /// <summary>A name the Team-name rules refuse returns <c>InvalidName</c> and creates nothing.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("_hidden")]
    [InlineData("a,b")]
    [InlineData("a/b")]
    public void CreateTeam_InvalidName_ReturnsInvalidNameAndCreatesNothing(string name)
    {
        using Fixture fixture = new();
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();

        TeamCreateResult result = provisioner.CreateTeam(name, Sample());

        Assert.Equal(TeamCreateOutcome.InvalidName, result.Outcome);
        Assert.Null(result.Path);
        Assert.Empty(Directory.GetFileSystemEntries(fixture.TeamsRoot));
    }

    /// <summary>The atomic write leaves no temporary file next to <c>_team.md</c>.</summary>
    [Fact]
    public void CreateTeam_LeavesNoTemporaryFiles()
    {
        using Fixture fixture = new();
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();

        provisioner.CreateTeam("Alpha", Sample());
        provisioner.UpdateTeamFile("Alpha", static current => current with { Manager = "Bo" });

        string[] names = [.. Directory.GetFiles(Path.Combine(fixture.TeamsRoot, "Alpha")).Select(Path.GetFileName).OfType<string>()];
        Assert.Equal([TeamPaths.TeamFileName], names);
    }

    /// <summary>An update that changes only the manager keeps the type, workspace, owns, notes and unknown lines.</summary>
    [Fact]
    public void UpdateTeamFile_ChangesOnlyTheManager()
    {
        using Fixture fixture = new();
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();
        provisioner.CreateTeam("Alpha", Sample());

        bool updated = provisioner.UpdateTeamFile("Alpha", static current => current with { Manager = "Bo" });

        TeamFile? after = provisioner.ReadTeamFile("Alpha");
        Assert.True(updated);
        Assert.NotNull(after);
        Assert.Equal((Sample() with { Manager = "Bo" }).Compose(), after.Compose());
        Assert.Equal("Bo", after.Manager);
    }

    /// <summary>Updating a Team with no <c>_team.md</c> returns false and never calls the callback.</summary>
    [Fact]
    public void UpdateTeamFile_NoFile_ReturnsFalseWithoutCallingChange()
    {
        using Fixture fixture = new("Business");
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();
        int calls = 0;

        bool updated = provisioner.UpdateTeamFile("Business", current =>
        {
            calls++;
            return current;
        });

        Assert.False(updated);
        Assert.Equal(0, calls);
    }

    /// <summary>
    /// <see cref="ITeamFolders.UpdateProjectAvatar"/> (ADR-0083) creates the Project's <c>_project.md</c> with the label and background and
    /// never the image; a second write keeps the file's other lines and notes.
    /// </summary>
    [Fact]
    public void UpdateProjectAvatar_WritesLabelAndBackground_NeverTheImage_AndKeepsOtherLines()
    {
        using Fixture fixture = new("Business", Path.Combine("Business", "Launch"));
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();
        string path = Path.Combine(fixture.TeamsRoot, "Business", "Launch", TeamPaths.ProjectFileName);

        Assert.True(provisioner.UpdateProjectAvatar("Business", "Launch", new Avatar("🚀", "face.png", "#336699")));
        Assert.Equal("---\navatarLabel: '🚀'\navatarBackground: '#336699'\n---\n", File.ReadAllText(path));

        File.WriteAllText(path, "---\navatarLabel: '🚀'\nowner: 'Ada'\n---\nNotes\n");
        Assert.True(provisioner.UpdateProjectAvatar("Business", "Launch", new Avatar("LA", null, null)));
        Assert.Equal("---\navatarLabel: 'LA'\nowner: 'Ada'\n---\nNotes\n", File.ReadAllText(path));
    }

    /// <summary>
    /// Saving the avatar a Project already has writes nothing: a Project with no file and <see cref="Avatar.None"/> keeps no file, and
    /// an unchanged hand-edited file keeps its blank line rather than being tidied.
    /// </summary>
    [Fact]
    public void UpdateProjectAvatar_Unchanged_WritesNothing()
    {
        using Fixture fixture = new("Business", Path.Combine("Business", "Launch"));
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();
        string path = Path.Combine(fixture.TeamsRoot, "Business", "Launch", TeamPaths.ProjectFileName);

        Assert.True(provisioner.UpdateProjectAvatar("Business", "Launch", Avatar.None));
        Assert.False(File.Exists(path));

        const string HandEdited = "---\navatarLabel: 'LA'\n\nowner: 'Ada'\n---\n";
        File.WriteAllText(path, HandEdited);
        Assert.True(provisioner.UpdateProjectAvatar("Business", "Launch", new Avatar("LA", null, null)));
        Assert.Equal(HandEdited, File.ReadAllText(path));
    }

    /// <summary>Writing the avatar of a Project with no folder returns false and creates nothing.</summary>
    [Fact]
    public void UpdateProjectAvatar_NoProjectFolder_ReturnsFalse()
    {
        using Fixture fixture = new("Business");
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();

        Assert.False(provisioner.UpdateProjectAvatar("Business", "Nope", new Avatar("NO", null, null)));
        Assert.False(Directory.Exists(Path.Combine(fixture.TeamsRoot, "Business", "Nope")));
    }

    /// <summary>
    /// <see cref="ITeamFolders.UpdateProjectFile"/> creates the file when the Project has none, keeps every other line and the notes of one that
    /// exists, writes nothing for an unchanged file, and <see cref="ITeamFolders.ReadProjectFile"/> reads it back; a missing folder or file is a miss.
    /// </summary>
    [Fact]
    public void UpdateProjectFile_AndReadProjectFile_RoundTripTheAvatarAndKeepTheRest()
    {
        using Fixture fixture = new("Business", Path.Combine("Business", "Launch"));
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();
        string path = Path.Combine(fixture.TeamsRoot, "Business", "Launch", TeamPaths.ProjectFileName);
        Assert.Null(provisioner.ReadProjectFile("Business", "Launch"));

        Assert.True(provisioner.UpdateProjectFile("Business", "Launch", file => file with { Avatar = new Avatar("LA", null, null) }));
        Assert.Equal("---\navatarLabel: 'LA'\n---\n", File.ReadAllText(path));

        File.WriteAllText(path, "---\navatarLabel: 'LA'\nowner: 'Ada'\n---\nNotes\n");
        Assert.True(provisioner.UpdateProjectFile("Business", "Launch", file => file with { Avatar = new Avatar("LB", null, null) }));
        Assert.Equal("---\navatarLabel: 'LB'\nowner: 'Ada'\n---\nNotes\n", File.ReadAllText(path));
        Assert.Equal("LB", provisioner.ReadProjectFile("Business", "Launch")?.Avatar.Label);

        Assert.False(provisioner.UpdateProjectFile("Business", "Nope", file => file));
        Assert.Null(provisioner.ReadProjectFile("Business", "Nope"));
    }

    /// <summary>A Project nobody changed keeps no <c>_project.md</c> when the rule is left absent, and the rule is written and read back once set (ADR-0083, ADR-0103).</summary>
    [Fact]
    public void UpdateProjectFile_OneInProgressRule_IsWrittenOnlyWhenSet()
    {
        using Fixture fixture = new("Business", Path.Combine("Business", "Launch"));
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();
        string path = Path.Combine(fixture.TeamsRoot, "Business", "Launch", TeamPaths.ProjectFileName);

        Assert.True(provisioner.UpdateProjectFile("Business", "Launch", file => file with { OneInProgressPerProject = RuleSetting.Absent }));
        Assert.False(File.Exists(path));

        Assert.True(provisioner.UpdateProjectFile("Business", "Launch", file => file with { OneInProgressPerProject = RuleSetting.On }));
        Assert.Equal("---\noneInProgressPerProject: 'on'\n---\n", File.ReadAllText(path));
        Assert.Equal(RuleSetting.On, provisioner.ReadProjectFile("Business", "Launch")?.OneInProgressPerProject);
    }

    /// <summary>Reading a Team that does not exist, or has no file, returns null.</summary>
    [Fact]
    public void ReadTeamFile_MissingTeamOrFile_ReturnsNull()
    {
        using Fixture fixture = new("Business");
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();

        Assert.Null(provisioner.ReadTeamFile("Nope"));
        Assert.Null(provisioner.ReadTeamFile("Business"));
    }

    /// <summary>Reading a created Team returns the parsed file.</summary>
    [Fact]
    public void ReadTeamFile_AfterCreate_ReturnsTheFile()
    {
        using Fixture fixture = new();
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();
        provisioner.CreateTeam("Alpha", Sample());

        TeamFile? read = provisioner.ReadTeamFile("Alpha");

        Assert.NotNull(read);
        Assert.Equal(Sample().Compose(), read.Compose());
    }

    /// <summary>A real data folder with the Persona, Library and catalog collaborators the provisioner needs.</summary>
    private sealed class Fixture : IDisposable
    {
        private readonly TempDataDir dir;
        private readonly PersonaStore personas;
        private readonly LibraryRootStore roots;
        private readonly LibraryPathResolver resolver;
        private readonly TaskStore tasks;
        private readonly TeamCatalog catalog;

        /// <param name="seedFolders">Team folders created before the catalog is built.</param>
        public Fixture(params string[] seedFolders)
        {
            this.dir = new TempDataDir();
            Directory.CreateDirectory(Path.Combine(this.dir.Path, "Teams"));
            Directory.CreateDirectory(Path.Combine(this.dir.Path, "Agents"));
            foreach (string seed in seedFolders)
            {
                Directory.CreateDirectory(Path.Combine(this.dir.Path, "Teams", seed));
            }

            IOptions<TeamOptions> options = this.dir.Options();
            AgentPaths paths = new(options);
            this.personas = new PersonaStore(paths, new PersonaModelStore(options), new PersonaEffortStore(options), new PersonaWorkModeStore(options), NullLogger<PersonaStore>.Instance);
            this.roots = new LibraryRootStore(options, paths, NullLogger<LibraryRootStore>.Instance);
            this.resolver = new LibraryPathResolver(this.roots, options, NullLogger<LibraryPathResolver>.Instance);
            this.tasks = TestTaskStore.CreateTaskStore(this.dir, this.personas);
            this.catalog = new TeamCatalog(this.personas, this.tasks);
        }

        public string TeamsRoot => Path.Combine(this.dir.Path, "Teams");

        public TeamFolderProvisioner CreateProvisioner() =>
            new(this.personas, this.roots, this.resolver, NullLogger<TeamFolderProvisioner>.Instance, catalog: this.catalog);

        public void Dispose()
        {
            this.catalog.Dispose();
            this.tasks.Dispose();
            this.personas.Dispose();
            this.dir.Dispose();
        }
    }
}
