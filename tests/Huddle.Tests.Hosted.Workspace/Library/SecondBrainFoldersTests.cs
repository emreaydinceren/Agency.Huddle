using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Elicitation;
using Agency.Huddle.Tests.Tasks;

namespace Agency.Huddle.Tests.Library;

/// <summary>
/// Pins SB-R01 (Spec §6.1) on <see cref="TeamFolderProvisioner"/>: every Team folder (label sync, the
/// create path) and every Project folder gets <c>raw/</c>, <c>memory/</c> and <c>outputs/</c>, start-up
/// creates <c>Teams/memory/</c>, and a label that collides with a reserved Team name is skipped and logged.
/// These rows live in the Hosted.Workspace project because label sync is driven from its fixture.
/// </summary>
public sealed class SecondBrainFoldersTests
{
    private static readonly string[] SubFolders = ["memory", "outputs", "raw"];

    /// <summary>A synced Persona label <c>Home</c> gets <c>raw</c>, <c>memory</c> and <c>outputs</c> beneath its Team folder.</summary>
    [Fact]
    public async Task Start_LabelHome_CreatesThreeSubFolders()
    {
        using Fixture fixture = new();
        fixture.Personas.Add(new PersonaIdentity("Nova", "Nova", "Nova", ["Home"]), "body");
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();

        await provisioner.StartAsync(TestContext.Current.CancellationToken);

        Assert.Equal(SubFolders, Names(Path.Combine(fixture.TeamsRoot, "Home")));
    }

    /// <summary>A Persona added after start (the <see cref="PersonaStore.PersonasChanged"/> path) also gets the three sub-folders.</summary>
    [Fact]
    public async Task PersonasChanged_NewLabel_CreatesThreeSubFolders()
    {
        using Fixture fixture = new();
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();
        await provisioner.StartAsync(TestContext.Current.CancellationToken);

        fixture.Personas.Add(new PersonaIdentity("Nova", "Nova", "Nova", ["Home"]), "body");

        Assert.Equal(SubFolders, Names(Path.Combine(fixture.TeamsRoot, "Home")));
    }

    /// <summary>Creating a Team through <see cref="ITeamFolders.CreateTeam"/> gives it the three sub-folders next to <c>_team.md</c>.</summary>
    [Fact]
    public void CreateTeam_NewName_CreatesThreeSubFolders()
    {
        using Fixture fixture = new();
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();
        TeamFile file = new(new VersionedRef("development", "1"), null, "C:\\Work\\Alpha", "The alpha product", "\n");

        TeamCreateResult result = provisioner.CreateTeam("Alpha", file);

        Assert.Equal(TeamCreateOutcome.Created, result.Outcome);
        Assert.Equal(SubFolders, Names(Path.Combine(fixture.TeamsRoot, "Alpha")));
    }

    /// <summary><see cref="TeamFolderProvisioner.EnsureProject"/> for Home/Repairs creates <c>Repairs/raw|memory|outputs</c>.</summary>
    [Fact]
    public void EnsureProject_Repairs_CreatesThreeSubFolders()
    {
        using Fixture fixture = new("Home");
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();
        Assert.True(fixture.Resolver.TryResolve("teams", "Home", out LibraryPath? home, out _));

        LibraryResult<LibraryPath> result = provisioner.EnsureProject(home!, "Repairs");

        Assert.NotNull(result.Value);
        Assert.Equal(SubFolders, Names(Path.Combine(fixture.TeamsRoot, "Home", "Repairs")));
    }

    /// <summary>A second run of start-up, create and project calls throws nothing and leaves the folder set unchanged.</summary>
    [Fact]
    public async Task SecondRun_IsNoOp()
    {
        using Fixture fixture = new();
        fixture.Personas.Add(new PersonaIdentity("Nova", "Nova", "Nova", ["Home"]), "body");
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();
        await provisioner.StartAsync(TestContext.Current.CancellationToken);
        Assert.True(fixture.Resolver.TryResolve("teams", "Home", out LibraryPath? home, out _));
        Assert.NotNull(provisioner.EnsureProject(home!, "Repairs").Value);
        string[] before = AllDirectories(fixture.TeamsRoot);

        await provisioner.StartAsync(TestContext.Current.CancellationToken);
        LibraryResult<LibraryPath> again = provisioner.EnsureProject(home!, "Repairs");

        Assert.NotNull(again.Value);
        Assert.Equal(before, AllDirectories(fixture.TeamsRoot));
        Assert.Contains(Path.Combine(fixture.TeamsRoot, "Home", "raw"), before);
    }

    /// <summary>Start-up creates the shared <c>Teams/memory/</c> folder even with no Persona labels.</summary>
    [Fact]
    public async Task Start_CreatesTeamsMemoryFolder()
    {
        using Fixture fixture = new();
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();

        await provisioner.StartAsync(TestContext.Current.CancellationToken);

        Assert.True(Directory.Exists(Path.Combine(fixture.TeamsRoot, "memory")));
    }

    /// <summary>A label <c>Raw</c> collides with a reserved Team name: no <c>Teams/Raw/</c> folder, and the skip is logged with the reserved reason.</summary>
    [Fact]
    public async Task Start_LabelRaw_SkippedAndLogged()
    {
        RecordingLogger logger = new();
        using Fixture fixture = new(logger: logger);
        fixture.Personas.Add(new PersonaIdentity("Nova", "Nova", "Nova", ["Raw"]), "body");
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();

        await provisioner.StartAsync(TestContext.Current.CancellationToken);

        Assert.Empty(Directory.GetDirectories(fixture.TeamsRoot, "Raw", SearchOption.TopDirectoryOnly));
        Assert.Contains(logger.Messages, static message => message.Contains("That folder is reserved.", StringComparison.Ordinal));
    }

    /// <summary>A Human-made <c>Teams/Home/Raw/</c> (capital R) is reused: no second <c>raw/</c> appears beside it (corrections item 4).</summary>
    [Fact]
    public async Task Start_ExistingCapitalRaw_Reused()
    {
        using Fixture fixture = new("Home/Raw");
        fixture.Personas.Add(new PersonaIdentity("Nova", "Nova", "Nova", ["Home"]), "body");
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();

        await provisioner.StartAsync(TestContext.Current.CancellationToken);

        string[] raws = [.. Names(Path.Combine(fixture.TeamsRoot, "Home")).Where(static name => string.Equals(name, "raw", StringComparison.OrdinalIgnoreCase))];
        Assert.Equal(["Raw"], raws);
    }

    private static string[] Names(string directory) =>
        [.. Directory.GetDirectories(directory).Select(static entry => Path.GetFileName(entry)).Order(StringComparer.Ordinal)];

    private static string[] AllDirectories(string root) =>
        [.. Directory.GetDirectories(root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal)];

    /// <summary>An isolated persona store, root store and resolver over a fresh temp data directory with <c>Teams/</c> created.</summary>
    private sealed class Fixture : IDisposable
    {
        private readonly TempDataDir dir;
        private readonly ILogger<TeamFolderProvisioner> logger;

        /// <param name="seedFolders">Folders (relative to <c>Teams/</c>) created before the catalog is built.</param>
        /// <param name="logger">The provisioner's logger, or <see langword="null"/> for none.</param>
        public Fixture(string[]? seedFolders = null, ILogger? logger = null)
        {
            this.dir = new TempDataDir();
            this.logger = logger is null ? NullLogger<TeamFolderProvisioner>.Instance : new Forwarding(logger);
            Directory.CreateDirectory(Path.Combine(this.dir.Path, "Teams"));
            Directory.CreateDirectory(Path.Combine(this.dir.Path, "Agents"));
            foreach (string seed in seedFolders ?? [])
            {
                Directory.CreateDirectory(Path.Combine(this.dir.Path, "Teams", seed));
            }

            IOptions<TeamOptions> options = this.dir.Options();
            AgentPaths paths = new(options);
            this.Personas = new PersonaStore(paths, new PersonaModelStore(options), new PersonaEffortStore(options), new PersonaWorkModeStore(options), NullLogger<PersonaStore>.Instance);
            this.Roots = new LibraryRootStore(options, paths, NullLogger<LibraryRootStore>.Instance);
            this.Resolver = new LibraryPathResolver(this.Roots, options, NullLogger<LibraryPathResolver>.Instance);
            this.Tasks = TestTaskStore.CreateTaskStore(this.dir, this.Personas);
            this.Catalog = new TeamCatalog(this.Personas, this.Tasks);
        }

        /// <param name="seedFolder">One folder, relative to <c>Teams/</c>, created before the catalog is built.</param>
        public Fixture(string seedFolder)
            : this([seedFolder])
        {
        }

        public TaskStore Tasks { get; }

        public TeamCatalog Catalog { get; }

        public PersonaStore Personas { get; }

        public LibraryRootStore Roots { get; }

        public LibraryPathResolver Resolver { get; }

        public string TeamsRoot => Path.Combine(this.dir.Path, "Teams");

        public TeamFolderProvisioner CreateProvisioner() =>
            new(this.Personas, this.Roots, this.Resolver, this.logger, catalog: this.Catalog);

        public void Dispose()
        {
            this.Catalog.Dispose();
            this.Tasks.Dispose();
            this.Personas.Dispose();
            this.dir.Dispose();
        }
    }

    /// <summary>Adapts a non-generic logger to <see cref="ILogger{TCategoryName}"/> for the provisioner.</summary>
    private sealed class Forwarding(ILogger inner) : ILogger<TeamFolderProvisioner>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => inner.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => inner.IsEnabled(logLevel);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            inner.Log(logLevel, eventId, state, exception, formatter);
    }
}
