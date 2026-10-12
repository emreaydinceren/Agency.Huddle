using Microsoft.Extensions.Options;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.App.Search;

/// <summary>
/// The Library group's provider (Global Search spec §8.3 and §8.6): walks every visible Library Root
/// concurrently through <see cref="LibraryFileService.SearchTextAsync"/>, and leaves out Persona and
/// Task files, which the Teammates and Tasks groups list instead.
/// </summary>
/// <remarks>
/// The Library has no archived side, and a disabled Library returns nothing at once. Hits are not
/// ranked or capped across roots here; the orchestrator does that.
/// </remarks>
internal sealed class LibrarySearchProvider(
    LibraryRootStore roots,
    LibraryPathResolver resolver,
    LibraryFileService files,
    IOptions<TeamOptions> options,
    Func<LibraryRoot, CancellationToken, Task>? beforeRootForTests = null) : ISearchProvider
{
    private readonly LibraryRootStore roots = roots;
    private readonly LibraryPathResolver resolver = resolver;
    private readonly LibraryFileService files = files;
    private readonly IOptions<TeamOptions> options = options;
    private readonly Func<LibraryRoot, CancellationToken, Task>? beforeRootForTests = beforeRootForTests;

    /// <summary>The group this provider fills.</summary>
    public SearchFilter Group => SearchFilter.Library;

    /// <summary>Always <see langword="true"/>: every search walks the Library from disk.</summary>
    public bool IsExpensive => true;

    /// <summary>Searches every visible Library Root, at most <c>Team:Search:LibraryParallelism</c> at a time.</summary>
    /// <param name="query">The search as the orchestrator received it.</param>
    /// <param name="ct">Cancels the search when a newer keystroke or a filter change supersedes it.</param>
    /// <returns>The Library hits from every visible root, unranked.</returns>
    public async Task<SearchResult> SearchAsync(SearchQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.Scope == SearchScope.Archived || !this.options.Value.Library.Enabled || query.Text.Trim().Length == 0)
        {
            return SearchResult.Empty;
        }

        string teamsRoot = Path.GetFullPath(Path.Combine(this.options.Value.DataDir, this.options.Value.Teams.Dir));
        Predicate<LibraryEntry> isExcluded = entry => IsExcluded(entry, teamsRoot);
        List<LibraryHit> hits = [];
        Lock gate = new();
        bool truncated = false;
        ParallelOptions parallel = new()
        {
            MaxDegreeOfParallelism = this.options.Value.Search.EffectiveLibraryParallelism,
            CancellationToken = ct,
        };

        await Parallel.ForEachAsync(this.roots.VisibleRoots, parallel, async (root, token) =>
        {
            if (this.beforeRootForTests is { } hook)
            {
                await hook(root, token);
            }

            if (!this.resolver.TryResolve(root.Id, string.Empty, out LibraryPath? scope, out _))
            {
                return;
            }

            LibraryTextSearchResult found = await this.files.SearchTextAsync(scope, query.Text, query.MaxHits, this.options.Value.Search, isExcluded, token);
            lock (gate)
            {
                truncated |= found.Truncated;
                hits.AddRange(found.Hits.Select(hit => ToHit(root, hit)));
            }
        });

        return new SearchResult([.. hits], truncated);
    }

    /// <summary>
    /// Whether an entry is never a Library hit (OP-10, §8.6): a Teammate's definition file, or a Task file
    /// under the Teams root. Tested on the full path, so a pinned root that contains the data directory is covered too.
    /// </summary>
    /// <param name="entry">The entry the walk visited.</param>
    /// <param name="teamsRoot">The full path of the Teams root.</param>
    /// <returns><see langword="true"/> when the entry must not be a hit.</returns>
    private static bool IsExcluded(LibraryEntry entry, string teamsRoot) =>
        entry.Path.Role == LibraryNodeRole.TeammateDefinition
        || (!entry.IsFolder && TaskLayout.TryMap(teamsRoot, entry.Path.FullPath, out _, out _));

    /// <summary>Maps one walked entry to a Library hit, naming its Team when it sits under the Teams root (OP-4).</summary>
    /// <param name="root">The root the entry was found under.</param>
    /// <param name="hit">The walked entry, with its tier and snippet.</param>
    /// <returns>The Library hit.</returns>
    private static LibraryHit ToHit(LibraryRoot root, LibraryTextHit hit)
    {
        LibraryEntry entry = hit.Entry;
        string relative = entry.Path.RelativePath;
        int lastSlash = relative.LastIndexOf('/');
        string folder = lastSlash < 0 ? string.Empty : relative[..lastSlash];

        string? team = null;
        if (root.Kind == LibraryRootKind.Teams && (lastSlash >= 0 || entry.IsFolder))
        {
            int firstSlash = relative.IndexOf('/');
            team = firstSlash < 0 ? relative : relative[..firstSlash];
        }

        return new LibraryHit(root.Id, root.DisplayName, relative, folder, entry.IsFolder, hit.Snippet, team, hit.Tier);
    }
}
