using System.Text;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Search;

namespace Agency.Huddle.Tests.Library;

/// <summary>
/// Tests for <c>SearchTextAsync</c> (Global Search spec §8.3): the name walk
/// <see cref="LibraryFileService.FindAsync"/> uses, plus the content test for a file whose name did not match.
/// </summary>
public sealed class LibraryFileServiceSearchTests
{
    /// <summary>Writes <paramref name="text"/> to the file at <paramref name="relativePath"/> under <paramref name="folder"/>, with any folders it needs.</summary>
    /// <param name="folder">The absolute folder to write under.</param>
    /// <param name="relativePath">The forward-slash path of the file to create.</param>
    /// <param name="text">The text to write.</param>
    /// <param name="encoding">The encoding to write with; UTF-8 without a byte-order mark when omitted.</param>
    private static void Write(string folder, string relativePath, string text, Encoding? encoding = null)
    {
        string full = Path.Combine(folder, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full) ?? folder);
        File.WriteAllText(full, text, encoding ?? new UTF8Encoding(false));
    }

    /// <summary>The absolute Business Team folder under the fixture's Teams root.</summary>
    /// <param name="fixture">The fixture whose Teams root holds the folder.</param>
    private static string Business(LibraryFileServiceFixture fixture) => Path.Combine(fixture.DataDir, "Teams", "Business");

    /// <summary>The root-relative paths of <paramref name="result"/>'s hits, in order.</summary>
    /// <param name="result">The search result to project.</param>
    private static string[] HitPaths(LibraryTextSearchResult result) => [.. result.Hits.Select(h => h.Entry.Path.RelativePath)];

    /// <summary>A file whose name matches is a name hit with no snippet, though its contents contain the phrase too: it is not read.</summary>
    [Fact]
    public async Task NameHit_IsReturnedWithoutReadingTheFile()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        Write(Business(fixture), "planning.md", "the plan is here");
        LibraryFileService service = fixture.CreateService();

        LibraryTextSearchResult result = await service.SearchTextAsync(fixture.ResolveTeams("Business"), "plan", 10, new SearchOptions(), null, ct);

        LibraryTextHit hit = Assert.Single(result.Hits);
        Assert.Equal("Business/planning.md", hit.Entry.Path.RelativePath);
        Assert.Equal(MatchTier.NamePrefix, hit.Tier);
        Assert.Null(hit.Snippet);
    }

    /// <summary>A file whose name does not match but whose text does is a content hit carrying the matching line.</summary>
    [Fact]
    public async Task ContentHit_CarriesTheSnippet()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        Write(Business(fixture), "notes.md", "first line\r\n  The budget review is due  \r\nlast line");
        LibraryFileService service = fixture.CreateService();

        LibraryTextSearchResult result = await service.SearchTextAsync(fixture.ResolveTeams("Business"), "BUDGET", 10, new SearchOptions(), null, ct);

        LibraryTextHit hit = Assert.Single(result.Hits);
        Assert.Equal("Business/notes.md", hit.Entry.Path.RelativePath);
        Assert.Equal(MatchTier.ContentMatch, hit.Tier);
        Assert.Equal("The budget review is due", hit.Snippet);
        Assert.False(result.Truncated);
    }

    /// <summary>A text file over the size limit is not read, so its text never hits, and the result is truncated.</summary>
    [Fact]
    public async Task OverSizeFile_NameOnlyAndTruncated()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        Write(Business(fixture), "big.md", "needle in a big file");
        LibraryFileService service = fixture.CreateService();
        SearchOptions search = new() { MaxContentFileBytes = 5 };

        LibraryTextSearchResult result = await service.SearchTextAsync(fixture.ResolveTeams("Business"), "needle", 10, search, null, ct);

        Assert.Empty(result.Hits);
        Assert.True(result.Truncated);
    }

    /// <summary>A file whose extension is not a configured text extension is never read, and does not truncate the result.</summary>
    [Fact]
    public async Task NonTextExtension_NeverRead()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        Write(Business(fixture), "data.bin", "needle in a binary");
        LibraryFileService service = fixture.CreateService();

        LibraryTextSearchResult result = await service.SearchTextAsync(fixture.ResolveTeams("Business"), "needle", 10, new SearchOptions(), null, ct);

        Assert.Empty(result.Hits);
        Assert.False(result.Truncated);
    }

    /// <summary>The visit budget is <c>Search.MaxVisitedEntries</c>, not <c>Library.MaxIndexedFiles</c> (OP-2): a small search budget cuts the walk short while the library budget is large.</summary>
    [Fact]
    public async Task MaxVisitedEntries_SetsTruncated()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build(o => o.Library.MaxIndexedFiles = 1000);
        for (int i = 0; i < 6; i++)
        {
            Write(Business(fixture), $"file{i}.md", "nothing");
        }

        LibraryFileService service = fixture.CreateService();
        SearchOptions search = new() { MaxVisitedEntries = 3 };

        LibraryTextSearchResult result = await service.SearchTextAsync(fixture.ResolveTeams("Business"), "file", 100, search, null, ct);

        Assert.Equal(3, result.Hits.Count);
        Assert.True(result.Truncated);
    }

    /// <summary>A cancelled search stops at the next entry, not at the next file read: entries the exclusion rule skips never reach a read, yet the walk still ends at once (D1).</summary>
    [Fact]
    public async Task Cancelled_StopsAtTheNextEntry()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        for (int i = 0; i < 200; i++)
        {
            Write(Business(fixture), $"file{i}.md", "nothing");
        }

        LibraryFileService service = fixture.CreateService();
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        int visited = 0;
        Predicate<LibraryEntry> cancelAfterFive = _ =>
        {
            if (++visited == 5)
            {
                cts.Cancel();
            }

            return true;
        };

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.SearchTextAsync(fixture.ResolveTeams("Business"), "zzz", 10, new SearchOptions(), cancelAfterFive, cts.Token));

        Assert.True(visited <= 6, $"The walk visited {visited} entries after cancellation.");
    }

    /// <summary>The walk applies the tree's hiding rules: <c>.git</c>, <c>.obsidian</c> and <c>_tasks</c> are never walked, so their text never hits.</summary>
    [Fact]
    public async Task HiddenFolders_NeverWalked()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string business = Business(fixture);
        Write(business, ".git/config.md", "needle");
        Write(business, ".obsidian/app.md", "needle");
        Write(business, "_tasks/task.md", "needle");
        Write(business, "visible.md", "needle");
        LibraryFileService service = fixture.CreateService();

        LibraryTextSearchResult result = await service.SearchTextAsync(fixture.ResolveTeams("Business"), "needle", 10, new SearchOptions(), null, ct);

        Assert.Equal(["Business/visible.md"], HitPaths(result));
    }

    /// <summary>A file another process holds open exclusively cannot be read and is skipped; the other files still hit.</summary>
    [Fact]
    public async Task LockedFile_IsSkipped()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string business = Business(fixture);
        Write(business, "locked.md", "needle");
        Write(business, "open.md", "needle");
        LibraryFileService service = fixture.CreateService();
        using FileStream held = new(Path.Combine(business, "locked.md"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        LibraryTextSearchResult result = await service.SearchTextAsync(fixture.ResolveTeams("Business"), "needle", 10, new SearchOptions(), null, ct);

        Assert.Equal(["Business/open.md"], HitPaths(result));
    }

    /// <summary>A UTF-16 file with a byte-order mark is decoded as the Library shows it, so its text is found.</summary>
    [Fact]
    public async Task Utf16File_IsFound()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        Write(Business(fixture), "wide.txt", "a wide needle", Encoding.Unicode);
        LibraryFileService service = fixture.CreateService();

        LibraryTextSearchResult result = await service.SearchTextAsync(fixture.ResolveTeams("Business"), "needle", 10, new SearchOptions(), null, ct);

        LibraryTextHit hit = Assert.Single(result.Hits);
        Assert.Equal("a wide needle", hit.Snippet);
    }

    /// <summary>An entry the exclusion predicate names is never a hit, by name or by contents, while a folder it names is still walked.</summary>
    [Fact]
    public async Task ExcludedEntry_IsNeverAHit()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string business = Business(fixture);
        Write(business, "persona.md", "needle");
        Write(business, "sub/inner.md", "needle");
        Write(business, "other.md", "needle");
        LibraryFileService service = fixture.CreateService();

        LibraryTextSearchResult result = await service.SearchTextAsync(
            fixture.ResolveTeams("Business"),
            "needle",
            10,
            new SearchOptions(),
            static entry => entry.Path.RelativePath.EndsWith("persona.md", StringComparison.Ordinal) || entry.Path.RelativePath.EndsWith("/sub", StringComparison.Ordinal),
            ct);

        Assert.Equal(["Business/other.md", "Business/sub/inner.md"], HitPaths(result));
    }

    /// <summary>More matches than the cap returns exactly the cap and marks the result truncated.</summary>
    [Fact]
    public async Task MaxHits_SetsTruncated()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        for (int i = 0; i < 4; i++)
        {
            Write(Business(fixture), $"n{i}.md", "needle");
        }

        LibraryFileService service = fixture.CreateService();

        LibraryTextSearchResult result = await service.SearchTextAsync(fixture.ResolveTeams("Business"), "needle", 2, new SearchOptions(), null, ct);

        Assert.Equal(2, result.Hits.Count);
        Assert.True(result.Truncated);
    }

    /// <summary>Cancelling while the first file is being considered stops the walk before that file or any other is read.</summary>
    [Fact]
    public async Task Cancelled_StopsWithinOneFile()
    {
        using CancellationTokenSource cts = new();
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        for (int i = 0; i < 3; i++)
        {
            Write(Business(fixture), $"f{i}.md", "needle");
        }

        LibraryFileService service = fixture.CreateService();
        int considered = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SearchTextAsync(
            fixture.ResolveTeams("Business"),
            "needle",
            10,
            new SearchOptions(),
            entry =>
            {
                considered++;
                cts.Cancel();
                return false;
            },
            cts.Token));

        Assert.Equal(1, considered);
    }
}
