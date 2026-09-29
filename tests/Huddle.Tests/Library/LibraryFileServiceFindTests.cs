using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>
/// Tests for <see cref="LibraryFileService.FindAsync"/> (Spec §6.8, Task 6.2.t as corrected by
/// corrections-D6 items 1-10): a name search inside one scope that applies exactly the tree's own
/// hiding rules, walks breadth-first, and stops at <c>max</c> hits or at
/// <see cref="LibraryOptions.MaxIndexedFiles"/> visited entries.
/// </summary>
public sealed class LibraryFileServiceFindTests
{
    /// <summary>Creates the file at <paramref name="relativePath"/> under <paramref name="folder"/>, with any folders it needs.</summary>
    /// <param name="folder">The absolute folder to write under.</param>
    /// <param name="relativePath">The forward-slash path of the file to create.</param>
    private static void Touch(string folder, string relativePath)
    {
        string full = Path.Combine(folder, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full) ?? folder);
        File.WriteAllText(full, string.Empty);
    }

    /// <summary>Lays out the Business Team folder used by most rows, under the fixture's Teams root.</summary>
    /// <param name="fixture">The fixture whose Teams root receives the folder.</param>
    private static void WriteBusiness(LibraryFileServiceFixture fixture)
    {
        string business = Path.Combine(fixture.DataDir, "Teams", "Business");
        Touch(business, "brief.md");
        Touch(business, "notes/brief-2.md");
        Touch(business, "_tasks/brief-task.md");
        Touch(business, "_brief/brief.md");
        Touch(business, "memory/brief-fact.md");
    }

    /// <summary>The root-relative paths of <paramref name="result"/>'s hits, in order.</summary>
    /// <param name="result">The search result to project.</param>
    private static string[] HitPaths(LibrarySearchResult result) => [.. result.Hits.Select(h => h.Path.RelativePath)];

    /// <summary>Plan row 1: hits come breadth-first with folders first at each level; a hidden <c>_</c> folder (even one whose own name matches) and what is inside it are not hits.</summary>
    [Fact]
    public async Task FindAsync_Teams_BreadthFirstFoldersFirst_SkipsUnderscoreFolders()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        WriteBusiness(fixture);
        LibraryFileService service = fixture.CreateService();
        LibraryPath scope = fixture.ResolveTeams("Business");

        LibrarySearchResult result = await service.FindAsync(scope, "brief", 200, ct);

        Assert.Equal(["Business/brief.md", "Business/memory/brief-fact.md", "Business/notes/brief-2.md"], HitPaths(result));
        Assert.False(result.Truncated);
    }

    /// <summary>Plan row 2: the match ignores case, so <c>BRIEF</c> gives the same hits as <c>brief</c>.</summary>
    [Fact]
    public async Task FindAsync_UpperCaseTerm_GivesTheSameHits()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        WriteBusiness(fixture);
        LibraryFileService service = fixture.CreateService();
        LibraryPath scope = fixture.ResolveTeams("Business");

        LibrarySearchResult result = await service.FindAsync(scope, "BRIEF", 200, ct);

        Assert.Equal(["Business/brief.md", "Business/memory/brief-fact.md", "Business/notes/brief-2.md"], HitPaths(result));
    }

    /// <summary>The term is trimmed before matching, so padding around it does not change the hits.</summary>
    [Fact]
    public async Task FindAsync_PaddedTerm_IsTrimmed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        WriteBusiness(fixture);
        LibraryFileService service = fixture.CreateService();
        LibraryPath scope = fixture.ResolveTeams("Business");

        LibrarySearchResult result = await service.FindAsync(scope, "  fact ", 200, ct);

        Assert.Equal(["Business/memory/brief-fact.md"], HitPaths(result));
    }

    /// <summary>Plan row 2: a folder whose name matches is a hit, with <c>IsFolder</c> true.</summary>
    [Fact]
    public async Task FindAsync_MatchingFolderName_IsAFolderHit()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        WriteBusiness(fixture);
        LibraryFileService service = fixture.CreateService();
        LibraryPath scope = fixture.ResolveTeams("Business");

        LibrarySearchResult result = await service.FindAsync(scope, "note", 200, ct);

        LibraryEntry hit = Assert.Single(result.Hits);
        Assert.Equal("Business/notes", hit.Path.RelativePath);
        Assert.True(hit.IsFolder);
    }

    /// <summary>Plan row 2 and corrections item 8: the scope folder itself is never a hit, and only the last segment of a hit's path is matched, so a term naming the scope or a parent folder finds no child by that.</summary>
    [Theory]
    [InlineData("Business")]
    [InlineData("teams")]
    public async Task FindAsync_TermNamingTheScopeOrAParent_FindsNothing(string term)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        WriteBusiness(fixture);
        LibraryFileService service = fixture.CreateService();
        LibraryPath scope = fixture.ResolveTeams("Business");

        LibrarySearchResult result = await service.FindAsync(scope, term, 200, ct);

        Assert.Empty(result.Hits);
        Assert.False(result.Truncated);
    }

    /// <summary>Corrections item 8: a term naming a folder matches that folder only, not the files inside it (the match is on the last segment of <c>RelativePath</c>, not the whole path).</summary>
    [Fact]
    public async Task FindAsync_TermNamingAFolder_DoesNotMatchItsChildren()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        WriteBusiness(fixture);
        LibraryFileService service = fixture.CreateService();
        LibraryPath scope = fixture.ResolveTeams("Business");

        LibrarySearchResult result = await service.FindAsync(scope, "memory", 200, ct);

        Assert.Equal(["Business/memory"], HitPaths(result));
    }

    /// <summary>Plan row 3: the <c>memory/</c> folder is visible and searched, so a term found only inside it is a hit.</summary>
    [Fact]
    public async Task FindAsync_MemoryFolder_IsSearched()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        WriteBusiness(fixture);
        LibraryFileService service = fixture.CreateService();
        LibraryPath scope = fixture.ResolveTeams("Business");

        LibrarySearchResult result = await service.FindAsync(scope, "fact", 200, ct);

        Assert.Equal(["Business/memory/brief-fact.md"], HitPaths(result));
    }

    /// <summary>Plan row 4 and corrections item 2: in a pinned root a <c>_x</c> folder is searched (the underscore rule is Teams-only) while <c>.obsidian</c> stays hidden.</summary>
    [Fact]
    public async Task FindAsync_PinnedRoot_SearchesUnderscoreFolder_ButNotObsidian()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        Touch(vault, "_x/brief.md");
        Touch(vault, ".obsidian/brief.md");
        LibraryFileService service = fixture.CreateService();
        LibraryPath scope = fixture.Resolve(vault, string.Empty);

        LibrarySearchResult result = await service.FindAsync(scope, "brief", 200, ct);

        Assert.Equal(["_x/brief.md"], HitPaths(result));
        Assert.False(result.Truncated);
    }

    /// <summary>Plan row 5: <c>max</c> 2 with three matches returns the first two in walk order and reports <c>Truncated</c>.</summary>
    [Fact]
    public async Task FindAsync_MoreMatchesThanMax_ReturnsTheFirstMax_AndTruncated()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        WriteBusiness(fixture);
        LibraryFileService service = fixture.CreateService();
        LibraryPath scope = fixture.ResolveTeams("Business");

        LibrarySearchResult result = await service.FindAsync(scope, "brief", 2, ct);

        Assert.Equal(["Business/brief.md", "Business/memory/brief-fact.md"], HitPaths(result));
        Assert.True(result.Truncated);
    }

    /// <summary>Plan row 5: <c>max</c> 3 with exactly three matches returns all three and is not truncated (a further hit must exist to report it).</summary>
    [Fact]
    public async Task FindAsync_ExactlyMaxMatches_IsNotTruncated()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        WriteBusiness(fixture);
        LibraryFileService service = fixture.CreateService();
        LibraryPath scope = fixture.ResolveTeams("Business");

        LibrarySearchResult result = await service.FindAsync(scope, "brief", 3, ct);

        Assert.Equal(["Business/brief.md", "Business/memory/brief-fact.md", "Business/notes/brief-2.md"], HitPaths(result));
        Assert.False(result.Truncated);
    }

    /// <summary>Plan row 6: with <c>Library:MaxIndexedFiles</c> 3 and five entries the walk stops after visiting three, so only the matches among those are returned and the result is truncated even though <c>max</c> was not reached.</summary>
    [Fact]
    public async Task FindAsync_MoreEntriesThanMaxIndexedFiles_StopsAtTheCap_AndTruncated()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build(o => o.Library.MaxIndexedFiles = 3);
        string vault = fixture.CreatePinnedRoot("Vault");
        Touch(vault, "note-1.md");
        Touch(vault, "note-2.md");
        Touch(vault, "other-1.md");
        Touch(vault, "other-2.md");
        Touch(vault, "other-3.md");
        LibraryFileService service = fixture.CreateService();
        LibraryPath scope = fixture.Resolve(vault, string.Empty);

        LibrarySearchResult result = await service.FindAsync(scope, "note", 10, ct);

        Assert.Equal(["note-1.md", "note-2.md"], HitPaths(result));
        Assert.True(result.Truncated);
    }

    /// <summary>Corrections item 5: <c>MaxIndexedFiles</c> exactly equal to the number of entries is not truncated; the scope itself is not counted.</summary>
    [Fact]
    public async Task FindAsync_EntryCountEqualToMaxIndexedFiles_IsNotTruncated()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build(o => o.Library.MaxIndexedFiles = 3);
        string vault = fixture.CreatePinnedRoot("Vault");
        Touch(vault, "note-1.md");
        Touch(vault, "note-2.md");
        Touch(vault, "other-1.md");
        LibraryFileService service = fixture.CreateService();
        LibraryPath scope = fixture.Resolve(vault, string.Empty);

        LibrarySearchResult result = await service.FindAsync(scope, "note", 10, ct);

        Assert.Equal(["note-1.md", "note-2.md"], HitPaths(result));
        Assert.False(result.Truncated);
    }

    /// <summary>Plan row 7: a blank or whitespace-only term finds nothing and is not truncated.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task FindAsync_BlankTerm_ReturnsNothing(string term)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        WriteBusiness(fixture);
        LibraryFileService service = fixture.CreateService();
        LibraryPath scope = fixture.ResolveTeams("Business");

        LibrarySearchResult result = await service.FindAsync(scope, term, 200, ct);

        Assert.Empty(result.Hits);
        Assert.False(result.Truncated);
    }

    /// <summary>Plan row 7: a <c>max</c> of zero or less returns no hits.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task FindAsync_MaxZeroOrLess_ReturnsNoHits(int max)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        WriteBusiness(fixture);
        LibraryFileService service = fixture.CreateService();
        LibraryPath scope = fixture.ResolveTeams("Business");

        LibrarySearchResult result = await service.FindAsync(scope, "brief", max, ct);

        Assert.Empty(result.Hits);
    }

    /// <summary>Plan row 8 and corrections item 3: with <c>FileChanges:Ignore</c> set to <c>build</c>, <c>build/brief.md</c> is hidden and <c>bin/brief.md</c> (no longer ignored, since the list replaces the default) is found.</summary>
    [Fact]
    public async Task FindAsync_HidesFileChangesIgnoreFolders()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build(o => o.FileChanges.Ignore = ["build"]);
        string vault = fixture.CreatePinnedRoot("Vault");
        Touch(vault, "build/brief.md");
        Touch(vault, "bin/brief.md");
        LibraryFileService service = fixture.CreateService();
        LibraryPath scope = fixture.Resolve(vault, string.Empty);

        LibrarySearchResult result = await service.FindAsync(scope, "brief", 200, ct);

        Assert.Equal(["bin/brief.md"], HitPaths(result));
    }

    /// <summary>Corrections item 5: a scope folder that does not exist yet (a Team with no folder) returns no hits and is not truncated, without throwing.</summary>
    [Fact]
    public async Task FindAsync_MissingScopeFolder_ReturnsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        LibraryFileService service = fixture.CreateService();
        LibraryPath scope = fixture.ResolveTeams("Ghost");

        LibrarySearchResult result = await service.FindAsync(scope, "brief", 200, ct);

        Assert.Empty(result.Hits);
        Assert.False(result.Truncated);
    }

    /// <summary>Task 6.3.t (corrections item 11): a token that is already cancelled makes the search throw <see cref="OperationCanceledException"/> rather than return a hit list. Only the pre-cancelled case is pinned; a mid-walk cancel is not deterministic because <c>ListAsync</c> returns completed tasks.</summary>
    [Fact]
    public async Task FindAsync_CancelledToken_Throws()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        WriteBusiness(fixture);
        LibraryFileService service = fixture.CreateService();
        LibraryPath scope = fixture.ResolveTeams("Business");
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.FindAsync(scope, "brief", 200, cancelled.Token));
    }

    /// <summary>Task 6.3.t (corrections items 7 and 12): a link inside the root pointing back at its own parent (<c>Business/loop</c> to <c>Business</c>) is not walked again, so each real file is a hit exactly once and the walk ends well under the entry cap.</summary>
    [Fact]
    public async Task FindAsync_LinkLoopInsideTheRoot_ReturnsEachFileOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build(o => o.Library.MaxIndexedFiles = 50);
        string vault = fixture.CreatePinnedRoot("Vault");
        Touch(vault, "Business/brief.md");
        Touch(vault, "Business/notes/brief-2.md");
        string link = Path.Combine(vault, "Business", "loop");
        if (!TestLinks.TryCreateLink(link, Path.Combine(vault, "Business")))
        {
            Assert.Skip("This machine cannot create a directory link.");
        }

        try
        {
            LibraryFileService service = fixture.CreateService();
            LibraryPath scope = fixture.Resolve(vault, string.Empty);

            LibrarySearchResult result = await service.FindAsync(scope, "brief", 200, ct);

            Assert.Equal(["Business/brief.md", "Business/notes/brief-2.md"], HitPaths(result));
            Assert.False(result.Truncated);
        }
        finally
        {
            TestLinks.RemoveLink(link);
        }
    }

    /// <summary>Task 6.3.t (corrections item 8): a link is matched on the leaf of its <c>RelativePath</c>, not on the folder it points at, so <c>brief</c> finds the link <c>brief-link</c> and <c>target</c> finds only the real <c>target-folder</c>.</summary>
    [Fact]
    public async Task FindAsync_Link_IsMatchedByItsOwnName_NotItsTargets()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        Touch(vault, "target-folder/inner.md");
        string link = Path.Combine(vault, "brief-link");
        if (!TestLinks.TryCreateLink(link, Path.Combine(vault, "target-folder")))
        {
            Assert.Skip("This machine cannot create a directory link.");
        }

        try
        {
            LibraryFileService service = fixture.CreateService();
            LibraryPath scope = fixture.Resolve(vault, string.Empty);

            LibrarySearchResult byLinkName = await service.FindAsync(scope, "brief", 200, ct);
            LibrarySearchResult byTargetName = await service.FindAsync(scope, "target", 200, ct);

            Assert.Equal(["brief-link"], HitPaths(byLinkName));
            Assert.Equal(["target-folder"], HitPaths(byTargetName));
        }
        finally
        {
            TestLinks.RemoveLink(link);
        }
    }

    /// <summary>Task 6.3.t (corrections item 13): a sub-folder the process cannot list is skipped without throwing, and the hits from its siblings are still returned. Skipped where the denial is not enforced (root on Linux CI).</summary>
    [Fact]
    public async Task FindAsync_UnlistableFolder_IsSkipped_AndSiblingsStillFound()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        Touch(vault, "a-locked/brief-hidden.md");
        Touch(vault, "b-open/brief.md");
        Touch(vault, "brief-root.md");
        string locked = Path.Combine(vault, "a-locked");
        LibraryFileService service = fixture.CreateService();
        LibraryPath scope = fixture.Resolve(vault, string.Empty);
        TestListing.DenyListing(locked);

        try
        {
            if (!TestListing.ListingIsDenied(locked))
            {
                Assert.Skip("Listing denial is not enforced for this user.");
            }

            LibrarySearchResult result = await service.FindAsync(scope, "brief", 200, ct);

            Assert.Equal(["brief-root.md", "b-open/brief.md"], HitPaths(result));
            Assert.False(result.Truncated);
        }
        finally
        {
            TestListing.GrantListing(locked);
        }
    }

    /// <summary>Corrections item 6: the scope is re-resolved from its <c>Root</c> and <c>RelativePath</c>, never trusting the caller's <c>FullPath</c>, so a scope pointing its <c>FullPath</c> at another folder still searches the folder its relative path names.</summary>
    [Fact]
    public async Task FindAsync_BogusFullPath_IsReResolvedFromTheRelativePath()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        WriteBusiness(fixture);
        Touch(Path.Combine(fixture.DataDir, "Teams", "Other"), "brief-other.md");
        LibraryFileService service = fixture.CreateService();
        LibraryPath real = fixture.ResolveTeams("Business");
        LibraryPath bogus = real with { FullPath = Path.Combine(fixture.DataDir, "Teams", "Other") };

        LibrarySearchResult result = await service.FindAsync(bogus, "brief", 200, ct);

        Assert.Equal(["Business/brief.md", "Business/memory/brief-fact.md", "Business/notes/brief-2.md"], HitPaths(result));
    }
}
