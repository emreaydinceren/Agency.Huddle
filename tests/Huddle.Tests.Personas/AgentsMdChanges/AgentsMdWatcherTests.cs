using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.AgentsMdChanges;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Waiting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.AgentsMdChanges;

/// <summary>
/// Pins <see cref="AgentsMdWatcher"/> (AgentsMdChanges Spec §6.3): a startup sweep, a tick that reads only files whose size or
/// modified time moved, Team-catalog enumeration only, a loop that survives an exception, a safe stop, and no run with Adapters off.
/// Ticks are driven through <c>TickAsync</c>; one test drives the real loop on a <see cref="FiringTimeProvider"/>.
/// </summary>
public sealed class AgentsMdWatcherTests : IDisposable
{
    private const string Home = "Home/AGENTS.md";

    private readonly AgentsMdLedgerFixture fixture = new();
    private readonly FiringTimeProvider clock = new();
    private readonly FakeCatalog catalog = new();

    private string DataDir => this.fixture.Options.Value.DataDir;

    private string TeamsRoot => Path.Combine(this.DataDir, this.fixture.Options.Value.Teams.Dir);

    /// <inheritdoc />
    public void Dispose() => this.fixture.Dispose();

    /// <summary>Builds the watcher over the fixture's ledger.</summary>
    /// <param name="options">The options to run under; the fixture's by default.</param>
    private AgentsMdWatcher NewWatcher(IOptions<TeamOptions>? options = null) =>
        new(this.fixture.Ledger, this.catalog, options ?? this.fixture.Options, NullLogger<AgentsMdWatcher>.Instance, this.clock);

    /// <summary>Writes a level file and stamps it with the clock's time, then moves the clock on so the next save differs.</summary>
    /// <param name="relative">The path below the Teams root, with <c>/</c> separators.</param>
    /// <param name="text">The text to write.</param>
    private void Save(string relative, string text)
    {
        string path = Path.Combine(this.TeamsRoot, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? this.TeamsRoot);
        File.WriteAllText(path, text);
        File.SetLastWriteTimeUtc(path, this.clock.GetUtcNow().UtcDateTime);
        this.clock.Advance(TimeSpan.FromSeconds(1));
    }

    /// <summary>Lists the ledger files on disk.</summary>
    private string[] LedgerFiles() =>
        Directory.GetFiles(Path.Combine(this.DataDir, "agents-md-ledger"), "*.json");

    /// <summary>A file edited while Huddle was closed is a pending edit as soon as the watcher starts, without any tick.</summary>
    [Fact]
    public async Task Start_SweepFindsAnEditMadeWhileClosed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.catalog.Teams = [new TeamSummary("Home", [], [], true)];
        this.Save(Home, "# Rules\n\nBe kind.\n");
        _ = this.fixture.Ledger.Observe(new AgentsMdFile(AgentsMdLevel.Team, "Home", null, Path.Combine(this.TeamsRoot, Home), Home));
        this.Save(Home, "# Rules\n\nBe terse and kind.\n");
        using AgentsMdWatcher watcher = this.NewWatcher();

        await watcher.StartAsync(ct);
        await Eventually.UntilAsync(() => this.fixture.Ledger.PendingEdits().Count == 1, ct, TestBudgets.Slow);
        await watcher.StopAsync(ct);

        Assert.Equal(Home, Assert.Single(this.fixture.Ledger.PendingEdits()).File.RelativePath);
    }

    /// <summary>A file whose size and modified time are unchanged is not read again, and one whose time moved is.</summary>
    [Fact]
    public async Task Tick_ReadsOnlyMovedFiles()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.catalog.Teams = [new TeamSummary("Home", [], [], true)];
        this.Save(Home, "# Rules\n\nAAAA\n");
        using AgentsMdWatcher watcher = this.NewWatcher();
        await watcher.TickAsync(ct);
        string path = Path.Combine(this.TeamsRoot, Home);
        DateTime stamp = File.GetLastWriteTimeUtc(path);

        File.WriteAllText(path, "# Rules\n\nBBBB\n");
        File.SetLastWriteTimeUtc(path, stamp);
        await watcher.TickAsync(ct);
        Assert.Empty(this.fixture.Ledger.PendingEdits());

        File.SetLastWriteTimeUtc(path, stamp.AddSeconds(5));
        await watcher.TickAsync(ct);
        Assert.Single(this.fixture.Ledger.PendingEdits());
    }

    /// <summary>A folder that is not a Team (Teams/memory) is never observed, so it spends no ledger entry.</summary>
    [Fact]
    public async Task Tick_NonTeamFolderIsIgnored()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.catalog.Teams = [new TeamSummary("Home", [], [], true)];
        this.Save(Home, "# Rules\n\nOne.\n");
        this.Save("memory/AGENTS.md", "# Memory\n\nA.\n");
        using AgentsMdWatcher watcher = this.NewWatcher();
        await watcher.TickAsync(ct);

        this.Save("memory/AGENTS.md", "# Memory\n\nB, longer.\n");
        await watcher.TickAsync(ct);

        Assert.Empty(this.fixture.Ledger.PendingEdits());
        Assert.Single(this.LedgerFiles());
    }

    /// <summary>A tick that throws is logged and the loop runs the next tick.</summary>
    [Fact]
    public async Task Tick_ExceptionDoesNotEndTheLoop()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.catalog.Teams = [new TeamSummary("Home", [], [], true)];
        this.catalog.ThrowFirstRead = true;
        using AgentsMdWatcher watcher = this.NewWatcher();

        await watcher.StartAsync(ct);
        await Eventually.UntilAsync(() => this.clock.ScheduledTimerCount == 1, ct, TestBudgets.Slow);
        await Eventually.UntilAsync(() => this.catalog.Reads >= 1, ct, TestBudgets.Slow);
        this.clock.Advance(TimeSpan.FromSeconds(10));
        await Eventually.UntilAsync(() => this.catalog.Reads >= 2, ct, TestBudgets.Slow);
        this.clock.Advance(TimeSpan.FromSeconds(10));
        await Eventually.UntilAsync(() => this.catalog.Reads >= 3, ct, TestBudgets.Slow);
        await watcher.StopAsync(ct);

        Assert.True(this.catalog.Reads >= 3);
    }

    /// <summary>Stopping on one thread while disposing on another neither throws nor hangs.</summary>
    [Fact]
    public async Task StopAndDisposeOnTwoThreads_DoNotThrow()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.catalog.Teams = [new TeamSummary("Home", [], [], true)];
        AgentsMdWatcher watcher = this.NewWatcher();
        await watcher.StartAsync(ct);
        await Eventually.UntilAsync(() => this.clock.ScheduledTimerCount == 1, ct, TestBudgets.Slow);
        using CancellationTokenSource bound = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bound.CancelAfter(TestBudgets.Slow);

        Task stop = Task.Run(() => watcher.StopAsync(bound.Token), bound.Token);
        Task dispose = Task.Run(watcher.Dispose, bound.Token);
        await Task.WhenAll(stop, dispose);

        Assert.True(stop.IsCompletedSuccessfully && dispose.IsCompletedSuccessfully);
    }

    /// <summary>With Adapters off the watcher arms no timer and writes no ledger folder anywhere.</summary>
    [Fact]
    public async Task AdaptersOff_DoesNotRun_AndWritesNoLedger()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir offDir = new();
        IOptions<TeamOptions> off = offDir.Options(team => team.Acp.Enabled = false);
        this.catalog.Teams = [new TeamSummary("Home", [], [], true)];
        string offHome = Path.Combine(off.Value.DataDir, off.Value.Teams.Dir, Home);
        Directory.CreateDirectory(Path.GetDirectoryName(offHome) ?? offDir.Path);
        await File.WriteAllTextAsync(offHome, "# Rules\n\nOne.\n", ct);
        using AgentsMdWatcher watcher = this.NewWatcher(off);

        await watcher.StartAsync(ct);
        // A negative has no event to wait for: a running loop yields, sweeps and arms its timer within moments, so hold for one short budget.
        await Task.Delay(TestBudgets.Short, ct);
        this.clock.Advance(TimeSpan.FromSeconds(60));
        await watcher.StopAsync(ct);

        Assert.Equal(0, this.clock.ScheduledTimerCount);
        Assert.False(Directory.Exists(Path.Combine(off.Value.DataDir, "agents-md-ledger")));
        Assert.False(Directory.Exists(Path.Combine(this.DataDir, "agents-md-ledger")));
    }

    /// <summary>A deleted file and a folder at a level path do not stop the others from being observed.</summary>
    [Fact]
    public async Task Tick_DeletedFile_Observed_OthersObserved()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.catalog.Teams = [new TeamSummary("Home", ["p1", "p2"], [], true)];
        this.Save("AGENTS.md", "# Wide\n\nOne.\n");
        this.Save(Home, "# Team\n\nOne.\n");
        this.Save("Home/p1/AGENTS.md", "# P1\n\nOne.\n");
        this.Save("Home/p2/AGENTS.md", "# P2\n\nOne.\n");
        using AgentsMdWatcher watcher = this.NewWatcher();
        await watcher.TickAsync(ct);

        File.Delete(Path.Combine(this.TeamsRoot, "AGENTS.md"));
        this.Save(Home, "# Team\n\nTwo, longer.\n");
        string p1 = Path.Combine(this.TeamsRoot, "Home/p1/AGENTS.md");
        File.Delete(p1);
        Directory.CreateDirectory(p1);
        this.Save("Home/p2/AGENTS.md", "# P2\n\nTwo, longer.\n");
        await watcher.TickAsync(ct);

        Assert.Equal(["AGENTS.md", Home, "Home/p2/AGENTS.md"], this.fixture.Ledger.PendingEdits().Select(static edit => edit.File.RelativePath).Order(StringComparer.Ordinal));
    }

    /// <summary>A hand-driven Team catalog that can throw on its first reads and counts them.</summary>
    private sealed class FakeCatalog : ITeamCatalog
    {
        private int reads;
        private Action? changed;

        /// <summary>The Teams the catalog reports.</summary>
        public IReadOnlyList<TeamSummary> Teams
        {
            get
            {
                int read = Interlocked.Increment(ref this.reads);
                if (read == 1 && this.ThrowFirstRead)
                {
                    throw new IOException("The catalog is locked.");
                }

                return this.Backing;
            }

            set => this.Backing = value;
        }

        /// <summary>How many reads of <see cref="Teams"/> have happened.</summary>
        public int Reads => Volatile.Read(ref this.reads);

        /// <summary>Whether the first read throws, so a loop shows it runs on.</summary>
        public bool ThrowFirstRead { get; set; }

        private IReadOnlyList<TeamSummary> Backing { get; set; } = [];

        /// <inheritdoc />
        public event Action? Changed
        {
            add => this.changed += value;
            remove => this.changed -= value;
        }

        /// <inheritdoc />
        public TeamSummary? Find(string team) => this.Backing.FirstOrDefault(t => string.Equals(t.Name, team, StringComparison.OrdinalIgnoreCase));

        /// <inheritdoc />
        public bool ProjectExists(string team, string project) => this.Find(team)?.Projects.Contains(project, StringComparer.OrdinalIgnoreCase) ?? false;
    }
}
