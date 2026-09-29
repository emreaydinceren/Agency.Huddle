using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// Functional tests for <see cref="TeamMembership"/> (Spec §6.2, §8.2; E-6, E-7, E-16): a real
/// <see cref="PersonaStore"/> on a temporary data directory, a <see cref="FakeTeamCatalog"/> for the
/// Team spellings (correction 5: no timing), and whole-text assertions on the definition file, so a
/// write that touches anything but the <c>teams</c> field fails. Every definition file, model and
/// effort is seeded BEFORE the store is constructed, and each event count unsubscribes the instant
/// the call returns, because <c>PersonaStore.Update</c> raises a second, unconditional
/// <c>PersonasChanged</c> about 500 ms later from its file watcher.
/// <para>
/// NOT COVERED: (1) the <c>Rejected</c>-from-<c>Update</c> path (a caught <c>ChatException</c>,
/// <c>IOException</c> or <c>UnauthorizedAccessException</c>): <c>Update</c> throws only for blank
/// text, an unknown name or text that will not load, and <c>Add</c>/<c>Remove</c> cannot produce any
/// of them from a valid definition, and an IO failure needs a held file that the persona watcher
/// races. (2) The lost update WITHOUT the read-modify-write lock has no deterministic mutation: the
/// concurrency row is probabilistic. (3) The card-vs-membership last-write-wins race (S17,
/// correction 13): <c>TeammateCard</c> saves its stale text unconditionally; accepted, recorded in
/// the Spec by Task 8.2, no code.
/// </para>
/// </summary>
public sealed class TeamMembershipTests
{
    private const string UnwritableText = "This Team's name can't be written to a Teammate's definition.";

    /// <summary>Adding a Persona to a Team rewrites only the <c>teams</c> line, quoting every value, and leaves every other file and byte alone.</summary>
    [Fact]
    public void Add_NewMember_WritesOnlyTheTeamsField()
    {
        string original = Definition("Nova", "\n", "teams: [Research]");
        string ada = Definition("Ada", "\n", "teams: [Ops]");
        using Fixture f = new([("nova", original), ("ada", ada)]);

        (MembershipResult result, int raised) = f.Observe(() => f.Membership.Add("Business", "Nova"));

        Assert.Equal(new MembershipResult(MembershipOutcome.Added), result);
        Assert.Equal(Definition("Nova", "\n", "teams: ['Research', 'Business']"), f.Read("nova"));
        Assert.Equal(ada, f.Read("ada"));
        Assert.Equal(1, raised);
    }

    /// <summary>The replaced line keeps its key spelling (<c>Teams:</c>) and its line ending, and every other line stays byte-identical, in a CRLF file.</summary>
    [Fact]
    public void Add_KeepsTheKeySpellingAndTheLineEndings()
    {
        using Fixture f = new([("nova", Definition("Nova", "\r\n", "Teams: [Research]"))]);

        MembershipResult result = f.Membership.Add("Business", "Nova");

        Assert.Equal(MembershipOutcome.Added, result.Outcome);
        Assert.Equal(Definition("Nova", "\r\n", "Teams: ['Research', 'Business']"), f.Read("nova"));
    }

    /// <summary>A Persona without a <c>teams</c> key gets one, immediately before the closing <c>---</c>, using the previous line's line ending.</summary>
    /// <param name="eol">The line ending of the seeded file.</param>
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void Add_PersonaWithNoTeamsField_AddsTheKey(string eol)
    {
        using Fixture f = new([("nova", Definition("Nova", eol))]);

        MembershipResult result = f.Membership.Add("Business", "Nova");

        Assert.Equal(MembershipOutcome.Added, result.Outcome);
        Assert.Equal(Definition("Nova", eol, "teams: ['Business']"), f.Read("nova"));
    }

    /// <summary>The new key goes after every other key, including the built-in marker, and the marker survives (S12).</summary>
    [Fact]
    public void Add_BuiltinPersona_WritesTheLabelAndKeepsTheMarker()
    {
        using Fixture f = new([("nova", Definition("Nova", "\n", "_builtin: true"))]);

        MembershipResult result = f.Membership.Add("Business", "Nova");

        Assert.Equal(MembershipOutcome.Added, result.Outcome);
        Assert.Equal(Definition("Nova", "\n", "_builtin: true", "teams: ['Business']"), f.Read("nova"));
    }

    /// <summary>A typed spelling that differs in case from an existing Team folder is written with the catalog's spelling.</summary>
    [Fact]
    public void Add_UsesTheCatalogSpelling()
    {
        using Fixture f = new([("nova", Definition("Nova", "\n"))]);
        f.Catalog.Teams = [new TeamSummary("Business", [], [], HasFolder: true)];

        MembershipResult result = f.Membership.Add("business", "Nova");

        Assert.Equal(MembershipOutcome.Added, result.Outcome);
        Assert.Equal(Definition("Nova", "\n", "teams: ['Business']"), f.Read("nova"));
    }

    /// <summary>A Team the catalog does not list yet is written exactly as typed.</summary>
    [Fact]
    public void Add_TeamNotInCatalog_WritesTheNameAsTyped()
    {
        using Fixture f = new([("nova", Definition("Nova", "\n"))]);
        f.Catalog.Teams = [new TeamSummary("Business", [], [], HasFolder: true)];

        MembershipResult result = f.Membership.Add("sales", "Nova");

        Assert.Equal(MembershipOutcome.Added, result.Outcome);
        Assert.Equal(Definition("Nova", "\n", "teams: ['sales']"), f.Read("nova"));
    }

    /// <summary>A Persona already carrying the label (any case) is reported as such: nothing on disk changes, not even the write time, and no <c>PersonasChanged</c> fires.</summary>
    /// <param name="typed">The Team as the caller types it.</param>
    [Theory]
    [InlineData("Business")]
    [InlineData("business")]
    public void Add_AlreadyMember_ReturnsAlreadyMemberAndWritesNothing(string typed)
    {
        using Fixture f = new([("nova", Definition("Nova", "\n", "teams: [BUSINESS]"))]);
        var before = f.Snapshot();

        (MembershipResult result, int raised) = f.Observe(() => f.Membership.Add(typed, "Nova"));

        Assert.Equal(MembershipOutcome.AlreadyMember, result.Outcome);
        Assert.Equal(before, f.Snapshot());
        Assert.Equal(0, raised);
    }

    /// <summary>Adding an unknown Persona is <c>NotFound</c>; nothing on disk changes and no event fires.</summary>
    [Fact]
    public void Add_UnknownPersona_ReturnsNotFound()
    {
        using Fixture f = new([("nova", Definition("Nova", "\n", "teams: [Research]"))]);
        var before = f.Snapshot();

        (MembershipResult result, int raised) = f.Observe(() => f.Membership.Add("Business", "Ghost"));

        Assert.Equal(MembershipOutcome.NotFound, result.Outcome);
        Assert.Equal(before, f.Snapshot());
        Assert.Equal(0, raised);
    }

    /// <summary>Removing an unknown Persona is <c>NotFound</c>; nothing on disk changes and no event fires.</summary>
    [Fact]
    public void Remove_UnknownPersona_ReturnsNotFound()
    {
        using Fixture f = new([("nova", Definition("Nova", "\n", "teams: [Business]"))]);
        var before = f.Snapshot();

        (MembershipResult result, int raised) = f.Observe(() => f.Membership.Remove("Business", "Ghost"));

        Assert.Equal(MembershipOutcome.NotFound, result.Outcome);
        Assert.Equal(before, f.Snapshot());
        Assert.Equal(0, raised);
    }

    /// <summary>An <c>Add</c> raises <c>PersonasChanged</c> exactly once on the calling thread (the watcher's later echo is excluded by unsubscribing at once).</summary>
    [Fact]
    public void Add_RaisesPersonasChangedExactlyOnce()
    {
        using Fixture f = new([("nova", Definition("Nova", "\n", "teams: [Research]"))]);

        (MembershipResult result, int raised) = f.Observe(() => f.Membership.Add("Business", "Nova"));

        Assert.Equal(MembershipOutcome.Added, result.Outcome);
        Assert.Equal(1, raised);
    }

    /// <summary>A <c>Remove</c> raises <c>PersonasChanged</c> exactly once on the calling thread.</summary>
    [Fact]
    public void Remove_RaisesPersonasChangedExactlyOnce()
    {
        using Fixture f = new([("nova", Definition("Nova", "\n", "teams: [Research, Business]"))]);

        (MembershipResult result, int raised) = f.Observe(() => f.Membership.Remove("Business", "Nova"));

        Assert.Equal(new MembershipResult(MembershipOutcome.Removed), result);
        Assert.Equal(1, raised);
    }

    /// <summary>The stored Model and Effort survive an <c>Add</c> (the write goes through <c>Update(name, text, model, effort)</c> with the current values).</summary>
    [Fact]
    public void Add_PreservesModelAndEffort()
    {
        using Fixture f = new([("nova", Definition("Nova", "\n", "teams: [Research]"))], model: "claude-opus-4", effort: "high");

        MembershipResult result = f.Membership.Add("Business", "Nova");

        Persona? stored = f.Personas.Get("Nova");
        Assert.Equal(MembershipOutcome.Added, result.Outcome);
        Assert.NotNull(stored);
        Assert.Equal("claude-opus-4", stored.Model);
        Assert.Equal("high", stored.Effort);
    }

    /// <summary>The stored Model and Effort survive a <c>Remove</c> too.</summary>
    [Fact]
    public void Remove_PreservesModelAndEffort()
    {
        using Fixture f = new([("nova", Definition("Nova", "\n", "teams: [Research, Business]"))], model: "claude-opus-4", effort: "high");

        MembershipResult result = f.Membership.Remove("Business", "Nova");

        Persona? stored = f.Personas.Get("Nova");
        Assert.Equal(MembershipOutcome.Removed, result.Outcome);
        Assert.NotNull(stored);
        Assert.Equal("claude-opus-4", stored.Model);
        Assert.Equal("high", stored.Effort);
    }

    /// <summary>Removing a Team drops the label whatever case it is written in, and keeps every other label (a plain pin, not a mutation target: the parser already merges case variants).</summary>
    [Fact]
    public void Remove_DropsEveryCaseVariant()
    {
        using Fixture f = new([("nova", Definition("Nova", "\n", "teams: [Business, business, Ops]"))]);

        MembershipResult result = f.Membership.Remove("BUSINESS", "Nova");

        Assert.Equal(MembershipOutcome.Removed, result.Outcome);
        Assert.Equal(Definition("Nova", "\n", "teams: ['Ops']"), f.Read("nova"));
    }

    /// <summary>Removing the last label removes the <c>teams</c> key entirely, not an empty <c>teams: []</c>.</summary>
    [Fact]
    public void Remove_LastLabel_RemovesTheTeamsKey()
    {
        using Fixture f = new([("nova", Definition("Nova", "\n", "teams: [Business]"))]);

        MembershipResult result = f.Membership.Remove("Business", "Nova");

        Assert.Equal(MembershipOutcome.Removed, result.Outcome);
        Assert.Equal(Definition("Nova", "\n"), f.Read("nova"));
    }

    /// <summary>Removing a Team the Persona is not in reports <c>NotMember</c>, whether or not it has a <c>teams</c> line; nothing changes and no event fires.</summary>
    /// <param name="teamsLine">The <c>teams</c> line of the seeded file, or empty for none.</param>
    [Theory]
    [InlineData("teams: [Research]")]
    [InlineData("")]
    public void Remove_NotMember_ReturnsNotMemberAndWritesNothing(string teamsLine)
    {
        ArgumentNullException.ThrowIfNull(teamsLine);
        string text = teamsLine.Length == 0 ? Definition("Nova", "\n") : Definition("Nova", "\n", teamsLine);
        using Fixture f = new([("nova", text)]);
        var before = f.Snapshot();

        (MembershipResult result, int raised) = f.Observe(() => f.Membership.Remove("Business", "Nova"));

        Assert.Equal(MembershipOutcome.NotMember, result.Outcome);
        Assert.Equal(before, f.Snapshot());
        Assert.Equal(0, raised);
    }

    /// <summary>A Team name holding a comma, a semicolon or a control character would split or break the <c>teams</c> line, so it is <c>Rejected</c> with the settled text and nothing is written.</summary>
    /// <param name="team">The unwritable Team name.</param>
    [Theory]
    [InlineData("Ops,Sales")]
    [InlineData("Ops;Sales")]
    [InlineData("Ops\nSales")]
    [InlineData("Ops\rSales")]
    [InlineData("Ops\tSales")]
    [InlineData("Ops\u0001Sales")]
    public void Add_TeamNameWithUnwritableCharacter_ReturnsRejectedWithTheUnwritableText(string team)
    {
        using Fixture f = new([("nova", Definition("Nova", "\n", "teams: [Research]"))]);
        var before = f.Snapshot();

        (MembershipResult result, int raised) = f.Observe(() => f.Membership.Add(team, "Nova"));

        Assert.Equal(new MembershipResult(MembershipOutcome.Rejected, UnwritableText), result);
        Assert.Equal(before, f.Snapshot());
        Assert.Equal(0, raised);
    }

    /// <summary>The characters that DO round-trip (quotes, brackets, colon, hash, spaces inside) are accepted and written with the quote doubled: the allowed neighbour of the unwritable rows.</summary>
    [Fact]
    public void Add_TeamNameWithQuoteAndBrackets_IsWrittenQuoted()
    {
        using Fixture f = new([("nova", Definition("Nova", "\n", "teams: [Research]"))]);

        MembershipResult result = f.Membership.Add("O'Brien #1: [Ops]", "Nova");

        Assert.Equal(MembershipOutcome.Added, result.Outcome);
        Assert.Equal(Definition("Nova", "\n", "teams: ['Research', 'O''Brien #1: [Ops]']"), f.Read("nova"));
    }

    /// <summary>A blank label, or one with leading or trailing whitespace, is <c>Rejected</c> with the settled text and nothing is written (the parser would trim or drop it).</summary>
    /// <param name="team">The blank or untrimmed Team name.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" Business")]
    [InlineData("Business ")]
    public void Add_BlankOrUntrimmedTeam_ReturnsRejected(string team)
    {
        using Fixture f = new([("nova", Definition("Nova", "\n", "teams: [Research]"))]);
        f.Catalog.Teams = [new TeamSummary("Business", [], [], HasFolder: true)];
        var before = f.Snapshot();

        (MembershipResult result, int raised) = f.Observe(() => f.Membership.Add(team, "Nova"));

        Assert.Equal(new MembershipResult(MembershipOutcome.Rejected, UnwritableText), result);
        Assert.Equal(before, f.Snapshot());
        Assert.Equal(0, raised);
    }

    /// <summary>The allowed neighbour of the blank/untrimmed rows: the same Team, trimmed, is <c>Added</c>.</summary>
    [Fact]
    public void Add_TrimmedTeam_StillReturnsAdded()
    {
        using Fixture f = new([("nova", Definition("Nova", "\n", "teams: [Research]"))]);
        f.Catalog.Teams = [new TeamSummary("Business", [], [], HasFolder: true)];

        MembershipResult result = f.Membership.Add("Business", "Nova");

        Assert.Equal(MembershipOutcome.Added, result.Outcome);
        Assert.Equal(Definition("Nova", "\n", "teams: ['Research', 'Business']"), f.Read("nova"));
    }

    /// <summary>
    /// Sixteen simultaneous <c>Add</c> calls for distinct Teams on one Persona leave all sixteen labels
    /// (plus the original) in the file. PROBABILISTIC: without the read-modify-write lock a lost
    /// update is likely but not certain on any one run, so this row can pass against a broken
    /// implementation; it cannot fail against a correct one.
    /// </summary>
    /// <returns>A task that completes when every call and the assertions have finished.</returns>
    [Fact]
    public async Task Add_ConcurrentAddsOnOnePersona_KeepsEveryLabel()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = new([("nova", Definition("Nova", "\n", "teams: [Research]"))]);
        string[] teams = [.. Enumerable.Range(0, 16).Select(static i => $"Team{i:D2}")];

        MembershipResult[] results = await Task.WhenAll(teams.Select(team => Task.Run(() => f.Membership.Add(team, "Nova"), ct)));

        Assert.All(results, r => Assert.Equal(MembershipOutcome.Added, r.Outcome));
        Persona? stored = f.Personas.Get("Nova");
        Assert.NotNull(stored);
        Assert.True(PersonaFrontmatter.TryReadIdentity(stored.Text, out PersonaIdentity? identity, out _));
        string[] expected = ["Research", .. teams];
        string[] actual = [.. identity.Teams.Order(StringComparer.Ordinal)];
        Assert.Equal(expected, actual);
    }

    /// <summary>Builds a Persona definition: <c>Name</c>, <c>Title</c>, <c>Alias</c> (lower-cased Name), then <paramref name="frontmatter"/> lines, then a one-line body.</summary>
    /// <param name="name">The Persona's Name.</param>
    /// <param name="eol">The line ending used between every line.</param>
    /// <param name="frontmatter">Extra frontmatter lines, after <c>Alias</c>.</param>
    private static string Definition(string name, string eol, params ReadOnlySpan<string> frontmatter)
    {
        List<string> lines = ["---", $"Name: {name}", $"Title: {name}", $"Alias: {name.ToLowerInvariant()}"];
        foreach (string line in frontmatter)
        {
            lines.Add(line);
        }

        lines.Add("---");
        lines.Add($"You are {name}.");
        return string.Join(eol, lines);
    }

    /// <summary>A temporary data directory, a real <see cref="PersonaStore"/> seeded before it starts, a <see cref="FakeTeamCatalog"/> and the service under test.</summary>
    private sealed class Fixture : IDisposable
    {
        private static readonly DateTime SeededWriteTime = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private readonly TempDataDir dir = new();
        private readonly TeammatePaths paths;

        /// <summary>Seeds every definition, model and effort, THEN constructs the store.</summary>
        /// <param name="files">The definition files as (stem, text) pairs.</param>
        /// <param name="model">The stored Model of "Nova", or null for none.</param>
        /// <param name="effort">The stored Effort of "Nova", or null for none.</param>
        public Fixture(IReadOnlyList<(string Stem, string Text)> files, string? model = null, string? effort = null)
        {
            IOptions<TeamOptions> options = this.dir.Options();
            this.paths = new TeammatePaths(options);
            foreach ((string stem, string text) in files)
            {
                TestPersonaFiles.Write(this.paths, stem, text);
                File.SetLastWriteTimeUtc(this.paths.DefinitionFile(stem), SeededWriteTime);
            }

            PersonaModelStore models = new(options);
            PersonaEffortStore efforts = new(options);
            models.Set("Nova", model);
            efforts.Set("Nova", effort);
            this.Personas = new PersonaStore(this.paths, models, efforts, NullLogger<PersonaStore>.Instance);
            this.Catalog = new FakeTeamCatalog();
            this.Membership = new TeamMembership(this.Personas, this.Catalog);
        }

        /// <summary>The real store the service writes through.</summary>
        public PersonaStore Personas { get; }

        /// <summary>The Team list the service resolves spellings from.</summary>
        public FakeTeamCatalog Catalog { get; }

        /// <summary>The service under test.</summary>
        public TeamMembership Membership { get; }

        /// <summary>Reads a definition file's text as it is on disk.</summary>
        /// <param name="stem">The Persona's file stem.</param>
        public string Read(string stem) => File.ReadAllText(this.paths.DefinitionFile(stem));

        /// <summary>Every file under the definitions root with its write time and text, so an unchanged snapshot proves no write and no stray temp file.</summary>
        public (string Path, DateTime Written, string Text)[] Snapshot() =>
            [.. Directory.EnumerateFiles(this.paths.DefinitionsRoot, "*", SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal)
                .Select(file => (Path.GetRelativePath(this.paths.DefinitionsRoot, file), File.GetLastWriteTimeUtc(file), File.ReadAllText(file)))];

        /// <summary>Runs <paramref name="call"/> and counts <c>PersonasChanged</c> raised until it returns, then unsubscribes at once.</summary>
        /// <param name="call">The membership call to observe.</param>
        public (MembershipResult Result, int Raised) Observe(Func<MembershipResult> call)
        {
            int raised = 0;
            void Count() => Interlocked.Increment(ref raised);
            this.Personas.PersonasChanged += Count;
            MembershipResult result = call();
            this.Personas.PersonasChanged -= Count;
            return (result, raised);
        }

        /// <summary>Disposes the store, then deletes the data directory.</summary>
        public void Dispose()
        {
            this.Personas.Dispose();
            this.dir.Dispose();
        }
    }
}
