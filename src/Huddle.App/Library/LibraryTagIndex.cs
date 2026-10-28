using Agency.Huddle.App.FileChanges;

namespace Agency.Huddle.App.Library;

/// <summary>
/// The tag aggregates over <see cref="WikiLinkIndex"/> (Spec §6.17): display casing and subtree counts per Library Root. Tags are collected in the
/// link index's own build, so a note is never read twice; this class only aggregates them on first use and drops its aggregate when
/// <see cref="WikiLinkIndex.Invalidated"/> fires for the root.
/// </summary>
internal sealed class LibraryTagIndex : IDisposable
{
    private readonly WikiLinkIndex links;
    private readonly Lock gate = new();
    private readonly Dictionary<string, RootTags> cache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> generations = new(StringComparer.Ordinal);
    private bool disposed;

    /// <summary>Creates the index over <paramref name="links"/>.</summary>
    /// <param name="links">The link index whose build collects each note's tags.</param>
    public LibraryTagIndex(WikiLinkIndex links)
    {
        ArgumentNullException.ThrowIfNull(links);
        this.links = links;
        this.links.Invalidated += this.OnInvalidated;
    }

    /// <summary>Unsubscribes from <see cref="WikiLinkIndex.Invalidated"/>.</summary>
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.links.Invalidated -= this.OnInvalidated;
        this.disposed = true;
    }

    /// <summary>Whether the root's tags were collected: <see langword="false"/> above <c>MaxIndexedFiles</c>, as for the link index.</summary>
    /// <param name="rootId">The Library Root id.</param>
    internal bool IsAvailable(string rootId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootId);
        return this.links.IsAvailable(rootId);
    }

    /// <summary>The note's own tags in the root's display casing: de-duplicated, frontmatter first, in source order, with no implied parents.</summary>
    /// <param name="note">The note.</param>
    internal IReadOnlyList<string> TagsOf(LibraryPath note)
    {
        ArgumentNullException.ThrowIfNull(note);

        RootTags tags = this.GetOrBuild(note.Root.Id);
        return tags.PerNote.TryGetValue(note.RelativePath, out IReadOnlyList<string>? own) ? own : [];
    }

    /// <summary>Every tag and every implied parent in the root, sorted by the case-insensitive full path. Empty when the root is unavailable.</summary>
    /// <param name="rootId">The Library Root id.</param>
    internal IReadOnlyList<LibraryTag> AllTags(string rootId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootId);
        return this.GetOrBuild(rootId).All;
    }

    /// <summary>The root-relative paths of the notes carrying <paramref name="tag"/> or a child, in ordinal path order.</summary>
    /// <param name="rootId">The Library Root id.</param>
    /// <param name="tag">The tag, in any casing, with an optional leading <c>#</c> and trailing <c>/</c>.</param>
    internal IReadOnlyList<string> NotesWith(string rootId, string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootId);
        ArgumentNullException.ThrowIfNull(tag);

        string key = tag.TrimStart('#').TrimEnd('/');
        return this.GetOrBuild(rootId).Notes.TryGetValue(key, out IReadOnlyList<string>? notes) ? notes : [];
    }

    private void OnInvalidated(string rootId)
    {
        lock (this.gate)
        {
            this.generations[rootId] = this.generations.TryGetValue(rootId, out int current) ? current + 1 : 1;
            _ = this.cache.Remove(rootId);
        }
    }

    private RootTags GetOrBuild(string rootId)
    {
        int startGeneration;
        lock (this.gate)
        {
            if (this.cache.TryGetValue(rootId, out RootTags? cached))
            {
                return cached;
            }

            startGeneration = this.generations.TryGetValue(rootId, out int g) ? g : 0;
        }

        RootTags built = LibraryTagIndex.Build(this.links.TagsInRoot(rootId));
        lock (this.gate)
        {
            int currentGeneration = this.generations.TryGetValue(rootId, out int g) ? g : 0;
            if (currentGeneration == startGeneration)
            {
                this.cache[rootId] = built;
            }
        }

        return built;
    }

    /// <summary>Walks the notes in ordinal path order; the first spelling met for a tag or a parent segment is its display casing.</summary>
    private static RootTags Build(IReadOnlyList<(string NotePath, IReadOnlyList<string> Tags)>? notes)
    {
        if (notes is null)
        {
            return RootTags.Empty;
        }

        Dictionary<string, string> display = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, List<string>> subtree = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string notePath, IReadOnlyList<string> tags) in notes)
        {
            foreach (string tag in tags)
            {
                LibraryTagIndex.AddWithParents(tag, notePath, display, subtree);
            }
        }

        Dictionary<string, IReadOnlyList<string>> perNote = new(FolderSnapshot.PathComparer);
        foreach ((string notePath, IReadOnlyList<string> tags) in notes)
        {
            List<string> own = [];
            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
            foreach (string tag in tags)
            {
                if (seen.Add(tag))
                {
                    own.Add(display[tag]);
                }
            }

            perNote[notePath] = own;
        }

        List<string> keys = [.. display.Keys];
        keys.Sort(StringComparer.OrdinalIgnoreCase);
        List<LibraryTag> all = [.. keys.Select(key => new LibraryTag(display[key], subtree[key].Count))];
        Dictionary<string, IReadOnlyList<string>> notesByTag = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string key, List<string> paths) in subtree)
        {
            notesByTag[key] = paths;
        }

        return new RootTags(all, notesByTag, perNote);
    }

    /// <summary>Records <paramref name="tag"/> and each parent prefix against <paramref name="notePath"/>; paths arrive in order, so a repeat is the list's last entry.</summary>
    private static void AddWithParents(string tag, string notePath, Dictionary<string, string> display, Dictionary<string, List<string>> subtree)
    {
        int end = 0;
        int segmentStart = 0;
        string? parentDisplay = null;
        while (end < tag.Length)
        {
            int slash = tag.IndexOf('/', end);
            end = slash < 0 ? tag.Length : slash;
            string prefix = tag[..end];

            // A parent segment takes the first spelling met in any tag that contains it, so a child is shown under its parent's spelling.
            string segment = tag[segmentStart..end];
            if (!display.TryGetValue(prefix, out string? shown))
            {
                shown = parentDisplay is null ? segment : string.Concat(parentDisplay, "/", segment);
                display[prefix] = shown;
            }

            parentDisplay = shown;
            segmentStart = end + 1;
            if (!subtree.TryGetValue(prefix, out List<string>? paths))
            {
                paths = [];
                subtree[prefix] = paths;
            }

            if (paths.Count == 0 || !string.Equals(paths[^1], notePath, StringComparison.Ordinal))
            {
                paths.Add(notePath);
            }

            end++;
        }
    }

    private sealed record RootTags(
        IReadOnlyList<LibraryTag> All,
        IReadOnlyDictionary<string, IReadOnlyList<string>> Notes,
        IReadOnlyDictionary<string, IReadOnlyList<string>> PerNote)
    {
        internal static RootTags Empty { get; } = new(
            [],
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal));
    }
}
