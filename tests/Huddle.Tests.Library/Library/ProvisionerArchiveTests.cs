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
/// Pins Spec §6.7's provisioner rule (Tasks 114 and 116, corrections-12 items 10 and 11): an archived Team
/// (<c>Archive/&lt;T&gt;/_team.md</c>) never gets its folder back from a label, a create or a Project call, and the
/// folder gate keeps a label sync out of an archive move.
/// </summary>
public sealed class ProvisionerArchiveTests
{
    private static readonly TeamFile Sample = new(VersionedRef.None, null, null, null, string.Empty);

    /// <summary>A Persona still labelled <c>Home</c> while <c>Archive/Home/_team.md</c> exists gets no <c>Teams/Home</c> at start.</summary>
    [Fact]
    public async Task Start_LabelOfArchivedTeam_CreatesNoFolder()
    {
        using Fixture f = new();
        f.Archive("Home");
        f.Personas.Add(new PersonaIdentity("Nova", "Nova", "Nova", ["Home", "Other"]), "body");
        using TeamFolderProvisioner provisioner = f.CreateProvisioner();

        await provisioner.StartAsync(TestContext.Current.CancellationToken);

        Assert.False(Directory.Exists(Path.Combine(f.TeamsRoot, "Home")));
        Assert.True(Directory.Exists(Path.Combine(f.TeamsRoot, "Other")));
    }

    /// <summary>The match ignores case: label <c>home</c> against <c>Archive/HOME/_team.md</c> creates no folder.</summary>
    [Fact]
    public async Task Start_LabelDiffersByCaseFromArchivedTeam_CreatesNoFolder()
    {
        using Fixture f = new();
        f.Archive("HOME");
        f.Personas.Add(new PersonaIdentity("Nova", "Nova", "Nova", ["home"]), "body");
        using TeamFolderProvisioner provisioner = f.CreateProvisioner();

        await provisioner.StartAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain(Directory.GetDirectories(f.TeamsRoot), d => string.Equals(Path.GetFileName(d), "home", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>A Persona added after start with the archived label also creates no folder (the <c>PersonasChanged</c> path).</summary>
    [Fact]
    public async Task PersonasChanged_LabelOfArchivedTeam_CreatesNoFolder()
    {
        using Fixture f = new();
        f.Archive("Home");
        using TeamFolderProvisioner provisioner = f.CreateProvisioner();
        await provisioner.StartAsync(TestContext.Current.CancellationToken);

        f.Personas.Add(new PersonaIdentity("Nova", "Nova", "Nova", ["Home"]), "body");

        Assert.False(Directory.Exists(Path.Combine(f.TeamsRoot, "Home")));
    }

    /// <summary>An archived Project alone (<c>Archive/Home/Old/</c>, no <c>_team.md</c>) does not make the Team archived: the live Team's label still gets its folder.</summary>
    [Fact]
    public async Task Start_OnlyAProjectArchived_LabelStillGetsFolder()
    {
        using Fixture f = new();
        Directory.CreateDirectory(Path.Combine(f.ArchiveRoot, "Home", "Old"));
        f.Personas.Add(new PersonaIdentity("Nova", "Nova", "Nova", ["Home"]), "body");
        using TeamFolderProvisioner provisioner = f.CreateProvisioner();

        await provisioner.StartAsync(TestContext.Current.CancellationToken);

        Assert.True(Directory.Exists(Path.Combine(f.TeamsRoot, "Home")));
    }

    /// <summary><c>EnsureTeam</c> refuses a name whose archived twin differs only by case, with the archived-name text, and creates nothing.</summary>
    [Fact]
    public void EnsureTeam_ArchivedNameDifferingByCase_RefusedWithText()
    {
        using Fixture f = new();
        f.Archive("Home");
        using TeamFolderProvisioner provisioner = f.CreateProvisioner();

        LibraryResult<LibraryPath> result = provisioner.EnsureTeam("home");

        Assert.Null(result.Value);
        Assert.Equal("A Team named Home is archived. Unarchive it or pick another name.", result.Error);
        Assert.Empty(Directory.GetDirectories(f.TeamsRoot));
    }

    /// <summary><c>CreateTeam</c> refuses an archived name and creates neither a folder nor a file.</summary>
    [Fact]
    public void CreateTeam_ArchivedName_InvalidNameAndNothingCreated()
    {
        using Fixture f = new();
        f.Archive("Home");
        using TeamFolderProvisioner provisioner = f.CreateProvisioner();

        TeamCreateResult result = provisioner.CreateTeam("Home", Sample);

        Assert.Equal(TeamCreateOutcome.InvalidName, result.Outcome);
        Assert.Empty(Directory.GetDirectories(f.TeamsRoot));
    }

    /// <summary><c>EnsureProjectIn</c> for an archived Team name creates no <c>Teams/&lt;T&gt;</c> folder (a label-only Team that was archived).</summary>
    [Fact]
    public void EnsureProjectIn_ArchivedTeam_RefusedAndNothingCreated()
    {
        using Fixture f = new();
        f.Archive("Home");
        f.Personas.Add(new PersonaIdentity("Nova", "Nova", "Nova", ["Home"]), "body");
        using TeamFolderProvisioner provisioner = f.CreateProvisioner();

        LibraryResult<LibraryPath> result = provisioner.EnsureProjectIn("Home", "Launch");

        Assert.Null(result.Value);
        Assert.False(Directory.Exists(Path.Combine(f.TeamsRoot, "Home")));
    }

    /// <summary><c>EnsureProjectIn</c> refuses a Project name an archived Project of the live Team already holds, ignoring case.</summary>
    [Fact]
    public void EnsureProjectIn_NameOfArchivedProject_RefusedWithText()
    {
        using Fixture f = new("Home");
        Directory.CreateDirectory(Path.Combine(f.ArchiveRoot, "Home", "Old"));
        using TeamFolderProvisioner provisioner = f.CreateProvisioner();

        LibraryResult<LibraryPath> result = provisioner.EnsureProjectIn("Home", "old");

        Assert.Null(result.Value);
        Assert.Equal("A Project named Old is archived in Home. Unarchive it or pick another name.", result.Error);
        Assert.False(Directory.Exists(Path.Combine(f.TeamsRoot, "Home", "old")));
    }

    /// <summary>
    /// A label sync raised while an archive move holds the folder gate waits for it: with the live folder renamed
    /// away and the archive not yet in place, the sync must not recreate <c>Teams/Home</c> (item 11).
    /// </summary>
    [Fact]
    public async Task SyncFolders_WhileGateHeld_WaitsAndThenSeesTheArchive()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = new("Home");
        using TeamFolderProvisioner provisioner = f.CreateProvisioner();
        await provisioner.StartAsync(ct);
        Task syncing = Task.CompletedTask;
        bool finishedWhileHeld = false;

        provisioner.RunExclusive(() =>
        {
            // The move's middle: the live folder is gone and the archive has no _team.md yet.
            Directory.Delete(Path.Combine(f.TeamsRoot, "Home"));
            syncing = Task.Run(() => f.Personas.Add(new PersonaIdentity("Nova", "Nova", "Nova", ["Home"]), "body"), ct);
            finishedWhileHeld = syncing.Wait(TimeSpan.FromMilliseconds(300), ct);
            f.Archive("Home");
            return 0;
        });
        await syncing.WaitAsync(TimeSpan.FromSeconds(10), ct);

        Assert.False(finishedWhileHeld);
        Assert.False(Directory.Exists(Path.Combine(f.TeamsRoot, "Home")));
    }

    /// <summary>A real data folder with the Persona, Library and catalog collaborators the provisioner needs.</summary>
    private sealed class Fixture : IDisposable
    {
        private readonly TempDataDir dir;
        private readonly LibraryRootStore roots;
        private readonly LibraryPathResolver resolver;
        private readonly TaskStore tasks;

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
            this.Personas = new PersonaStore(paths, new PersonaModelStore(options), new PersonaEffortStore(options), new PersonaWorkModeStore(options), NullLogger<PersonaStore>.Instance);
            this.roots = new LibraryRootStore(options, paths, NullLogger<LibraryRootStore>.Instance);
            this.resolver = new LibraryPathResolver(this.roots, options, NullLogger<LibraryPathResolver>.Instance);
            this.tasks = TestTaskStore.CreateTaskStore(this.dir, this.Personas);
            this.Catalog = new TeamCatalog(this.Personas, this.tasks);
            this.ArchiveRoot = this.roots.Roots.Single(r => r.Kind == LibraryRootKind.Archive).FullPath;
            Directory.CreateDirectory(this.ArchiveRoot);
        }

        public PersonaStore Personas { get; }

        public TeamCatalog Catalog { get; }

        public string ArchiveRoot { get; }

        public string TeamsRoot => Path.Combine(this.dir.Path, "Teams");

        /// <summary>Makes <paramref name="name"/> an archived Team: <c>Archive/&lt;name&gt;/_team.md</c> exists.</summary>
        /// <param name="name">The folder's spelling.</param>
        public void Archive(string name)
        {
            string folder = Path.Combine(this.ArchiveRoot, name);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, TeamPaths.TeamFileName), Sample.Compose());
        }

        public TeamFolderProvisioner CreateProvisioner() =>
            new(this.Personas, this.roots, this.resolver, NullLogger<TeamFolderProvisioner>.Instance, catalog: this.Catalog);

        public void Dispose()
        {
            this.Catalog.Dispose();
            this.tasks.Dispose();
            this.Personas.Dispose();
            this.dir.Dispose();
        }
    }
}
