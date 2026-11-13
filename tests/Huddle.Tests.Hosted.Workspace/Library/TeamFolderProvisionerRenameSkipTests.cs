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
/// Pins Team Rename Spec R2 and §5.2: <see cref="TeamFolderProvisioner"/> does not create a Team folder
/// for a label whose rename is pending, so <c>Teams/Marketing/</c> is not re-created while members
/// still say it; once the row is completed it syncs as before. Uses the real
/// <see cref="TeamRenameStore"/> on the temporary data directory.
/// </summary>
public sealed class TeamFolderProvisionerRenameSkipTests
{
    /// <summary>While a rename of Marketing is pending, a PersonasChanged with labels still saying Marketing does not create <c>Teams/Marketing</c>.</summary>
    [Fact]
    public async Task PersonasChanged_PendingRename_DoesNotCreateTheOldFolder()
    {
        using Fixture fixture = new();
        _ = fixture.Renames.Begin(TeamRenameKind.Team, "Marketing", "Marketing", "Growth");
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();
        await provisioner.StartAsync(TestContext.Current.CancellationToken);

        _ = fixture.Personas.Add(new PersonaIdentity("Nova", "Nova", "Nova", ["Marketing"]), "body");

        Assert.False(Directory.Exists(Path.Combine(fixture.TeamsRoot, "Marketing")));
    }

    /// <summary>A label with a pending rename is skipped at start too (a crash mid-rename), while another label still gets its folder.</summary>
    [Fact]
    public async Task Start_PendingRename_SkipsOnlyThatLabel()
    {
        using Fixture fixture = new();
        _ = fixture.Personas.Add(new PersonaIdentity("Nova", "Nova", "Nova", ["Marketing", "Sales"]), "body");
        _ = fixture.Renames.Begin(TeamRenameKind.Team, "Marketing", "Marketing", "Growth");
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();

        await provisioner.StartAsync(TestContext.Current.CancellationToken);

        Assert.False(Directory.Exists(Path.Combine(fixture.TeamsRoot, "Marketing")));
        Assert.True(Directory.Exists(Path.Combine(fixture.TeamsRoot, "Sales")));
    }

    /// <summary>After the row is completed, the next sync behaves as today and creates a folder for a label that has none.</summary>
    [Fact]
    public async Task PersonasChanged_RenameCompleted_CreatesTheFolderAgain()
    {
        using Fixture fixture = new();
        long id = fixture.Renames.Begin(TeamRenameKind.Team, "Marketing", "Marketing", "Growth");
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();
        await provisioner.StartAsync(TestContext.Current.CancellationToken);
        _ = fixture.Personas.Add(new PersonaIdentity("Nova", "Nova", "Nova", ["Marketing"]), "body");
        Assert.False(Directory.Exists(Path.Combine(fixture.TeamsRoot, "Marketing")));

        fixture.Renames.Complete(id);
        _ = fixture.Personas.Add(new PersonaIdentity("Bo", "Bo", "Bo", ["Marketing"]), "body");

        Assert.True(Directory.Exists(Path.Combine(fixture.TeamsRoot, "Marketing")));
    }

    /// <summary>A temporary data directory with the stores a provisioner needs and a real rename journal.</summary>
    private sealed class Fixture : IDisposable
    {
        private readonly TempDataDir dir = new();
        private readonly LibraryRootStore roots;
        private readonly LibraryPathResolver resolver;
        private readonly TaskStore tasks;
        private readonly TeamCatalog catalog;

        /// <summary>Creates <c>Teams/</c> and <c>Agents/</c>, then the stores.</summary>
        public Fixture()
        {
            Directory.CreateDirectory(this.TeamsRoot);
            Directory.CreateDirectory(Path.Combine(this.dir.Path, "Agents"));
            IOptions<TeamOptions> options = this.dir.Options();
            AgentPaths paths = new(options);
            this.Personas = new PersonaStore(paths, new PersonaModelStore(options), new PersonaEffortStore(options), new PersonaWorkModeStore(options), NullLogger<PersonaStore>.Instance);
            this.roots = new LibraryRootStore(options, paths, NullLogger<LibraryRootStore>.Instance);
            this.resolver = new LibraryPathResolver(this.roots, options, NullLogger<LibraryPathResolver>.Instance);
            this.tasks = TestTaskStore.CreateTaskStore(this.dir, this.Personas);
            this.catalog = new TeamCatalog(this.Personas, this.tasks);
            this.Renames = new TeamRenameStore(options);
        }

        /// <summary>The store the provisioner reads Personas from.</summary>
        public PersonaStore Personas { get; }

        /// <summary>The real rename journal.</summary>
        public TeamRenameStore Renames { get; }

        /// <summary>The absolute path of the Teams root.</summary>
        public string TeamsRoot => Path.Combine(this.dir.Path, "Teams");

        /// <summary>Builds the provisioner under test with the rename journal.</summary>
        public TeamFolderProvisioner CreateProvisioner() =>
            new(this.Personas, this.roots, this.resolver, NullLogger<TeamFolderProvisioner>.Instance, catalog: this.catalog, renames: this.Renames);

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
