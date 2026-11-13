using Agency.Huddle.App.References;
using Agency.Huddle.Contracts;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Agents;

namespace Agency.Huddle.Tests.References;

/// <summary>Pins R11 for References: a Team Reference or a Library-file Reference that names a renamed Team by its old name resolves to today's Team.</summary>
public sealed class ReferenceResolverRenameTests
{
    private static ReferenceCandidate Candidate(ReferenceKind kind, string key) => new(kind, key, 0);

    private static ReferenceResolver ResolverWithJournal(ReferenceFixture fixture, TempDataDir typesDir, TeamRenameStore journal) =>
        new(fixture.Tasks, fixture.Prefixes, fixture.Directory, fixture.Catalog, RoleFixtures.Types(typesDir), fixture.LibraryResolver, fixture.Options, journal);

    private static TeamRenameStore Journal(ReferenceFixture fixture)
    {
        TeamRenameStore store = new(fixture.Options);
        store.Complete(store.Begin(TeamRenameKind.Team, "Marketing", "Marketing", "Growth"));
        return store;
    }

    /// <summary>A Team Reference naming the old Team resolves to the renamed Team.</summary>
    [Fact]
    public async Task ResolveTeam_OldName_ResolvesToGrowth()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct);
        using TempDataDir typesDir = new();
        fixture.Catalog.Teams = [new TeamSummary("Growth", [], ["Nova"], true)];

        IReadOnlyList<ResolvedReference> result = await ResolverWithJournal(fixture, typesDir, Journal(fixture))
            .ResolveAsync([Candidate(ReferenceKind.Team, "Marketing")], ct);

        ResolvedTeam team = Assert.IsType<ResolvedTeam>(Assert.Single(result));
        Assert.Equal("Growth", team.Name);
    }

    /// <summary>A Team Reference to a name that was never journalled and is not in the catalog stays gone.</summary>
    [Fact]
    public async Task ResolveTeam_NeverJournalledUnknown_StaysGone()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct);
        using TempDataDir typesDir = new();
        fixture.Catalog.Teams = [new TeamSummary("Growth", [], ["Nova"], true)];

        IReadOnlyList<ResolvedReference> result = await ResolverWithJournal(fixture, typesDir, Journal(fixture))
            .ResolveAsync([Candidate(ReferenceKind.Team, "Research")], ct);

        Assert.Equal([new GoneReference(ReferenceKind.Team, "Research")], result);
    }

    /// <summary>A Library-file Reference under the old Team folder resolves to the file under the renamed folder.</summary>
    [Fact]
    public async Task ResolveFile_UnderOldTeam_ResolvesUnderGrowth()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct);
        using TempDataDir typesDir = new();
        string folder = Path.Combine(fixture.DataDir, "Teams", "Growth");
        Directory.CreateDirectory(folder);
        string present = Path.Combine(folder, "plan.md");
        await File.WriteAllTextAsync(present, "x", ct);
        string old = Path.Combine(fixture.DataDir, "Teams", "Marketing", "plan.md");

        IReadOnlyList<ResolvedReference> result = await ResolverWithJournal(fixture, typesDir, Journal(fixture))
            .ResolveAsync([Candidate(ReferenceKind.LibraryFile, old)], ct);

        Assert.Equal([new ResolvedLibraryFile(present, "plan.md")], result);
    }
}
