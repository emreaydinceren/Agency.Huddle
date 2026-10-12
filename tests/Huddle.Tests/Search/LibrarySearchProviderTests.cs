using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Search;
using Agency.Huddle.Tests.Library;

namespace Agency.Huddle.Tests.Search;

/// <summary>
/// The Library provider's half of GS-T4 (Global Search spec §8.3 and §8.6): the Persona and Task
/// exclusions, the Team derivation, the visible-root scope, the switches, and the parallel walk.
/// </summary>
public sealed class LibrarySearchProviderTests
{
    /// <summary>A Task file sits under the Teams root, where the walk never reaches it; it is excluded by the Tasks rule regardless.</summary>
    [Fact]
    public async Task TaskFile_ExcludedThroughTheTeamsRoot()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        WriteFile(Path.Combine(fixture.DataDir, "Teams", "Alpha", "_tasks", "PLAT-0001.md"), "The zebra is in the task.");

        SearchResult result = await Provider(fixture, OptionsFor(fixture)).SearchAsync(Query("zebra"), TestContext.Current.CancellationToken);

        Assert.Empty(result.Hits);
    }

    /// <summary>With a pinned root that contains the data directory, a Task file is still excluded by its full path.</summary>
    [Fact]
    public async Task TaskFile_ExcludedThroughAPinnedRootContainingDataDir()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build(o => { o.Library.Roots = [new PinnedRootOption { Name = "Data", Path = o.DataDir }]; });
        WriteFile(Path.Combine(fixture.DataDir, "Teams", "Alpha", "_tasks", "PLAT-0001.md"), "The zebra is in the task.");
        fixture.RootStore.SetHidden("teams", true);
        fixture.RootStore.SetHidden("teammates", true);

        SearchResult result = await Provider(fixture, OptionsFor(fixture)).SearchAsync(Query("zebra"), TestContext.Current.CancellationToken);

        Assert.Empty(result.Hits);
    }

    /// <summary>A Teammate definition's text matches the query, but it is listed under Teammates and never as a Library file.</summary>
    [Fact]
    public async Task PersonaFile_ExcludedThroughTheTeammatesRoot()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        _ = fixture.CreateTeammate("Ben", "Ben", "ben");

        SearchResult result = await Provider(fixture, OptionsFor(fixture)).SearchAsync(Query("Chief of Staff"), TestContext.Current.CancellationToken);

        Assert.Empty(result.Hits);
    }

    /// <summary>With a pinned root that contains the data directory and the built-in roots hidden, a Teammate definition is still excluded by its full path.</summary>
    [Fact]
    public async Task PersonaFile_ExcludedThroughAPinnedRootContainingDataDir()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build(o => { o.Library.Roots = [new PinnedRootOption { Name = "Data", Path = o.DataDir }]; });
        _ = fixture.CreateTeammate("Ben", "Ben", "ben");
        fixture.RootStore.SetHidden("teams", true);
        fixture.RootStore.SetHidden("teammates", true);

        SearchResult result = await Provider(fixture, OptionsFor(fixture)).SearchAsync(Query("Chief of Staff"), TestContext.Current.CancellationToken);

        Assert.Empty(result.Hits);
    }

    /// <summary>A file under a Team folder carries that Team's name, and its folder path is the folder it sits in.</summary>
    [Fact]
    public async Task HitUnderTeamsRoot_CarriesItsTeam()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        WriteFile(Path.Combine(fixture.DataDir, "Teams", "Alpha", "notes.md"), "The zebra crossed.");

        SearchResult result = await Provider(fixture, OptionsFor(fixture)).SearchAsync(Query("zebra"), TestContext.Current.CancellationToken);

        LibraryHit hit = Assert.IsType<LibraryHit>(Assert.Single(result.Hits));
        Assert.Equal("teams", hit.RootId);
        Assert.Equal("Teams", hit.RootName);
        Assert.Equal("Alpha/notes.md", hit.RelativePath);
        Assert.Equal("Alpha", hit.FolderPath);
        Assert.False(hit.IsFolder);
        Assert.Equal("Alpha", hit.TeamName);
        Assert.Equal(MatchTier.ContentMatch, hit.Tier);
        Assert.NotNull(hit.Snippet);
    }

    /// <summary>A file in a pinned root has no Team, and sits at the root's top level with an empty folder path.</summary>
    [Fact]
    public async Task HitInPinnedRoot_HasNoTeam()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string pinned = fixture.CreatePinnedRoot("Notes");
        WriteFile(Path.Combine(pinned, "memo.md"), "The zebra crossed.");

        SearchResult result = await Provider(fixture, OptionsFor(fixture)).SearchAsync(Query("zebra"), TestContext.Current.CancellationToken);

        LibraryHit hit = Assert.IsType<LibraryHit>(Assert.Single(result.Hits));
        Assert.Equal("notes", hit.RootId);
        Assert.Equal("Notes", hit.RootName);
        Assert.Equal("memo.md", hit.RelativePath);
        Assert.Equal(string.Empty, hit.FolderPath);
        Assert.False(hit.IsFolder);
        Assert.Null(hit.TeamName);
    }

    /// <summary>A built-in root hidden in the tree is not searched, so a file under it is not found.</summary>
    [Fact]
    public async Task HiddenBuiltInRoot_IsNotSearched()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        WriteFile(Path.Combine(fixture.DataDir, "Teams", "Alpha", "notes.md"), "The zebra crossed.");
        fixture.RootStore.SetHidden("teams", true);

        SearchResult result = await Provider(fixture, OptionsFor(fixture)).SearchAsync(Query("zebra"), TestContext.Current.CancellationToken);

        Assert.Empty(result.Hits);
    }

    /// <summary>The Library has no archived side: the archived scope returns an empty result without walking any root.</summary>
    [Fact]
    public async Task Archived_ReturnsEmptyAtOnce()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        WriteFile(Path.Combine(fixture.DataDir, "Teams", "Alpha", "notes.md"), "The zebra crossed.");
        int calls = 0;
        Func<LibraryRoot, CancellationToken, Task> hook = (_, _) =>
        {
            calls++;
            return Task.CompletedTask;
        };

        SearchResult result = await Provider(fixture, OptionsFor(fixture), hook).SearchAsync(Query("zebra", SearchScope.Archived), TestContext.Current.CancellationToken);

        Assert.Empty(result.Hits);
        Assert.False(result.Truncated);
        Assert.Equal(0, calls);
    }

    /// <summary>With the Library switched off, the provider returns an empty result even when a file matches.</summary>
    [Fact]
    public async Task LibraryDisabled_ReturnsEmpty()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        WriteFile(Path.Combine(fixture.DataDir, "Teams", "Alpha", "notes.md"), "The zebra crossed.");
        TeamOptions options = OptionsFor(fixture);
        options.Library.Enabled = false;

        SearchResult result = await Provider(fixture, options).SearchAsync(Query("zebra"), TestContext.Current.CancellationToken);

        Assert.Empty(result.Hits);
    }

    /// <summary>Visible roots are walked at most <c>LibraryParallelism</c> at a time, and every root finishes.</summary>
    [Fact]
    public async Task Roots_SearchedInParallel_UpToTheSetting()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        _ = fixture.CreatePinnedRoot("Alpha");
        _ = fixture.CreatePinnedRoot("Beta");
        _ = fixture.CreatePinnedRoot("Gamma");
        TeamOptions options = OptionsFor(fixture);
        options.Search.LibraryParallelism = 2;

        Lock gate = new();
        int active = 0;
        int maxActive = 0;
        int finished = 0;
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task Hook(LibraryRoot root, CancellationToken token)
        {
            if (root.Kind != LibraryRootKind.Pinned)
            {
                return;
            }

            lock (gate)
            {
                active++;
                maxActive = Math.Max(maxActive, active);
            }

            await release.Task.WaitAsync(token);
            lock (gate)
            {
                active--;
                finished++;
            }
        }

        int ActiveNow()
        {
            lock (gate)
            {
                return active;
            }
        }

        using CancellationTokenSource bound = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bound.CancelAfter(TimeSpan.FromSeconds(10));
        Task<SearchResult> search = Provider(fixture, options, Hook).SearchAsync(Query("zebra"), ct);
        while (ActiveNow() < 2)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(5), bound.Token);
        }

        // A short observation window: a scheduler that admitted a third root now would show it here.
        await Task.Delay(TimeSpan.FromMilliseconds(100), bound.Token);
        release.SetResult();
        SearchResult result = await search;

        Assert.Equal(2, maxActive);
        Assert.Equal(3, finished);
        Assert.Empty(result.Hits);
    }

    /// <summary>Builds the Library query for <paramref name="text"/> in <paramref name="scope"/>, with the Library filter and the 50-hit cap (OP-5).</summary>
    private static SearchQuery Query(string text, SearchScope scope = SearchScope.Active) =>
        new(text, SearchFilter.Library, null, scope, 50);

    /// <summary>The options the provider reads (the Search options, the Teams folder name and the Library switch), over the fixture's data directory.</summary>
    private static TeamOptions OptionsFor(LibraryFileServiceFixture fixture) => new() { DataDir = fixture.DataDir };

    /// <summary>Builds the provider over the fixture's live root store, resolver and a fresh Library file service.</summary>
    /// <param name="fixture">The fixture whose roots are searched.</param>
    /// <param name="options">The options the provider reads.</param>
    /// <param name="hook">The per-root test seam, or <see langword="null"/>.</param>
    private static LibrarySearchProvider Provider(LibraryFileServiceFixture fixture, TeamOptions options, Func<LibraryRoot, CancellationToken, Task>? hook = null) =>
        new(fixture.RootStore, fixture.Resolver, fixture.CreateService(), Options.Create(options), hook);

    /// <summary>Writes <paramref name="text"/> to <paramref name="path"/>, creating its folder first.</summary>
    /// <param name="path">The file to write.</param>
    /// <param name="text">The file's text.</param>
    private static void WriteFile(string path, string text)
    {
        string? folder = Path.GetDirectoryName(path);
        if (folder is not null)
        {
            Directory.CreateDirectory(folder);
        }

        File.WriteAllText(path, text);
    }
}
