using System.Text.RegularExpressions;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Elicitation;
using Microsoft.Extensions.Logging;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// Pins #669 (spec D-5, V7a-c): an installed Team whose <c>AGENTS.md</c> still describes the Review stage is offered the rewrite
/// as pending retro proposals from <c>Huddle</c>, once; the file is never written until the Human accepts. The old texts are the
/// process templates Huddle shipped, taken from git history into <c>ReviewStageOldProcesses/</c>.
/// </summary>
public sealed partial class ReviewStageMigrationTests
{
    private const string MarkerName = "review-tasks.checked";

    private const string LegacyMarkerName = "review-tasks.proposed";

    private static readonly string OldFolder = Path.Combine(AppContext.BaseDirectory, "Teams", "ReviewStageOldProcesses");

    /// <summary>The last shipped copy of each process before a review became a Task.</summary>
    public static TheoryData<string> LatestOldFiles() =>
    [
        "lightweight-flow.9f23cabc0.md",
        "spec-driven.9f23cabc0.md",
        "maintenance.9f23cabc0.md",
        "sprints.9f23cabc0.md",
        "household-basics.aafc31dca.md",
    ];

    /// <summary>Every distinct shipped copy of every process that describes a Review stage.</summary>
    public static TheoryData<string> AllOldFiles()
    {
        TheoryData<string> data = [];
        foreach (string file in Directory.GetFiles(OldFolder, "*.md").Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetFileName(file));
        }

        return data;
    }

    /// <summary>A Team with the verbatim old text gets proposal sets of at most three changes whose finds each occur once.</summary>
    /// <param name="fileName">The old process copy.</param>
    [Theory]
    [MemberData(nameof(AllOldFiles))]
    public async Task StartAsync_OldShippedProcess_StoresSetsFromHuddleWithUniqueFinds(string fileName)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        Rig rig = new(dir, OldText(fileName));

        await rig.Migration().StartAsync(ct);

        IReadOnlyList<RetroProposalSet> sets = rig.Store.Pending(Rig.TeamName);
        Assert.NotEmpty(sets);
        Assert.All(sets, static set => Assert.Equal("Huddle", set.Proposer));
        Assert.All(sets, static set => Assert.InRange(set.Changes.Count, 1, 3));
        string text = rig.AgentsMd;
        foreach (RetroChange change in sets.SelectMany(static set => set.Changes))
        {
            Assert.Equal("AGENTS.md", change.TargetFile);
            Assert.Equal(1, RetroDiff.CountMatches(text, RetroDiff.FitLineEndings(text, change.Find)));
        }
    }

    /// <summary>Applying every offered change to any shipped copy leaves no Review-stage wording.</summary>
    /// <param name="fileName">The old process copy.</param>
    [Theory]
    [MemberData(nameof(AllOldFiles))]
    public async Task StartAsync_OldShippedProcess_AppliedChangesLeaveNoOldWording(string fileName)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        Rig rig = new(dir, OldText(fileName));

        await rig.Migration().StartAsync(ct);

        string text = rig.AgentsMd;
        foreach (RetroChange change in rig.Store.Pending(Rig.TeamName).SelectMany(static set => set.Changes))
        {
            text = text.Replace(RetroDiff.FitLineEndings(text, change.Find), RetroDiff.FitLineEndings(text, change.Replace), StringComparison.Ordinal);
        }

        Assert.DoesNotMatch(OldWording(), text);
    }

    /// <summary>The file is byte-identical after the step and equal to the current built-in process once every change is accepted.</summary>
    /// <param name="fileName">The last shipped copy of one process.</param>
    [Theory]
    [MemberData(nameof(LatestOldFiles))]
    public async Task AcceptAsync_EveryChange_GivesTheCurrentBuiltInProcess(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);
        string old = OldText(fileName);
        string path = Path.Combine(fixture.TeamFolder, TeamPaths.AgentsMdFileName);
        await File.WriteAllTextAsync(path, old, ct);
        byte[] before = await File.ReadAllBytesAsync(path, ct);

        await Migration(fixture).StartAsync(ct);

        Assert.Equal(before, await File.ReadAllBytesAsync(path, ct));
        IReadOnlyList<RetroProposalSet> sets = await fixture.Service.PendingAsync(fixture.RoomId, ct);
        Assert.NotEmpty(sets);
        foreach (RetroProposalSet set in sets)
        {
            foreach (RetroChange change in set.Changes)
            {
                RetroChangeOutcome outcome = await fixture.Service.AcceptAsync(fixture.RoomId, set.Id, change.Id, ct);
                Assert.True(outcome.Kind == RetroChangeOutcomeKind.Applied, outcome.Text + " | " + change.Title);
            }
        }

        Assert.DoesNotMatch(OldWording(), fixture.ProcessNow);
        Assert.Equal(WithoutPlaybookTrim(CurrentText(fileName)), WithoutPlaybookTrim(fixture.ProcessNow.ReplaceLineEndings("\n")));
        Assert.Empty(fixture.Proposals.Pending("Platform"));
    }

    /// <summary>
    /// Drops what Playbooks S4 trimmed from the built-in processes (the draft-is-a-file paragraph, the disagreement
    /// bullet and the template version), which the review-wording migration never touches, so the comparison
    /// keeps checking only the review wording.
    /// </summary>
    /// <param name="text">A process file with <c>\n</c> line endings.</param>
    /// <returns>The text without those lines.</returns>
    private static string WithoutPlaybookTrim(string text)
    {
        List<string> kept = [];
        bool skipBlank = false;
        foreach (string line in text.Split('\n'))
        {
            if (skipBlank)
            {
                skipBlank = false;
                if (line.Length == 0)
                {
                    continue;
                }
            }

            if (line.StartsWith("A draft that the Definition of Done reviews is a file", StringComparison.Ordinal))
            {
                skipBlank = true;
                continue;
            }

            if (line.StartsWith("template: ", StringComparison.Ordinal)
                || string.Equals(line, "- A disagreement between agents goes to the Human with both views written on the Task.", StringComparison.Ordinal))
            {
                continue;
            }

            kept.Add(line);
        }

        return WithoutBlockerGate(string.Join('\n', kept));
    }

    /// <summary>
    /// Drops what TaskOrder slice 2 added to every built-in process (the <c>blockers-done-before-start</c> entry in
    /// <c>gates:</c>, its Gates bullet, and "no other hard gate" back to "no hard gate"). Installed Teams are never
    /// migrated to that gate, so the review-wording migration correctly leaves it out and the comparison must too.
    /// </summary>
    /// <param name="text">A process file with <c>\n</c> line endings.</param>
    /// <returns>The text as it read before the gate was added.</returns>
    private static string WithoutBlockerGate(string text)
    {
        IEnumerable<string> kept = text.Split('\n')
            .Where(static line => !line.StartsWith("- `blockers-done-before-start`:", StringComparison.Ordinal))
            .Select(static line => line
                .Replace("gates: ['blockers-done-before-start']", "gates: []", StringComparison.Ordinal)
                .Replace("declares no other hard gate", "declares no hard gate", StringComparison.Ordinal));
        return string.Join('\n', kept);
    }

    /// <summary>A file with CRLF line endings is offered the same rewrite and ends with the current wording, still CRLF.</summary>
    [Fact]
    public async Task AcceptAsync_CrlfFile_GivesTheCurrentProcessWithCrlf()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);
        const string FileName = "spec-driven.9f23cabc0.md";
        await File.WriteAllTextAsync(Path.Combine(fixture.TeamFolder, TeamPaths.AgentsMdFileName), OldText(FileName).ReplaceLineEndings("\r\n"), ct);

        await Migration(fixture).StartAsync(ct);
        foreach (RetroProposalSet set in await fixture.Service.PendingAsync(fixture.RoomId, ct))
        {
            foreach (RetroChange change in set.Changes)
            {
                Assert.Equal(RetroChangeOutcomeKind.Applied, (await fixture.Service.AcceptAsync(fixture.RoomId, set.Id, change.Id, ct)).Kind);
            }
        }

        Assert.Equal(CurrentText(FileName).ReplaceLineEndings("\r\n"), fixture.ProcessNow);
    }

    /// <summary>A Team whose file has no outdated text, or already has the new text, gets nothing.</summary>
    /// <param name="fileName">A current built-in process, or <see langword="null"/> for the fixture's own short process.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("lightweight-flow.9f23cabc0.md")]
    [InlineData("household-basics.aafc31dca.md")]
    public async Task StartAsync_NoOutdatedText_OffersNothing(string? fileName)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        Rig rig = new(dir, fileName is null ? RetroProposalFixture.ProcessText : CurrentText(fileName));

        await rig.Migration().StartAsync(ct);

        Assert.Empty(rig.Store.Pending(Rig.TeamName));
    }

    /// <summary>A second start offers nothing more, and neither does a start after the Human rejected every change.</summary>
    [Fact]
    public async Task StartAsync_SecondStartOrAfterReject_OffersNothingAgain()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);
        await File.WriteAllTextAsync(Path.Combine(fixture.TeamFolder, TeamPaths.AgentsMdFileName), OldText("sprints.9f23cabc0.md"), ct);

        await Migration(fixture).StartAsync(ct);
        int offered = fixture.Proposals.Pending("Platform").Count;
        await Migration(fixture).StartAsync(ct);

        Assert.True(offered > 0);
        Assert.True(File.Exists(Path.Combine(fixture.TeamFolder, MarkerName)));
        Assert.Equal(offered, fixture.Proposals.Pending("Platform").Count);
        foreach (RetroProposalSet set in await fixture.Service.PendingAsync(fixture.RoomId, ct))
        {
            foreach (RetroChange change in set.Changes)
            {
                _ = await fixture.Service.RejectAsync(fixture.RoomId, set.Id, change.Id, "keep my wording", ct);
            }
        }

        await Migration(fixture).StartAsync(ct);

        Assert.Empty(fixture.Proposals.Pending("Platform"));
    }

    /// <summary>A Team whose file holds no old text is marked checked once, and the marker does not say anything was offered.</summary>
    [Fact]
    public async Task StartAsync_NothingToOffer_WritesACheckedMarkerThatSaysNothingWasOffered()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        Rig rig = new(dir, RetroProposalFixture.ProcessText);

        await rig.Migration().StartAsync(ct);

        string marker = await File.ReadAllTextAsync(Path.Combine(dir.Path, "platform", MarkerName), ct);
        Assert.Empty(rig.Store.Pending(Rig.TeamName));
        Assert.Contains("nothing was offered", marker, StringComparison.Ordinal); // contains-ok: pins the stored marker text
        Assert.False(File.Exists(Path.Combine(dir.Path, "platform", LegacyMarkerName)));
    }

    /// <summary>A Team whose file holds old text is marked checked with a marker that says a rewrite was offered.</summary>
    [Fact]
    public async Task StartAsync_OldText_WritesACheckedMarkerThatSaysARewriteWasOffered()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        Rig rig = new(dir, OldText("sprints.9f23cabc0.md"));

        await rig.Migration().StartAsync(ct);

        string marker = await File.ReadAllTextAsync(Path.Combine(dir.Path, "platform", MarkerName), ct);
        Assert.NotEmpty(rig.Store.Pending(Rig.TeamName));
        Assert.Contains("a rewrite was offered", marker, StringComparison.Ordinal); // contains-ok: pins the stored marker text
    }

    /// <summary>A Team that holds the older marker name is already checked: nothing is offered and no new marker is written.</summary>
    [Fact]
    public async Task StartAsync_LegacyMarker_IsTreatedAsCheckedAndOffersNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        Rig rig = new(dir, OldText("sprints.9f23cabc0.md"));
        await File.WriteAllTextAsync(Path.Combine(dir.Path, "platform", LegacyMarkerName), "old marker", ct);

        await rig.Migration().StartAsync(ct);

        Assert.Empty(rig.Store.Pending(Rig.TeamName));
        Assert.False(File.Exists(Path.Combine(dir.Path, "platform", MarkerName)));
    }

    /// <summary>A Team that fails is logged and the next Team is still handled.</summary>
    [Fact]
    public async Task StartAsync_OneTeamFails_LogsAndHandlesTheNext()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        Rig rig = new(dir, OldText("lightweight-flow.9f23cabc0.md"));
        FakeTeamCatalog catalog = new() { Teams = [new TeamSummary("Broken", [], [], true), new TeamSummary(Rig.TeamName, [], [], true)] };
        ThrowingFolders folders = new(rig.Folders, "Broken");

        await new ReviewStageMigration(catalog, folders, rig.Store, rig.Log).StartAsync(ct);

        Assert.NotEmpty(rig.Store.Pending(Rig.TeamName));
        Assert.Contains(rig.Log.Entries, static entry => entry.Level == LogLevel.Warning);
    }

    private static ReviewStageMigration Migration(RetroProposalFixture fixture) =>
        new(new FakeTeamCatalog { Teams = [new TeamSummary("Platform", [], [], true)] }, fixture.Folders, fixture.Proposals, new RecordingLogger<ReviewStageMigration>());

    private static string OldText(string fileName) => File.ReadAllText(Path.Combine(OldFolder, fileName));

    private static string CurrentText(string fileName)
    {
        string process = fileName[..fileName.IndexOf('.', StringComparison.Ordinal)];
        string type = string.Equals(process, "household-basics", StringComparison.Ordinal) ? "household" : "development";
        return File.ReadAllText(Path.Combine(RepoRoot(), "src", "Huddle.App", "TeamTypes", "Builtin", type, "processes", process + ".md")).ReplaceLineEndings("\n");
    }

    private static string RepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Huddle.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Huddle.slnx was not found above the test folder.");
    }

    /// <summary>Matches the Review-stage wording the shipped processes used.</summary>
    /// <returns>The generated regex.</returns>
    [GeneratedRegex(
        @"\*\*Review\b|\*\*Verify\*\*: QA|Review and verify|\b(?:in|to|from|into) Review\b|Progress, Review|- Review: evidence from challenge|- QA evidence: evidence from verify|- Spec reviewed|asks? for review|records findings|outcome on the Task|waits in Review|second read",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 2000)]
    private static partial Regex OldWording();

    /// <summary>One Team folder on disk with a given <c>AGENTS.md</c>, the real store and a recording log.</summary>
    private sealed class Rig
    {
        public const string TeamName = "Platform";

        private readonly string agentsPath;

        public Rig(TempDataDir dir, string agentsMd)
        {
            string folder = Path.Combine(dir.Path, "platform");
            _ = Directory.CreateDirectory(folder);
            this.agentsPath = Path.Combine(folder, TeamPaths.AgentsMdFileName);
            File.WriteAllText(this.agentsPath, agentsMd);
            this.Folders = new DiskTeamFolders(folder);
            this.Store = new RetroProposalStore(this.Folders);
        }

        public DiskTeamFolders Folders { get; }

        public RetroProposalStore Store { get; }

        public RecordingLogger<ReviewStageMigration> Log { get; } = new();

        public string AgentsMd => File.ReadAllText(this.agentsPath);

        public ReviewStageMigration Migration() =>
            new(new FakeTeamCatalog { Teams = [new TeamSummary(TeamName, [], [], true)] }, this.Folders, this.Store, this.Log);
    }

    /// <summary>Folders that serve one Team from a real folder and fail with an <see cref="IOException"/> for another.</summary>
    private sealed class ThrowingFolders(ITeamFolders inner, string failing) : ITeamFolders
    {
        public LibraryResult<LibraryPath> EnsureTeam(string name) => inner.EnsureTeam(name);

        public LibraryResult<LibraryPath> FindTeam(string name) =>
            string.Equals(name, failing, StringComparison.Ordinal) ? throw new IOException("The Team folder is locked.") : inner.FindTeam(name);

        public LibraryResult<LibraryPath> EnsureProjectIn(string team, string project) => inner.EnsureProjectIn(team, project);

        public TeamCreateResult CreateTeam(string team, TeamFile file, bool leaveExistingAlone = false) => inner.CreateTeam(team, file, leaveExistingAlone);

        public bool UpdateTeamFile(string team, Func<TeamFile, TeamFile> change) => inner.UpdateTeamFile(team, change);

        public TeamFile? ReadTeamFile(string team) => inner.ReadTeamFile(team);
    }
}
