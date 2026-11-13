using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.AgentsMdChanges;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Helpers;
using Agency.Huddle.App.Teams;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.AgentsMdChanges;

/// <summary>
/// <see cref="LedgerRenameRewrite"/> (Team Rename Spec 4, Ledgers): the AGENTS.md ledger rows, every Agent's file state (<c>team:</c> keys and
/// the Subscribed paths, in each form an entry under the Team folder can take) and the raw baselines follow the new folder name; the rows
/// of a Team whose name only starts the same stay; a second run changes nothing.
/// </summary>
public sealed class LedgerRenameRewriteTests : IDisposable
{
    private static readonly DateTimeOffset Stamp = new(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);

    private readonly TempDataDir dir = new();

    /// <inheritdoc />
    public void Dispose() => this.dir.Dispose();

    private static TeamRename Rename() => new(1, TeamRenameKind.Team, "Growth", "Growth", "Revenue", TeamRenameState.Pending, Stamp);

    private string AbsoluteTeam(string team) => Path.GetFullPath(Path.Combine(this.dir.Path, "Teams", team)).Replace('\\', '/');

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

    private void Observe(AgentsMdLedger ledger, string relativePath)
    {
        string full = Path.Combine(this.dir.Path, "Teams", relativePath);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(full) ?? this.dir.Path);
        File.WriteAllText(full, "Lead with the number.\n");
        _ = ledger.Observe(new AgentsMdFile(AgentsMdLevel.Team, "Home", null, full, relativePath));
    }

    private static FileState Seed(IReadOnlyList<string> subscribed) => new(
        subscribed,
        new Dictionary<string, RoomBaseline>(StringComparer.Ordinal)
        {
            ["room-1"] = new RoomBaseline(new Dictionary<string, FolderSnapshot>(StringComparer.OrdinalIgnoreCase) { ["team:Growth"] = Snapshot() }),
        },
        new Dictionary<string, IReadOnlyDictionary<string, FileWriter>>(StringComparer.OrdinalIgnoreCase)
        {
            ["team:Growth"] = new Dictionary<string, FileWriter>(FolderSnapshot.PathComparer) { ["plan.md"] = new FileWriter("room-1", new FileEntry(1, Stamp)) },
        });

    private static FolderSnapshot Snapshot() =>
        new(new Dictionary<string, FileEntry>(FolderSnapshot.PathComparer) { ["plan.md"] = new FileEntry(1, Stamp) });

    private LedgerRenameRewrite NewRewrite(AgentsMdLedger ledger, FileStateStore files, RawBaselineStore baselines) =>
        new(ledger, files, baselines, this.dir.Options());

    /// <summary>The AGENTS.md ledger entries under the Team folder are re-keyed, and those of <c>Growth2</c> stay.</summary>
    [Fact]
    public async Task RunAsync_MovesLedgerRowsUnderThePrefix_AndLeavesASiblingPrefix()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AgentsMdLedger ledger = this.NewLedger();
        this.Observe(ledger, "Growth/AGENTS.md");
        this.Observe(ledger, "Growth/Launch/AGENTS.md");
        this.Observe(ledger, "Growth2/AGENTS.md");
        string? before = ledger.FirstSightHash("Growth/AGENTS.md");
        FileStateStore files = new(this.dir.Options(), NullLogger<FileStateStore>.Instance);
        RawBaselineStore baselines = new(this.dir.Options());
        LedgerRenameRewrite rewrite = this.NewRewrite(ledger, files, baselines);

        await rewrite.RunAsync(Rename(), ct);

        Assert.NotNull(before);
        Assert.Equal(before, ledger.FirstSightHash("Revenue/AGENTS.md"));
        Assert.NotNull(ledger.FirstSightHash("Revenue/Launch/AGENTS.md"));
        Assert.Null(ledger.FirstSightHash("Growth/AGENTS.md"));
        Assert.NotNull(ledger.FirstSightHash("Growth2/AGENTS.md"));
        Assert.Empty(rewrite.TeamsTouched(Rename()));
    }

    /// <summary>Every Agent's file state follows: the <c>team:</c> keys and the Subscribed paths written as <c>Teams/..</c>, <c>./Teams/..</c> or an absolute path; <c>Teams/Growth2</c> stays.</summary>
    [Fact]
    public async Task RunAsync_RewritesEveryAgentsFileState_InEverySubscribedForm()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FileStateStore files = new(this.dir.Options(), NullLogger<FileStateStore>.Instance);
        List<string> subscribed =
        [
            "Teams/Growth/research",
            "./Teams/Growth/research",
            this.AbsoluteTeam("Growth") + "/research",
            "Teams/Growth2/other",
        ];
        files.Save("Nova", Seed(subscribed));
        files.Save("Coach", Seed(subscribed));

        await this.NewRewrite(this.NewLedger(), files, new RawBaselineStore(this.dir.Options())).RunAsync(Rename(), ct);

        foreach (string agent in new[] { "Nova", "Coach" })
        {
            FileState? state = files.Load(agent);
            Assert.NotNull(state);
            Assert.Equal(["Teams/Revenue/research", "./Teams/Revenue/research", this.AbsoluteTeam("Revenue") + "/research", "Teams/Growth2/other"], state.Subscribed);
            Assert.True(state.Rooms["room-1"].Folders.ContainsKey("team:Revenue"));
            Assert.False(state.Rooms["room-1"].Folders.ContainsKey("team:Growth"));
            Assert.True(state.Writers.ContainsKey("team:Revenue"));
        }
    }

    /// <summary>The raw baselines under the Team folder move, and <c>Teams/Growth2</c> stays.</summary>
    [Fact]
    public async Task RunAsync_MovesRawBaselines()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RawBaselineStore baselines = new(this.dir.Options());
        baselines.Record("Teams/Growth/raw/r1/a.pdf", 10, Stamp);
        baselines.Record("Teams/Growth2/raw/r1/b.pdf", 20, Stamp);

        await this.NewRewrite(this.NewLedger(), new FileStateStore(this.dir.Options(), NullLogger<FileStateStore>.Instance), baselines).RunAsync(Rename(), ct);

        Assert.True(baselines.TryGet("Teams/Revenue/raw/r1/a.pdf", out RawBaseline? moved));
        Assert.Equal(10, moved.SizeBytes);
        Assert.False(baselines.TryGet("Teams/Growth/raw/r1/a.pdf", out _));
        Assert.True(baselines.TryGet("Teams/Growth2/raw/r1/b.pdf", out _));
    }

    /// <summary>A second run finds nothing under the old prefix and changes none of the three stores.</summary>
    [Fact]
    public async Task RunAsync_SecondRun_IsIdempotent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AgentsMdLedger ledger = this.NewLedger();
        this.Observe(ledger, "Growth/AGENTS.md");
        FileStateStore files = new(this.dir.Options(), NullLogger<FileStateStore>.Instance);
        files.Save("Nova", Seed(["Teams/Growth/research"]));
        RawBaselineStore baselines = new(this.dir.Options());
        baselines.Record("Teams/Growth/raw/r1/a.pdf", 10, Stamp);
        LedgerRenameRewrite rewrite = this.NewRewrite(ledger, files, baselines);

        await rewrite.RunAsync(Rename(), ct);
        await rewrite.RunAsync(Rename(), ct);

        Assert.NotNull(ledger.FirstSightHash("Revenue/AGENTS.md"));
        Assert.Equal(["Teams/Revenue/research"], files.Load("Nova")?.Subscribed);
        Assert.True(baselines.TryGet("Teams/Revenue/raw/r1/a.pdf", out _));
    }
}
