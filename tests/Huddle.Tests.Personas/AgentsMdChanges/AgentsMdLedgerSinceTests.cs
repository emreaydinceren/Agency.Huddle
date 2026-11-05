using System.Text.Json;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.AgentsMdChanges;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.AgentsMdChanges;

/// <summary>
/// Pins <see cref="AgentsMdLedger.Since"/> and the revision cap (AgentsMdChanges Spec §8.3 last paragraphs and §6.2
/// "Implementation notes"): oldest first, the last match after a revert, an empty answer at the head, null once the chain
/// was pruned past the heard hash, a cap of <c>MaxRevisionsKept</c>, and a first-sight hash that outlives pruning and a
/// restart. Edits settle through <see cref="AgentsMdLedger.CommitComparison"/>, so no Helper is ever reached.
/// </summary>
public sealed class AgentsMdLedgerSinceTests : IDisposable
{
    private const string Relative = "Home/AGENTS.md";
    private const int Cap = 3;

    private readonly TempDataDir dir = new();
    private int writes;

    private string TeamsRoot => Path.Combine(this.dir.Path, "Teams");

    private string LedgerDir => Path.Combine(this.dir.Path, "agents-md-ledger");

    /// <inheritdoc />
    public void Dispose() => this.dir.Dispose();

    /// <summary>The one place the ledger is built; a second call is a new instance over the same folder.</summary>
    /// <param name="cap">The revisions kept per file; the default is large enough that nothing is pruned.</param>
    private AgentsMdLedger NewLedger(int cap = 20)
    {
        IOptions<TeamOptions> options = this.dir.Options(team => team.AgentsMd.MaxRevisionsKept = cap);
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

    /// <summary>The file under test.</summary>
    private AgentsMdFile TheFile() => new(AgentsMdLevel.Team, "Home", null, Path.Combine(this.TeamsRoot, Relative), Relative);

    /// <summary>The text for version <paramref name="version"/>; each version has its own hash.</summary>
    /// <param name="version">The version number.</param>
    private static string TextOf(int version) => $"Rule {version}.\n";

    /// <summary>The hash the ledger uses for version <paramref name="version"/>.</summary>
    /// <param name="version">The version number.</param>
    private static string HashOf(int version) => AgentsMdBody.Parse(TextOf(version)).Hash;

    /// <summary>Writes a version with a distinct modification time.</summary>
    /// <param name="version">The version number.</param>
    private void Save(int version)
    {
        AgentsMdFile file = this.TheFile();
        Directory.CreateDirectory(Path.GetDirectoryName(file.FullPath) ?? this.TeamsRoot);
        File.WriteAllText(file.FullPath, TextOf(version));
        this.writes++;
        File.SetLastWriteTimeUtc(file.FullPath, new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc).AddMinutes(this.writes));
    }

    /// <summary>Records the first version as first sight, then settles each later version as its own revision.</summary>
    /// <param name="ledger">The ledger.</param>
    /// <param name="versions">The versions in order; the first is the first sight.</param>
    private void Play(AgentsMdLedger ledger, params int[] versions)
    {
        this.Save(versions[0]);
        _ = ledger.Observe(this.TheFile());
        foreach (int version in versions.Skip(1))
        {
            this.Save(version);
            _ = ledger.Observe(this.TheFile());
            _ = ledger.CommitComparison(this.TheFile());
        }
    }

    /// <summary>The number of revisions in the one ledger file on disk.</summary>
    private int RevisionsOnDisk()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Assert.Single(Directory.GetFiles(this.LedgerDir, "*.json"))));
        foreach (JsonProperty property in document.RootElement.EnumerateObject())
        {
            if (string.Equals(property.Name, "Revisions", StringComparison.OrdinalIgnoreCase))
            {
                return property.Value.GetArrayLength();
            }
        }

        return -1;
    }

    /// <summary>Since a heard hash returns the later revisions in the order they were committed.</summary>
    [Fact]
    public void Since_ReturnsOldestFirst()
    {
        AgentsMdLedger ledger = this.NewLedger();
        this.Play(ledger, 0, 1, 2, 3);

        IReadOnlyList<AgentsMdRevision>? since = ledger.Since(Relative, HashOf(0));

        Assert.NotNull(since);
        Assert.Equal([HashOf(1), HashOf(2), HashOf(3)], since.Select(static revision => revision.ToHash));
    }

    /// <summary>A revert makes a hash appear twice; Since counts from the last revision that reached it.</summary>
    [Fact]
    public void Since_UsesTheLastMatch_AfterARevert()
    {
        AgentsMdLedger ledger = this.NewLedger();
        this.Play(ledger, 0, 1, 0, 2);

        IReadOnlyList<AgentsMdRevision>? since = ledger.Since(Relative, HashOf(0));

        Assert.NotNull(since);
        Assert.Equal([HashOf(2)], since.Select(static revision => revision.ToHash));
    }

    /// <summary>A heard hash equal to the head has nothing after it: an empty list, not null.</summary>
    [Fact]
    public void Since_HeadHash_Empty()
    {
        AgentsMdLedger ledger = this.NewLedger();
        this.Play(ledger, 0, 1);

        IReadOnlyList<AgentsMdRevision>? since = ledger.Since(Relative, HashOf(1));

        Assert.NotNull(since);
        Assert.Empty(since);
    }

    /// <summary>A heard hash that fell off the front of the pruned chain gives null: the unknown path.</summary>
    [Fact]
    public void Since_PrunedPast_Null()
    {
        AgentsMdLedger ledger = this.NewLedger(Cap);
        this.Play(ledger, 0, 1, 2, 3, 4);

        Assert.Null(ledger.Since(Relative, HashOf(0)));
    }

    /// <summary>The chain never holds more than <c>MaxRevisionsKept</c> revisions, in memory and on disk, and keeps the newest.</summary>
    [Fact]
    public void Pruning_KeepsMaxRevisions()
    {
        AgentsMdLedger ledger = this.NewLedger(Cap);
        this.Play(ledger, 0, 1, 2, 3, 4);

        IReadOnlyList<AgentsMdRevision>? since = ledger.Since(Relative, HashOf(1));

        Assert.Equal(Cap, this.RevisionsOnDisk());
        Assert.NotNull(since);
        Assert.Equal([HashOf(2), HashOf(3), HashOf(4)], since.Select(static revision => revision.ToHash));
    }

    /// <summary>The first-sight hash is its own field, so pruning the first revision away does not lose it.</summary>
    [Fact]
    public void FirstSightHash_SurvivesPruning()
    {
        AgentsMdLedger ledger = this.NewLedger(Cap);
        this.Play(ledger, 0, 1, 2, 3, 4);

        Assert.Equal(HashOf(0), ledger.FirstSightHash(Relative));
    }

    /// <summary>A pruned chain reloaded by a new instance keeps its cap, its first-sight hash and its unknown path.</summary>
    [Fact]
    public void Pruned_SurvivesANewInstance()
    {
        this.Play(this.NewLedger(Cap), 0, 1, 2, 3, 4);

        AgentsMdLedger reloaded = this.NewLedger(Cap);

        IReadOnlyList<AgentsMdRevision>? since = reloaded.Since(Relative, HashOf(1));

        Assert.Equal(HashOf(0), reloaded.FirstSightHash(Relative));
        Assert.Null(reloaded.Since(Relative, HashOf(0)));
        Assert.NotNull(since);
        Assert.Equal(Cap, since.Count);
        Assert.Equal(Cap, this.RevisionsOnDisk());
    }
}
