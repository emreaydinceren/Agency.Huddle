namespace Agency.Huddle.App.Search;

/// <summary>Bound from <c>Team:Search</c>, per spec §10.</summary>
public sealed class SearchOptions
{
    /// <summary>
    /// The text extensions read by search when <see cref="TextExtensions"/> is not set, compared
    /// case-insensitively: the Library's text kinds plus Markdown.
    /// </summary>
    private static readonly IReadOnlyList<string> DefaultTextExtensions =
        [".md", ".markdown", ".txt", ".json", ".yaml", ".yml", ".csv", ".log", ".xml", ".cs", ".js", ".ts", ".css", ".ps1"];

    /// <summary>How many Library Roots are searched at once. Below one is raised to one (see <see cref="EffectiveLibraryParallelism"/>).</summary>
    public int LibraryParallelism { get; set; } = 4;

    /// <summary><see cref="LibraryParallelism"/> raised to at least one.</summary>
    public int EffectiveLibraryParallelism => Math.Max(1, this.LibraryParallelism);

    /// <summary>Above this length, a Library file is matched by name only and never read.</summary>
    public long MaxContentFileBytes { get; set; } = 1048576;

    /// <summary>
    /// How many entries one Library Root's walk visits before it stops and the group reports
    /// <c>Results may be incomplete.</c>. A visit budget, not an index. Assumed, not yet confirmed by the owner (OP-2).
    /// </summary>
    public int MaxVisitedEntries { get; set; } = 5000;

    /// <summary>
    /// The extensions whose contents are read. Nullable, with no initialiser: <c>ConfigurationBinder</c>
    /// appends to an already-populated list, so a configured value is read through
    /// <see cref="EffectiveTextExtensions"/> rather than off this property directly.
    /// </summary>
    public IReadOnlyList<string>? TextExtensions { get; set; }

    /// <summary><see cref="TextExtensions"/> when set and non-empty; otherwise the default list.</summary>
    public IReadOnlyList<string> EffectiveTextExtensions => this.TextExtensions is { Count: > 0 } configured ? configured : DefaultTextExtensions;
}
