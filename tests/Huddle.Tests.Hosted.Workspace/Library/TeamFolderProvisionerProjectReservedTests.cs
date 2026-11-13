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
/// Pins Team Rename Spec 5.5 (Project rule): <see cref="TeamFolderProvisioner.EnsureProject"/> and
/// <see cref="TeamFolderProvisioner.EnsureProjectIn"/> refuse a Project name the rename journal holds, so an old name is not
/// re-created behind a rename, and still accept an unrelated name.
/// </summary>
public sealed class TeamFolderProvisionerProjectReservedTests
{
    private const string Refusal = "A Project named \"Launch\" was renamed in this Team, so the name is reserved.";

    /// <summary><c>EnsureProject</c> refuses the old name of a renamed Project and creates nothing.</summary>
    [Fact]
    public void EnsureProject_ReservedName_IsRefused()
    {
        using Fixture fixture = new();
        fixture.Renames.Complete(fixture.Renames.Begin(TeamRenameKind.Project, "Business", "Launch", "Launch-2027"));
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();
        Assert.True(fixture.Resolver.TryResolve("teams", "Business", out LibraryPath? teamFolder, out _));

        LibraryResult<LibraryPath> result = provisioner.EnsureProject(teamFolder, "Launch");
        LibraryResult<LibraryPath> otherCase = provisioner.EnsureProject(teamFolder, "launch");

        Assert.Null(result.Value);
        Assert.Equal(Refusal, result.Error);
        Assert.Null(otherCase.Value);
        Assert.NotNull(otherCase.Error);
        Assert.False(Directory.Exists(Path.Combine(fixture.TeamsRoot, "Business", "launch")));
    }

    /// <summary><c>EnsureProject</c> refuses a name held by a Pending Project rename too.</summary>
    [Fact]
    public void EnsureProject_PendingNewName_IsRefused()
    {
        using Fixture fixture = new();
        _ = fixture.Renames.Begin(TeamRenameKind.Project, "Business", "Launch", "Launch-2027");
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();
        Assert.True(fixture.Resolver.TryResolve("teams", "Business", out LibraryPath? teamFolder, out _));

        LibraryResult<LibraryPath> result = provisioner.EnsureProject(teamFolder, "Launch-2027");

        Assert.Null(result.Value);
        Assert.NotNull(result.Error);
        Assert.False(Directory.Exists(Path.Combine(fixture.TeamsRoot, "Business", "Launch-2027")));
    }

    /// <summary><c>EnsureProject</c> still creates a Project no rename mentions.</summary>
    [Fact]
    public void EnsureProject_UnrelatedName_IsCreated()
    {
        using Fixture fixture = new();
        fixture.Renames.Complete(fixture.Renames.Begin(TeamRenameKind.Project, "Business", "Launch", "Launch-2027"));
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();
        Assert.True(fixture.Resolver.TryResolve("teams", "Business", out LibraryPath? teamFolder, out _));

        LibraryResult<LibraryPath> result = provisioner.EnsureProject(teamFolder, "Retro");

        Assert.Null(result.Error);
        Assert.True(Directory.Exists(Path.Combine(fixture.TeamsRoot, "Business", "Retro")));
    }

    /// <summary><c>EnsureProjectIn</c> refuses the old name of a renamed Project and creates nothing.</summary>
    [Fact]
    public void EnsureProjectIn_ReservedName_IsRefused()
    {
        using Fixture fixture = new();
        fixture.Renames.Complete(fixture.Renames.Begin(TeamRenameKind.Project, "Business", "Launch", "Launch-2027"));
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();

        LibraryResult<LibraryPath> result = provisioner.EnsureProjectIn("Business", "Launch");

        Assert.Null(result.Value);
        Assert.Equal(Refusal, result.Error);
        Assert.False(Directory.Exists(Path.Combine(fixture.TeamsRoot, "Business", "Launch")));
    }

    /// <summary><c>EnsureProjectIn</c> still creates a Project no rename mentions.</summary>
    [Fact]
    public void EnsureProjectIn_UnrelatedName_IsCreated()
    {
        using Fixture fixture = new();
        fixture.Renames.Complete(fixture.Renames.Begin(TeamRenameKind.Project, "Business", "Launch", "Launch-2027"));
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();

        LibraryResult<LibraryPath> result = provisioner.EnsureProjectIn("Business", "Retro");

        Assert.Null(result.Error);
        Assert.True(Directory.Exists(Path.Combine(fixture.TeamsRoot, "Business", "Retro")));
    }

    /// <summary>A temporary data directory with a <c>Business</c> Team folder, the stores a provisioner needs and a real rename journal.</summary>
    private sealed class Fixture : IDisposable
    {
        private readonly TempDataDir dir = new();
        private readonly LibraryRootStore roots;
        private readonly TaskStore tasks;
        private readonly TeamCatalog catalog;

        /// <summary>Creates <c>Teams/Business</c> and <c>Agents/</c>, then the stores.</summary>
        public Fixture()
        {
            Directory.CreateDirectory(Path.Combine(this.TeamsRoot, "Business"));
            Directory.CreateDirectory(Path.Combine(this.dir.Path, "Agents"));
            IOptions<TeamOptions> options = this.dir.Options();
            AgentPaths paths = new(options);
            this.Personas = new PersonaStore(paths, new PersonaModelStore(options), new PersonaEffortStore(options), new PersonaWorkModeStore(options), NullLogger<PersonaStore>.Instance);
            this.roots = new LibraryRootStore(options, paths, NullLogger<LibraryRootStore>.Instance);
            this.Resolver = new LibraryPathResolver(this.roots, options, NullLogger<LibraryPathResolver>.Instance);
            this.tasks = TestTaskStore.CreateTaskStore(this.dir, this.Personas);
            this.catalog = new TeamCatalog(this.Personas, this.tasks);
            this.Renames = new TeamRenameStore(options);
        }

        /// <summary>The store the provisioner reads Personas from.</summary>
        public PersonaStore Personas { get; }

        /// <summary>The resolver the provisioner validates with.</summary>
        public LibraryPathResolver Resolver { get; }

        /// <summary>The real rename journal.</summary>
        public TeamRenameStore Renames { get; }

        /// <summary>The absolute path of the Teams root.</summary>
        public string TeamsRoot => Path.Combine(this.dir.Path, "Teams");

        /// <summary>Builds the provisioner under test with the rename journal.</summary>
        public TeamFolderProvisioner CreateProvisioner() =>
            new(this.Personas, this.roots, this.Resolver, NullLogger<TeamFolderProvisioner>.Instance, catalog: this.catalog, renames: this.Renames);

        /// <inheritdoc />
        public void Dispose()
        {
            this.catalog.Dispose();
            this.tasks.Dispose();
            this.Personas.Dispose();
            this.dir.Dispose();
        }
    }
}
