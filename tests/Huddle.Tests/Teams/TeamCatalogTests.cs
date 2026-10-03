using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp;
using Agency.Huddle.Tests.Tasks;
using Agency.Huddle.Tests.Ui;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// Functional tests for the live <see cref="TeamCatalog"/> service (Spec §6.1): a real
/// <see cref="PersonaStore"/> and <see cref="TaskStore"/> over one temporary data directory, the
/// change event both sources drive, and the DI registration. Every definition file and folder a row
/// needs at start is seeded BEFORE the stores are constructed. The pure projection is pinned by
/// <see cref="TeamCatalogBuildTests"/>.
/// </summary>
public sealed class TeamCatalogTests
{
    /// <summary>With no Personas and no folders the catalog lists no Teams.</summary>
    [Fact]
    public void Teams_WithNoPersonasAndNoFolders_IsEmpty()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore tasks = TestTaskStore.CreateTaskStore(dir, personas);
        using TeamCatalog catalog = new(personas, tasks);

        Assert.Empty(catalog.Teams);
    }

    /// <summary>The catalog lists every Persona label and every Team folder, with Projects and Members, sorted by Name.</summary>
    [Fact]
    public void Teams_AtStart_ListsPersonaLabelsAndFolders()
    {
        using TempDataDir dir = new();
        WritePersona(dir, "Nova", "Marketing, Ops");
        WritePersona(dir, "Ada", "Ops");
        CreateFolder(dir, "Ops", "Beta");
        CreateFolder(dir, "Ops", "Alpha");
        CreateFolder(dir, "Research");
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore tasks = TestTaskStore.CreateTaskStore(dir, personas);
        using TeamCatalog catalog = new(personas, tasks);

        string[] expected =
        [
            "Marketing|projects=|members=Nova|folder=False",
            "Ops|projects=Alpha,Beta|members=Ada,Nova|folder=True",
            "Research|projects=|members=|folder=True"
        ];
        Assert.Equal(expected, Describe(catalog.Teams));
    }

    /// <summary>A Persona that gains a Team label through <see cref="PersonaStore.Update"/> makes the catalog raise <c>Changed</c> exactly once and list the new Team.</summary>
    [Fact]
    public void Changed_WhenAPersonaGainsALabel_FiresOnceAndListsTheTeam()
    {
        using TempDataDir dir = new();
        WritePersona(dir, "Nova", teams: null);
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore tasks = TestTaskStore.CreateTaskStore(dir, personas);
        using TeamCatalog catalog = new(personas, tasks);
        Assert.Empty(catalog.Teams);
        int raised = 0;
        void Count() => Interlocked.Increment(ref raised);
        catalog.Changed += Count;

        personas.Update("Nova", PersonaText("Nova", "Ops"), model: null, effort: null, workMode: null);

        // Stop counting the instant Update returns: Update's own write trips the PersonaStore file
        // watcher, which raises a SECOND PersonasChanged after its 500 ms debounce (see
        // PersonaStoreTests.Update_RaisesPersonasChangedOnce). This row pins the synchronous raise.
        catalog.Changed -= Count;

        Assert.Equal(1, raised);
        Assert.Equal(["Ops|projects=|members=Nova|folder=False"], Describe(catalog.Teams));
    }

    /// <summary>A Team folder created on disk reaches the catalog through the TaskStore's watcher: <c>Changed</c> fires and the Team's <c>HasFolder</c> flips to true.</summary>
    [Fact]
    public async Task Changed_WhenATeamFolderAppears_FiresAndSetsHasFolder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        WritePersona(dir, "Nova", "Research");
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore tasks = TestTaskStore.CreateTaskStore(dir, personas);
        using TeamCatalog catalog = new(personas, tasks);
        Assert.Equal(["Research|projects=|members=Nova|folder=False"], Describe(catalog.Teams));
        int raised = 0;
        void Count() => Interlocked.Increment(ref raised);
        catalog.Changed += Count;

        CreateFolder(dir, "Research");

        // The watcher plus a 500 ms debounce drive this, so poll: never a fixed delay. The catalog
        // swaps its snapshot BEFORE it raises Changed, so once Changed has fired Teams is current.
        await WaitForAsync(() => Volatile.Read(ref raised) > 0, ct);
        catalog.Changed -= Count;

        Assert.Equal(["Research|projects=|members=Nova|folder=True"], Describe(catalog.Teams));
    }

    /// <summary>After <see cref="TeamCatalog.Dispose"/> neither source raises <c>Changed</c>: not a Persona update, and not the TaskStore's own change signal.</summary>
    [Fact]
    public async Task Changed_AfterDispose_NeverFires()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        WritePersona(dir, "Nova", teams: null);
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore tasks = TestTaskStore.CreateTaskStore(dir, personas);
        TeamCatalog catalog = new(personas, tasks);
        int raised = 0;
        void Count() => Interlocked.Increment(ref raised);
        catalog.Changed += Count;
        int taskSignals = 0;
        void CountTaskSignal() => Interlocked.Increment(ref taskSignals);
        tasks.IndexChanged += CountTaskSignal;

        catalog.Dispose();
        personas.Update("Nova", PersonaText("Nova", "Ops"), model: null, effort: null, workMode: null);
        CreateFolder(dir, "Research");

        // Prove the TaskStore signal DID fire after the dispose, so silence from the catalog is
        // the unsubscribe and not a watcher that never got going.
        await WaitForAsync(() => Volatile.Read(ref taskSignals) > 0, ct);
        tasks.IndexChanged -= CountTaskSignal;

        Assert.Equal(0, Volatile.Read(ref raised));
    }

    /// <summary><see cref="ITeamCatalog.Find"/> matches a Team name ignoring case, returns the display spelling, and returns null for an unknown Team.</summary>
    [Fact]
    public void Find_IgnoresCase_AndReturnsNullForUnknown()
    {
        using TempDataDir dir = new();
        WritePersona(dir, "Nova", "Ops");
        CreateFolder(dir, "Ops", "Alpha");
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore tasks = TestTaskStore.CreateTaskStore(dir, personas);
        using TeamCatalog catalog = new(personas, tasks);

        foreach (string spelling in new[] { "Ops", "ops", "OPS", "oPs" })
        {
            TeamSummary? found = catalog.Find(spelling);

            Assert.NotNull(found);
            Assert.Equal(["Ops|projects=Alpha|members=Nova|folder=True"], Describe([found]));
        }

        Assert.Null(catalog.Find("Nope"));
    }

    /// <summary><see cref="ITeamCatalog.ProjectExists"/> ignores case on both the Team and the Project, and is false for an unknown Project or Team.</summary>
    [Fact]
    public void ProjectExists_IgnoresCase()
    {
        using TempDataDir dir = new();
        WritePersona(dir, "Nova", "Ops");
        CreateFolder(dir, "Ops", "Alpha");
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore tasks = TestTaskStore.CreateTaskStore(dir, personas);
        using TeamCatalog catalog = new(personas, tasks);

        bool[] actual =
        [
            catalog.ProjectExists("Ops", "Alpha"),
            catalog.ProjectExists("OPS", "aLPHA"),
            catalog.ProjectExists("ops", "ALPHA"),
            catalog.ProjectExists("Ops", "Gamma"),
            catalog.ProjectExists("Nope", "Alpha")
        ];

        Assert.Equal([true, true, true, false, false], actual);
    }

    /// <summary>A folder named <c>memory</c> under a Team is the shared memory folder, not a Project: it is neither listed nor found, in any case, while a real Project beside it still is.</summary>
    [Fact]
    public void ProjectExists_ReservedMemoryFolder_IsNotAProject()
    {
        using TempDataDir dir = new();
        WritePersona(dir, "Nova", "Ops");
        CreateFolder(dir, "Ops", "memory");
        CreateFolder(dir, "Ops", "Alpha");
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore tasks = TestTaskStore.CreateTaskStore(dir, personas);
        using TeamCatalog catalog = new(personas, tasks);

        bool[] actual =
        [
            catalog.ProjectExists("Ops", "memory"),
            catalog.ProjectExists("Ops", "MEMORY"),
            catalog.ProjectExists("Ops", "Alpha")
        ];

        Assert.Equal([false, false, true], actual);
        Assert.Equal(["Ops|projects=Alpha|members=Nova|folder=True"], Describe(catalog.Teams));
    }

    /// <summary>The DI container resolves <see cref="ITeamCatalog"/> to the same singleton instance as <see cref="TeamCatalog"/>, so the pages and the Dispose-owning host see one catalog.</summary>
    [Fact]
    public async Task Resolve_ITeamCatalog_ReturnsTheSameInstanceAsTeamCatalog()
    {
        await using TeamWebApplicationFactory factory = new();

        ITeamCatalog byInterface = factory.Services.GetRequiredService<ITeamCatalog>();
        TeamCatalog concrete = factory.Services.GetRequiredService<TeamCatalog>();

        Assert.Same(concrete, byInterface);
    }

    /// <summary>Projects each Team to a literal one-line description so a whole list can be asserted (the record's list members compare by reference).</summary>
    /// <param name="teams">The Teams to describe.</param>
    private static string[] Describe(IReadOnlyList<TeamSummary> teams) =>
        [.. teams.Select(static t => $"{t.Name}|projects={string.Join(',', t.Projects)}|members={string.Join(',', t.Members)}|folder={t.HasFolder}")];

    /// <summary>Minimal valid Persona definition text, with a <c>Teams:</c> line only when <paramref name="teams"/> is given.</summary>
    /// <param name="name">The Persona's Name; its lower-cased form is the Alias.</param>
    /// <param name="teams">The comma-separated Team labels, or null for none.</param>
    private static string PersonaText(string name, string? teams)
    {
        string teamsLine = teams is null ? string.Empty : $"Teams: {teams}\n";
        return $"---\nName: {name}\nTitle: {name}\nAlias: {name.ToLowerInvariant()}\n{teamsLine}---\nYou are {name}.";
    }

    /// <summary>Seeds a Persona definition file before the stores are constructed.</summary>
    /// <param name="dir">The temporary data directory.</param>
    /// <param name="name">The Persona's Name.</param>
    /// <param name="teams">The comma-separated Team labels, or null for none.</param>
    private static void WritePersona(TempDataDir dir, string name, string? teams) =>
        TestPersonaFiles.Write(new TeammatePaths(dir.Options()), name.ToLowerInvariant(), PersonaText(name, teams));

    /// <summary>Creates a Team folder, or a Project folder under it, beneath the Tasks scan root.</summary>
    /// <param name="dir">The temporary data directory.</param>
    /// <param name="segments">The Team name, then optionally the Project name.</param>
    private static void CreateFolder(TempDataDir dir, params string[] segments) =>
        Directory.CreateDirectory(Path.Combine([TestTaskStore.Root(dir), .. segments]));

    /// <summary>
    /// Polls <paramref name="condition"/> until it is true or a generous timeout elapses, for
    /// asserting on a <see cref="FileSystemWatcher"/>-driven side effect without a bare delay.
    /// Copied from <c>AppearanceStoreTests.WaitForAsync</c>.
    /// </summary>
    /// <param name="condition">Checked repeatedly until it returns <see langword="true"/>.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    private static async Task WaitForAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("Timed out waiting for the filesystem watcher to pick up the change.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }
    }
}
