using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
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
        new(new VersionedRef("development", "1"), manager, "C:\\Work\\Alpha", "The alpha product", "Process.md", "\nSome notes.\n")
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
            Directory.CreateDirectory(Path.Combine(this.dir.Path, "Teammates"));
            foreach (string seed in seedFolders)
            {
                Directory.CreateDirectory(Path.Combine(this.dir.Path, "Teams", seed));
            }

            IOptions<TeamOptions> options = this.dir.Options();
            TeammatePaths paths = new(options);
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
