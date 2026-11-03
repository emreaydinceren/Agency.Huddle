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
/// Pins the reserved Project names on the Library side (Second Brain, task 1.4):
/// <see cref="TeamFolderProvisioner.EnsureProject"/> refuses <c>outputs</c>, accepts an ordinary name,
/// and <see cref="LibraryPathResolver"/> classifies <c>raw</c> as a plain folder.
/// </summary>
public sealed class EnsureProjectReservedTests
{
    /// <summary>Creating Project <c>outputs</c> (any case) fails with exactly the outputs text and creates no folder.</summary>
    [Theory]
    [InlineData("outputs")]
    [InlineData("OUTPUTS")]
    public void EnsureProject_Outputs_RefusedWithExactlyOutputsReservedProblem(string project)
    {
        using Fixture fixture = new("Home");
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();
        Assert.True(fixture.Resolver.TryResolve("teams", "Home", out LibraryPath? teamFolder, out _));

        LibraryResult<LibraryPath> result = provisioner.EnsureProject(teamFolder!, project);

        Assert.Null(result.Value);
        Assert.Equal(TeamNames.OutputsReservedProblem, result.Error);
        Assert.Empty(Directory.GetFileSystemEntries(Path.Combine(fixture.TeamsRoot, "Home")));
    }

    /// <summary>Creating an ordinary Project succeeds and makes its folder.</summary>
    [Fact]
    public void EnsureProject_Repairs_SucceedsAndCreatesFolder()
    {
        using Fixture fixture = new("Home");
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();
        Assert.True(fixture.Resolver.TryResolve("teams", "Home", out LibraryPath? teamFolder, out _));

        LibraryResult<LibraryPath> result = provisioner.EnsureProject(teamFolder!, "Repairs");

        Assert.Null(result.Error);
        Assert.NotNull(result.Value);
        Assert.True(Directory.Exists(Path.Combine(fixture.TeamsRoot, "Home", "Repairs")));
    }

    /// <summary>The resolver treats <c>Teams/Home/raw</c> as a plain folder and <c>Teams/Home/Repairs</c> as a Project folder (expected green already: the resolver landed with <c>IsReservedProjectName</c>).</summary>
    [Theory]
    [InlineData("Home/raw", LibraryNodeRole.Folder)]
    [InlineData("Home/Repairs", LibraryNodeRole.ProjectFolder)]
    public void Resolve_UnderTeam_ClassifiesReservedAndOrdinalProjects(string relativePath, LibraryNodeRole expected)
    {
        using Fixture fixture = new("Home");

        bool resolved = fixture.Resolver.TryResolve("teams", relativePath, out LibraryPath? path, out _);

        Assert.True(resolved);
        Assert.Equal(expected, path!.Role);
    }

    /// <summary>A real data folder with the Persona, Library and catalog collaborators the provisioner needs.</summary>
    private sealed class Fixture : IDisposable
    {
        private readonly TempDataDir dir;
        private readonly PersonaStore personas;
        private readonly LibraryRootStore roots;
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
            this.Resolver = new LibraryPathResolver(this.roots, options, NullLogger<LibraryPathResolver>.Instance);
            this.tasks = TestTaskStore.CreateTaskStore(this.dir, this.personas);
            this.catalog = new TeamCatalog(this.personas, this.tasks);
        }

        public LibraryPathResolver Resolver { get; }

        public string TeamsRoot => Path.Combine(this.dir.Path, "Teams");

        public TeamFolderProvisioner CreateProvisioner() =>
            new(this.personas, this.roots, this.Resolver, NullLogger<TeamFolderProvisioner>.Instance, catalog: this.catalog);

        public void Dispose()
        {
            this.catalog.Dispose();
            this.tasks.Dispose();
            this.personas.Dispose();
            this.dir.Dispose();
        }
    }
}
