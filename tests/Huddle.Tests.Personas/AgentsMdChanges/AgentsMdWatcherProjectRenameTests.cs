using Agency.Huddle.App.AgentsMdChanges;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Teams;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.AgentsMdChanges;

/// <summary>
/// <see cref="AgentsMdWatcher"/> and a Project rename (Team Rename Spec 5.5): while a rename of a Project is Pending its folder has
/// moved, so a poll must read neither the old AGENTS.md as removed nor the new one as added.
/// </summary>
public sealed class AgentsMdWatcherProjectRenameTests : IDisposable
{
    private const string OldFile = "Home/Launch/AGENTS.md";
    private const string NewFile = "Home/Launch-2027/AGENTS.md";

    private readonly AgentsMdLedgerFixture fixture = new();
    private readonly FiringTimeProvider clock = new();
    private readonly FakeTeamCatalog catalog = new();
    private readonly TeamRenameStore renames;

    /// <summary>Builds the journal over the fixture's data folder and lists the Team <c>Home</c> in the catalog.</summary>
    public AgentsMdWatcherProjectRenameTests()
    {
        this.renames = new TeamRenameStore(this.fixture.Options);
        this.catalog.Teams = [new TeamSummary("Home", [], [], true)];
    }

    private string TeamsRoot => Path.Combine(this.fixture.Options.Value.DataDir, this.fixture.Options.Value.Teams.Dir);

    /// <inheritdoc />
    public void Dispose() => this.fixture.Dispose();

    private AgentsMdWatcher NewWatcher() =>
        new(this.fixture.Ledger, this.catalog, this.fixture.Options, NullLogger<AgentsMdWatcher>.Instance, this.clock, this.renames);

    private async Task<AgentsMdWatcher> ObservedAsync(CancellationToken ct)
    {
        string path = Path.Combine(this.TeamsRoot, OldFile);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path) ?? this.TeamsRoot);
        File.WriteAllText(path, "# Project\n\nOne.\n");
        AgentsMdWatcher watcher = this.NewWatcher();
        await watcher.TickAsync(ct);
        return watcher;
    }

    private void MoveProject() =>
        Directory.Move(Path.Combine(this.TeamsRoot, "Home", "Launch"), Path.Combine(this.TeamsRoot, "Home", "Launch-2027"));

    /// <summary>With a rename of the Project Pending, a poll after the folder moved reports neither a removed nor an added AGENTS.md.</summary>
    [Fact]
    public async Task Tick_PendingProjectRename_ReportsNeitherRemovedNorAdded()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using AgentsMdWatcher watcher = await this.ObservedAsync(ct);
        _ = this.renames.Begin(TeamRenameKind.Project, "Home", "Launch", "Launch-2027");
        this.MoveProject();

        await watcher.TickAsync(ct);

        List<string> files = [.. this.fixture.Ledger.PendingEdits().Select(static edit => edit.File.RelativePath)];
        Assert.DoesNotContain(OldFile, files);
        Assert.DoesNotContain(NewFile, files);
    }
}
