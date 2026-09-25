using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Teammates;
using Agency.Huddle.Tests.Acp;
using Agency.Huddle.Tests.Acp.Tools;

namespace Agency.Huddle.Tests.Teammates;

/// <summary>
/// Pins <see cref="CandidateChecker"/> against Spec §6.8's internal flow and every row of Spec
/// §8.3, using real collaborators throughout (<see cref="PersonaStore"/>,
/// <see cref="SqliteTeamDirectory"/>) rather than mocks, plus the existing
/// <see cref="FakeAgentGateway"/> from the Tools tests - the checker never opens a pipe, so that
/// fake is a complete stand-in. Every problem <see cref="CandidateChecker"/> returns is
/// model-facing text (Spec §6.8, §8.3): <see cref="AssertProblemsAreModelFacing"/> is applied to
/// every test's <see cref="CandidateCheck.Problems"/> to pin that no absolute filesystem path, no
/// <c>~</c> disambiguator, and no bare synthetic <c>{Name}.md</c> filename ever leaks - only a
/// real Persona's path, rewritten relative to the Teams directory, may still say <c>.md</c>.
/// </summary>
public sealed partial class CandidateCheckerTests
{
    /// <summary>
    /// Spec §8.3 order 2: <see cref="Agency.Huddle.Contracts.NameRules.IsValidAgentName"/> catches
    /// a Name with a trailing period, worded exactly as the Spec's own example.
    /// </summary>
    [Fact]
    public async Task CheckAsync_InvalidName_ReportsProblem()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using var fixture = await CreateFixtureAsync(ct);

        var candidate = MakeCandidate(name: "Vera.");

        var result = await fixture.Checker.CheckAsync([candidate], ct);

        Assert.False(result.IsValid);
        Assert.Contains("'Vera.' is not a valid Name: letters, digits, '-' and '_', with single spaces between words.", result.Problems);
        AssertProblemsAreModelFacing(result.Problems);
    }

    /// <summary>Spec §8.3 order 3: a blank Title is a problem, worded exactly as the Spec's own example.</summary>
    [Fact]
    public async Task CheckAsync_BlankTitle_ReportsProblem()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using var fixture = await CreateFixtureAsync(ct);

        var candidate = MakeCandidate(name: "Iris", alias: "iris", title: "   ");

        var result = await fixture.Checker.CheckAsync([candidate], ct);

        Assert.False(result.IsValid);
        Assert.Contains("Iris has a blank Title.", result.Problems);
        AssertProblemsAreModelFacing(result.Problems);
    }

    /// <summary>
    /// Spec §6.8 implementation notes: a Body starting with <c>---</c> would read as a second
    /// frontmatter block once composed, so it is a problem rather than silently accepted.
    /// </summary>
    [Fact]
    public async Task CheckAsync_BodyStartsWithFrontmatterDelimiter_ReportsProblem()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using var fixture = await CreateFixtureAsync(ct);

        var candidate = MakeCandidate(body: "---\nThis looks like a second frontmatter block.");

        var result = await fixture.Checker.CheckAsync([candidate], ct);

        Assert.False(result.IsValid);
        Assert.Contains(result.Problems, p => p.Contains("Body", StringComparison.Ordinal) && p.Contains("---", StringComparison.Ordinal));
        AssertProblemsAreModelFacing(result.Problems);
    }

    /// <summary>
    /// Spec §12 F-24: a Team containing <c>,</c> would silently split into two Teams the next time
    /// the composed frontmatter is read back (the <c>SplitTeams</c> round-trip hazard), so it is
    /// rejected up front instead.
    /// </summary>
    [Fact]
    public async Task CheckAsync_TeamsEntryContainsComma_ReportsProblem()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using var fixture = await CreateFixtureAsync(ct);

        var candidate = MakeCandidate(teams: ["a,b"]);

        var result = await fixture.Checker.CheckAsync([candidate], ct);

        Assert.False(result.IsValid);
        Assert.Contains(result.Problems, p => p.Contains("a,b", StringComparison.Ordinal));
        AssertProblemsAreModelFacing(result.Problems);
    }

    /// <summary>
    /// Spec §8.3 order 4: two Candidates in the same Proposal sharing an Alias must both be named,
    /// either by <see cref="CandidateChecker"/>'s own joint check or by a rewritten
    /// <c>PersonaStore.Check</c> message - either way, neither Candidate's synthetic
    /// <c>{Name}.md</c> filename may leak, since neither Candidate exists on disk.
    /// </summary>
    [Fact]
    public async Task CheckAsync_TwoCandidatesShareAnAlias_NamesBothCandidates()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using var fixture = await CreateFixtureAsync(ct);

        var vera = MakeCandidate(name: "Vera", alias: "vee");
        var vela = MakeCandidate(name: "Vela", alias: "vee");

        var result = await fixture.Checker.CheckAsync([vera, vela], ct);

        Assert.False(result.IsValid);
        var joined = string.Join(" | ", result.Problems);
        Assert.Contains("Vera", joined, StringComparison.Ordinal);
        Assert.Contains("Vela", joined, StringComparison.Ordinal);
        AssertProblemsAreModelFacing(result.Problems);
    }

    /// <summary>
    /// Spec §8.3 order 5: an Alias equal to an existing Persona's Name is rejected by
    /// <c>PersonaStore.Check</c>'s collision rules, and the message must name the real file - a
    /// path this really exists at - relative to the data directory, never absolute.
    /// </summary>
    [Fact]
    public async Task CheckAsync_AliasEqualsExistingPersonaName_NamesTheRealFileRelatively()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using var fixture = await CreateFixtureAsync(ct);

        fixture.Personas.Add(new PersonaIdentity("Jarvis", "Chief of Staff", "jar", []), "You are Jarvis.");

        var candidate = MakeCandidate(name: "Newbie", alias: "Jarvis");

        var result = await fixture.Checker.CheckAsync([candidate], ct);

        Assert.False(result.IsValid);
        // contains-ok: the rest of the sentence (which Persona/Alias collided) is PersonaStore.Check's
        // own text, already pinned by PersonaStoreTests; this only proves the rewritten path survived.
        Assert.Contains(result.Problems, p => p.Contains("Teammates/Jarvis/Jarvis.md", StringComparison.Ordinal));
        AssertProblemsAreModelFacing(result.Problems);
    }

    /// <summary>
    /// Spec §8.3 order 5: a Candidate whose NAME equals an existing Persona's own Name is rejected
    /// with a reason that names the real file - not the collapsed-path bug where the candidate's
    /// synthetic path and the real file's path were both literally <c>Iris.md</c>, leaving
    /// <see cref="PersonaIndex"/>'s "others" exclusion (which compares Path) with nothing to name
    /// and producing <c>"Persona Name 'Iris' is also used by . ..."</c> instead. Fixed at the
    /// source in <c>PersonaStore.SyntheticPathsFor</c>.
    /// </summary>
    [Fact]
    public async Task CheckAsync_CandidateNameEqualsExistingPersonaName_NamesTheFile()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using var fixture = await CreateFixtureAsync(ct);

        fixture.Personas.Add(new PersonaIdentity("Iris", "Existing Role", "existing-iris", []), "Existing Iris body.");

        var candidate = MakeCandidate(name: "Iris", alias: "new-iris");

        var result = await fixture.Checker.CheckAsync([candidate], ct);

        Assert.False(result.IsValid);
        // contains-ok: the rest of the sentence is PersonaStore.Check's own text, already pinned by
        // PersonaStoreTests; this only proves the rewritten path survived.
        Assert.Contains(result.Problems, p => p.Contains("Teammates/Iris/Iris.md", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Problems, p => p.Contains("used by .", StringComparison.Ordinal));
        AssertProblemsAreModelFacing(result.Problems);
    }

    /// <summary>Spec §8.3 order 6 / F-9: a Name equal to the Human's Name, worded exactly as the Spec's own example.</summary>
    [Fact]
    public async Task CheckAsync_NameEqualsHumanName_ReportsProblem()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using var fixture = await CreateFixtureAsync(ct);

        var candidate = MakeCandidate(name: "You", alias: "you-alias");

        var result = await fixture.Checker.CheckAsync([candidate], ct);

        Assert.False(result.IsValid);
        Assert.Contains("'You' is the Human's Name.", result.Problems);
        AssertProblemsAreModelFacing(result.Problems);
    }

    /// <summary>
    /// Spec §8.3 order 6 / F-9: a Name matching a connected Agent that has no Persona file (a demo
    /// Agent such as <c>echo</c>) is reserved, worded exactly as the Spec's own example - otherwise
    /// the new Persona's <c>hello</c> would attach to the existing Agent's id.
    /// </summary>
    [Fact]
    public async Task CheckAsync_NameMatchesConnectedAgentWithNoPersona_ReportsProblem()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using var fixture = await CreateFixtureAsync(ct);

        var echoUser = await fixture.Directory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(echoUser);
        fixture.Gateway.SetOnline(echoUser.Id);

        var candidate = MakeCandidate(name: "echo", alias: "echo-alias");

        var result = await fixture.Checker.CheckAsync([candidate], ct);

        Assert.False(result.IsValid);
        Assert.Contains("'echo' is a connected Agent that is not a Teammate.", result.Problems);
        AssertProblemsAreModelFacing(result.Problems);
    }

    /// <summary>
    /// Spec §6.8 internal flow's own "files" step: <c>{Name}.md</c> already existing under the
    /// Teams directory is a problem even when that file never became a valid Persona - seeded here
    /// as raw, non-Persona text written before the store's own startup scan, so it is
    /// deterministically present in <see cref="PersonaStore.RejectedFiles"/> by the time the
    /// checker runs, rather than depending on the filesystem watcher's debounce.
    /// </summary>
    [Fact]
    public async Task CheckAsync_NameFileAlreadyOnDisk_ReportsProblem()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using var fixture = await CreateFixtureAsync(
            ct,
            paths => TestPersonaFiles.Write(paths, "Ghost", "not even a valid Persona file"));

        var candidate = MakeCandidate(name: "Ghost", alias: "ghost");

        var result = await fixture.Checker.CheckAsync([candidate], ct);

        Assert.False(result.IsValid);
        Assert.Contains(result.Problems, p => p.Contains("Ghost", StringComparison.Ordinal));
        AssertProblemsAreModelFacing(result.Problems);
    }

    /// <summary>
    /// The Deliverable's headline claim: a Candidate with three independent faults (an invalid
    /// Name, a blank Title, and a Body that starts with <c>---</c>) gets all three problems back
    /// together, not just the first one <see cref="CandidateChecker"/> happens to find.
    /// </summary>
    [Fact]
    public async Task CheckAsync_CandidateWithThreeFaults_ReportsAllThreeTogether()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using var fixture = await CreateFixtureAsync(ct);

        var candidate = MakeCandidate(name: "Vera.", alias: "vera", title: "   ", body: "---\nSecond frontmatter block?");

        var result = await fixture.Checker.CheckAsync([candidate], ct);

        Assert.False(result.IsValid);
        Assert.Contains(result.Problems, p => p.Contains("is not a valid Name", StringComparison.Ordinal));
        Assert.Contains(result.Problems, p => p.Contains("blank Title", StringComparison.Ordinal));
        Assert.Contains(result.Problems, p => p.Contains("Body", StringComparison.Ordinal) && p.Contains("---", StringComparison.Ordinal));
        Assert.True(result.Problems.Count >= 3, $"Expected all three faults reported together; got {result.Problems.Count}: {string.Join(" | ", result.Problems)}");
        AssertProblemsAreModelFacing(result.Problems);
    }

    /// <summary>
    /// Builds a valid Candidate with every field defaulted to something that passes every check,
    /// so each test above overrides only the one field its fault lives in.
    /// </summary>
    private static Candidate MakeCandidate(
        string name = "Vera",
        string alias = "vee",
        string title = "Researcher",
        string body = "You research things.",
        IReadOnlyList<string>? teams = null,
        string? consultWhen = null)
    {
        return new Candidate(name, alias, title, body, teams ?? [], consultWhen);
    }

    /// <summary>
    /// Builds a real <see cref="PersonaStore"/>, <see cref="SqliteTeamDirectory"/> and
    /// <see cref="FakeAgentGateway"/> in a fresh <see cref="TempDataDir"/>, and the
    /// <see cref="CandidateChecker"/> under test wired to all three. <paramref name="seedTeamsDir"/>,
    /// when given, writes files into the Teams directory BEFORE <see cref="PersonaStore"/> is
    /// constructed, so its constructor's own startup scan picks them up deterministically.
    /// </summary>
    private static async Task<Fixture> CreateFixtureAsync(CancellationToken ct, Action<TeammatePaths>? seedTeamsDir = null)
    {
        var dataDir = new TempDataDir();
        var paths = new TeammatePaths(dataDir.Options());
        seedTeamsDir?.Invoke(paths);

        var directory = new SqliteTeamDirectory(dataDir.Options());
        await directory.InitializeAsync("You", ct);

        var personas = new PersonaStore(
            paths, new PersonaModelStore(dataDir.Options()), new PersonaEffortStore(dataDir.Options()), NullLogger<PersonaStore>.Instance);
        var gateway = new FakeAgentGateway();
        var checker = new CandidateChecker(personas, directory, gateway);

        return new Fixture(dataDir, personas, directory, gateway, checker);
    }

    /// <summary>
    /// Asserts that none of <paramref name="problems"/> leaks a filesystem implementation detail: no
    /// Windows drive-letter absolute path, no <c>~</c> disambiguator (only ever produced by
    /// <see cref="PersonaStore.Check"/> for a sibling Candidate's synthetic path), and no bare
    /// <c>{Name}.md</c> reference that is not a real, relative <c>Teammates/</c> path. Internal rather
    /// than private so <c>ProposalServiceTests</c> can reuse it for a <see cref="CandidateFailure.Reason"/>
    /// that ultimately came from the same <see cref="CandidateChecker"/> problem text.
    /// </summary>
    internal static void AssertProblemsAreModelFacing(IReadOnlyList<string> problems)
    {
        foreach (var problem in problems)
        {
            Assert.DoesNotMatch(@"[A-Za-z]:[\\/]", problem);
            Assert.DoesNotContain("~", problem, StringComparison.Ordinal);

            foreach (Match match in MdReferenceRegex().Matches(problem))
            {
                // contains-ok: proving the reference is a real, DataDir-relative Teammates/ path,
                // not a Windows-absolute or bare filename leak - the exact remainder is per-test text.
                Assert.StartsWith("Teammates/", match.Value, StringComparison.Ordinal);
            }
        }
    }

    /// <summary>Matches any <c>.md</c>-suffixed path-like token in a problem string, for <see cref="AssertProblemsAreModelFacing"/>.</summary>
    [GeneratedRegex(@"[A-Za-z0-9_./-]*\.md", RegexOptions.CultureInvariant)]
    private static partial Regex MdReferenceRegex();

    /// <summary>
    /// Bundles the real collaborators one <see cref="CandidateChecker"/> under test needs, torn
    /// down together - <see cref="SqliteTeamDirectory"/> holds no unmanaged resources of its own.
    /// </summary>
    private sealed class Fixture(TempDataDir dataDir, PersonaStore personas, SqliteTeamDirectory directory, FakeAgentGateway gateway, CandidateChecker checker) : IDisposable
    {
        /// <summary>The real <see cref="PersonaStore"/> backing this fixture's Teams directory.</summary>
        public PersonaStore Personas { get; } = personas;

        /// <summary>The real <see cref="SqliteTeamDirectory"/> backing this fixture's Human and Agent rows.</summary>
        public SqliteTeamDirectory Directory { get; } = directory;

        /// <summary>The fake <see cref="IAgentGateway"/> a test can mark Agents online through.</summary>
        public FakeAgentGateway Gateway { get; } = gateway;

        /// <summary>The <see cref="CandidateChecker"/> under test.</summary>
        public CandidateChecker Checker { get; } = checker;

        /// <summary>Disposes the real <see cref="PersonaStore"/> and the underlying temp directory.</summary>
        public void Dispose()
        {
            this.Personas.Dispose();
            dataDir.Dispose();
        }
    }
}
