using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.AgentsMdChanges;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.AgentsMdChanges;

/// <summary>
/// <see cref="AgentsMdLedger.MovePrefix"/> (Team Rename Spec 4, Ledgers): the entries of every <c>AGENTS.md</c> under a renamed Team's
/// folder are re-keyed to the new path, on disk and in memory, with their first sight and revisions intact.
/// </summary>
public sealed class AgentsMdLedgerMovePrefixTests : IDisposable
{
    private readonly TempDataDir dir = new();

    private string TeamsRoot => Path.Combine(this.dir.Path, "Teams");

    private string LedgerDir => Path.Combine(this.dir.Path, "agents-md-ledger");

    /// <inheritdoc />
    public void Dispose() => this.dir.Dispose();

    private AgentsMdLedger NewLedger()
    {
        IOptions<TeamOptions> options = this.dir.Options();
        HelperSettingsResolver settings = new(new HelperSettingsStore(options), options, NullLogger<HelperSettingsResolver>.Instance);
        HelperBudget budget = new(new HelperUsageStore(options));
        return new AgentsMdLedger(
            static () => throw new InvalidOperationException("The dispatcher must not be reached."),
            static () => throw new InvalidOperationException("The Helper must not be reached."),
            settings,
            budget,
            options,
            NullLogger<AgentsMdLedger>.Instance);
    }

    private AgentsMdFile Observed(AgentsMdLedger ledger, string relativePath, string text)
    {
        AgentsMdFile file = new(AgentsMdLevel.Team, "Home", null, Path.Combine(this.TeamsRoot, relativePath), relativePath);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(file.FullPath) ?? this.TeamsRoot);
        File.WriteAllText(file.FullPath, text);
        _ = ledger.Observe(file);
        return file;
    }

    /// <summary>After the move the new path finds the old entry's first sight and revisions, and the old path finds nothing.</summary>
    [Fact]
    public void MovePrefix_RekeysEntriesUnderThePrefix()
    {
        AgentsMdLedger ledger = this.NewLedger();
        _ = this.Observed(ledger, "Growth/AGENTS.md", "Lead with the number.\n");
        _ = this.Observed(ledger, "Growth/Launch/AGENTS.md", "Ship on Friday.\n");
        string? before = ledger.FirstSightHash("Growth/AGENTS.md");

        ledger.MovePrefix("Growth", "Revenue");

        Assert.NotNull(before);
        Assert.Equal(before, ledger.FirstSightHash("Revenue/AGENTS.md"));
        Assert.NotNull(ledger.FirstSightHash("Revenue/Launch/AGENTS.md"));
        Assert.Null(ledger.FirstSightHash("Growth/AGENTS.md"));
        Assert.Null(ledger.FirstSightHash("Growth/Launch/AGENTS.md"));
        Assert.Single(ledger.Since("Revenue/AGENTS.md", string.Empty) ?? []);
    }

    /// <summary>A new ledger instance over the same folder reads the moved entries: the files were re-keyed, not just memory.</summary>
    [Fact]
    public void MovePrefix_PersistsToTheLedgerFiles()
    {
        AgentsMdLedger ledger = this.NewLedger();
        _ = this.Observed(ledger, "Growth/AGENTS.md", "Lead with the number.\n");
        string? before = ledger.FirstSightHash("Growth/AGENTS.md");

        ledger.MovePrefix("Growth", "Revenue");

        AgentsMdLedger reopened = this.NewLedger();
        Assert.Equal(before, reopened.FirstSightHash("Revenue/AGENTS.md"));
        Assert.Null(reopened.FirstSightHash("Growth/AGENTS.md"));
        Assert.Single(Directory.GetFiles(this.LedgerDir, "*.json"));
    }

    /// <summary>An entry outside the prefix, including a sibling whose name only begins the same, is not moved.</summary>
    [Fact]
    public void MovePrefix_EntryOutsideThePrefix_Untouched()
    {
        AgentsMdLedger ledger = this.NewLedger();
        _ = this.Observed(ledger, "Growth/AGENTS.md", "One.\n");
        _ = this.Observed(ledger, "Growth2/AGENTS.md", "Two.\n");
        _ = this.Observed(ledger, "Ops/AGENTS.md", "Three.\n");

        ledger.MovePrefix("Growth", "Revenue");

        Assert.NotNull(ledger.FirstSightHash("Growth2/AGENTS.md"));
        Assert.NotNull(ledger.FirstSightHash("Ops/AGENTS.md"));
        Assert.Null(ledger.FirstSightHash("Revenue2/AGENTS.md"));
        Assert.Equal(3, Directory.GetFiles(this.LedgerDir, "*.json").Length);
    }

    /// <summary>A case-only change keeps the case-folded key, so nothing moves and both spellings still find the entry.</summary>
    [Fact]
    public void MovePrefix_CaseOnly_IsANoOp()
    {
        AgentsMdLedger ledger = this.NewLedger();
        _ = this.Observed(ledger, "Growth/AGENTS.md", "One.\n");
        string[] filesBefore = [.. Directory.GetFiles(this.LedgerDir, "*.json").Order(StringComparer.Ordinal)];

        ledger.MovePrefix("Growth", "growth");

        string[] filesAfter = [.. Directory.GetFiles(this.LedgerDir, "*.json").Order(StringComparer.Ordinal)];
        Assert.NotNull(ledger.FirstSightHash("Growth/AGENTS.md"));
        Assert.NotNull(ledger.FirstSightHash("growth/AGENTS.md"));
        Assert.Equal(filesBefore, filesAfter);
    }
}
