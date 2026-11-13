using Agency.Huddle.App.AgentsMdChanges;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Teams;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.AgentsMdChanges;

/// <summary>
/// <see cref="AgentsMdWatcher"/> and a Team Rename (Spec 4, Ledgers): while a rename of a Team is Pending its folder has moved but the
/// catalog still names it, so a poll must not read the vanished AGENTS.md as a deletion; once the rename is Complete the watcher
/// behaves as before.
/// </summary>
public sealed class AgentsMdWatcherRenameTests : IDisposable
{
    private const string Home = "Home/AGENTS.md";

    private readonly AgentsMdLedgerFixture fixture = new();
    private readonly FiringTimeProvider clock = new();
    private readonly FakeTeamCatalog catalog = new();
    private readonly TeamRenameStore renames;

    /// <summary>Builds the journal over the fixture's data folder and lists the Team <c>Home</c> in the catalog.</summary>
    public AgentsMdWatcherRenameTests()
    {
        this.renames = new TeamRenameStore(this.fixture.Options);
        this.catalog.Teams = [new TeamSummary("Home", [], [], true)];
    }

    private string TeamsRoot => Path.Combine(this.fixture.Options.Value.DataDir, this.fixture.Options.Value.Teams.Dir);

    /// <inheritdoc />
    public void Dispose() => this.fixture.Dispose();

    private AgentsMdWatcher NewWatcher() =>
        new(this.fixture.Ledger, this.catalog, this.fixture.Options, NullLogger<AgentsMdWatcher>.Instance, this.clock, this.renames);

    private void MoveHomeAway()
    {
        Directory.Move(Path.Combine(this.TeamsRoot, "Home"), Path.Combine(this.TeamsRoot, "Hub"));
    }

    private async Task<AgentsMdWatcher> ObservedAsync(CancellationToken ct)
    {
        string path = Path.Combine(this.TeamsRoot, Home);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path) ?? this.TeamsRoot);
        File.WriteAllText(path, "# Team\n\nOne.\n");
        AgentsMdWatcher watcher = this.NewWatcher();
        await watcher.TickAsync(ct);
        return watcher;
    }

    /// <summary>With a rename of the Team Pending, a poll after the folder moved reports no deletion of its AGENTS.md.</summary>
    [Fact]
    public async Task Tick_PendingRename_DoesNotReportTheMovedFileAsDeleted()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using AgentsMdWatcher watcher = await this.ObservedAsync(ct);
        _ = this.renames.Begin(TeamRenameKind.Team, "Home", "Home", "Hub");
        this.MoveHomeAway();

        await watcher.TickAsync(ct);

        Assert.DoesNotContain(Home, this.fixture.Ledger.PendingEdits().Select(static edit => edit.File.RelativePath));
    }

    /// <summary>After the rename is Complete the same missing file is a deletion again, as without a rename.</summary>
    [Fact]
    public async Task Tick_AfterComplete_BehavesAsToday()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using AgentsMdWatcher watcher = await this.ObservedAsync(ct);
        long id = this.renames.Begin(TeamRenameKind.Team, "Home", "Home", "Hub");
        this.MoveHomeAway();
        await watcher.TickAsync(ct);

        this.renames.Complete(id);
        await watcher.TickAsync(ct);

        Assert.Contains(Home, this.fixture.Ledger.PendingEdits().Select(static edit => edit.File.RelativePath));
    }
}
