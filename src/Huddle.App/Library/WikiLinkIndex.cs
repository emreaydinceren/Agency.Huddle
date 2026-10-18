using Microsoft.Extensions.Options;
using Agency.Huddle.App.FileChanges;

namespace Agency.Huddle.App.Library;

/// <summary>A backlink into <see cref="LibraryBacklink.NotePath"/>, one row per line that holds a
/// resolved link to the queried note (Spec §6.5 <i>Backlinks</i>).</summary>
/// <param name="NotePath">The linking note's root-relative path.</param>
/// <param name="Line">The 1-based line number the link (or links) appear on.</param>
/// <param name="LineText">The full text of that line.</param>
internal sealed record LibraryBacklink(string NotePath, int Line, string LineText);

/// <summary>An outgoing wikilink of a note, for the Linked documents panel.</summary>
/// <param name="Link">The parsed link as written in the note.</param>
/// <param name="ResolvedPath">The linked note's root-relative path, or <see langword="null"/> when it does not exist.</param>
internal sealed record LibraryOutgoingLink(WikiLink Link, string? ResolvedPath);

/// <summary>
/// The per-Library-Root wikilink index (Spec §6.5, §7 <c>MaxIndexedFiles</c>): built lazily on
/// first use, cached per root, and invalidated by <see cref="Invalidate"/> or by
/// <see cref="LibraryRootStore.RootsChanged"/>. Walks each root once with
/// <see cref="LibraryPathResolver.TryResolveChild"/> (so a junction loop or a reserved folder is
/// skipped exactly as <see cref="LibraryFileService.ListAsync"/> skips it), hiding the same folders
/// (corrections-B5 item 5). Holds no per-request state; every read is served from the cache under
/// <see cref="gate"/>, built outside the lock so disk I/O never blocks a concurrent query.
/// </summary>
internal sealed class WikiLinkIndex : IDisposable
{
    private readonly LibraryRootStore roots;
    private readonly LibraryPathResolver resolver;
    private readonly IOptions<TeamOptions> options;
    private readonly Action? afterWalkForTests;
    private readonly Lock gate = new();
    private readonly Dictionary<string, RootIndex> cache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> generations = new(StringComparer.Ordinal);
    private bool disposed;

    /// <summary>Raised with the Library Root id after that root's cached index was dropped, by
    /// <see cref="Invalidate"/> and once per root by a roots change. Always raised outside
    /// <see cref="gate"/>, so a handler may query the index.</summary>
    internal event Action<string>? Invalidated;

    /// <summary>Creates the index over <paramref name="roots"/> and <paramref name="resolver"/>, subscribing to
    /// <see cref="LibraryRootStore.RootsChanged"/> so a re-pin or a hide/show drops every cached root.</summary>
    /// <param name="roots">Every configured Library Root.</param>
    /// <param name="resolver">Resolves and walks paths inside each root.</param>
    /// <param name="options">The bound <see cref="TeamOptions"/>, for <see cref="LibraryOptions.MaxIndexedFiles"/> and the ignore list.</param>
    public WikiLinkIndex(LibraryRootStore roots, LibraryPathResolver resolver, IOptions<TeamOptions> options)
        : this(roots, resolver, options, afterWalkForTests: null)
    {
    }

    /// <summary>Test-only seam (fix card 8.4, item 1): <paramref name="afterWalkForTests"/> runs after a build
    /// finishes walking disk but before the generation check that decides whether to cache it, so a test can
    /// call <see cref="Invalidate"/> deterministically inside that window to prove the race fix without a
    /// sleep.</summary>
    internal WikiLinkIndex(LibraryRootStore roots, LibraryPathResolver resolver, IOptions<TeamOptions> options, Action? afterWalkForTests)
    {
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(options);

        this.roots = roots;
        this.resolver = resolver;
        this.options = options;
        this.afterWalkForTests = afterWalkForTests;
        this.roots.RootsChanged += this.OnRootsChanged;
    }

    /// <summary>Unsubscribes from <see cref="LibraryRootStore.RootsChanged"/>.</summary>
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.roots.RootsChanged -= this.OnRootsChanged;
        this.disposed = true;
    }

    /// <summary>Whether <paramref name="rootId"/>'s index was built: <see langword="false"/> when the root
    /// has more than <see cref="LibraryOptions.MaxIndexedFiles"/> files.</summary>
    /// <param name="rootId">The Library Root id.</param>
    internal bool IsAvailable(string rootId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootId);
        return this.GetOrBuild(rootId) is { Available: true };
    }

    /// <summary>Every note in <paramref name="note"/>'s root whose resolved links point at it (Spec §6.5
    /// <i>Backlinks</i>), one row per line, in <see cref="LibraryBacklink.NotePath"/> then
    /// <see cref="LibraryBacklink.Line"/> order. Empty when the root's index isn't available.</summary>
    /// <param name="note">The open note, re-resolved against the caller's own spelling of its path.</param>
    internal IReadOnlyList<LibraryBacklink> Backlinks(LibraryPath note)
    {
        ArgumentNullException.ThrowIfNull(note);

        RootIndex? index = this.GetOrBuild(note.Root.Id);
        if (index is not { Available: true })
        {
            return [];
        }

        List<LibraryBacklink> results = [];
        foreach (string notePath in index.SortedNotes)
        {
            IReadOnlyList<(WikiLink Link, string LineText, string? ResolvedPath)> links = index.NoteLinks[notePath];
            HashSet<int> addedLines = [];
            foreach ((WikiLink link, string lineText, string? resolvedPath) in links)
            {
                if (resolvedPath is null || !FolderSnapshot.PathComparer.Equals(resolvedPath, note.RelativePath))
                {
                    continue;
                }

                if (addedLines.Add(link.Line))
                {
                    results.Add(new LibraryBacklink(notePath, link.Line, lineText));
                }
            }
        }

        return results;
    }

    /// <summary>The wikilinks written in <paramref name="note"/>, resolved, in document order (Spec <i>Linked
    /// documents</i>): embeds are left out, a link to a missing note is kept with a <see langword="null"/>
    /// <see cref="LibraryOutgoingLink.ResolvedPath"/>, and a target linked more than once is listed once.
    /// Empty when the root's index isn't available or the note isn't in it.</summary>
    /// <param name="note">The open note, re-resolved against the caller's own spelling of its path.</param>
    internal IReadOnlyList<LibraryOutgoingLink> LinksFrom(LibraryPath note)
    {
        ArgumentNullException.ThrowIfNull(note);

        RootIndex? index = this.GetOrBuild(note.Root.Id);
        if (index is not { Available: true })
        {
            return [];
        }

        foreach (string notePath in index.SortedNotes)
        {
            if (!FolderSnapshot.PathComparer.Equals(notePath, note.RelativePath))
            {
                continue;
            }

            List<LibraryOutgoingLink> results = [];
            HashSet<string> seen = new(FolderSnapshot.PathComparer);
            foreach ((WikiLink link, _, string? resolvedPath) in index.NoteLinks[notePath])
            {
                if (!link.IsEmbed && seen.Add(resolvedPath ?? link.Target))
                {
                    results.Add(new LibraryOutgoingLink(link, resolvedPath));
                }
            }

            return results;
        }

        return [];
    }

    /// <summary>Every link across <paramref name="target"/>'s root that resolves to it (used by rename), in
    /// <c>NotePath</c> then document order. Empty when the root's index isn't available.</summary>
    /// <param name="target">The link target, re-resolved against the caller's own spelling of its path.</param>
    internal IReadOnlyList<(string NotePath, WikiLink Link)> LinksTo(LibraryPath target)
    {
        ArgumentNullException.ThrowIfNull(target);

        RootIndex? index = this.GetOrBuild(target.Root.Id);
        if (index is not { Available: true })
        {
            return [];
        }

        List<(string NotePath, WikiLink Link)> results = [];
        foreach (string notePath in index.SortedNotes)
        {
            foreach ((WikiLink link, _, string? resolvedPath) in index.NoteLinks[notePath])
            {
                if (resolvedPath is not null && FolderSnapshot.PathComparer.Equals(resolvedPath, target.RelativePath))
                {
                    results.Add((notePath, link));
                }
            }
        }

        return results;
    }

    /// <summary>Every file's root-relative path strictly under <paramref name="relativeFolderPath"/>, at any
    /// depth (corrections-B5 D8 item 8, folder link preview): reuses this index's already-built file list
    /// rather than a second disk walk. Empty when the root's index isn't available.</summary>
    /// <param name="rootId">The Library Root id.</param>
    /// <param name="relativeFolderPath">The folder's root-relative path, no trailing separator.</param>
    internal IReadOnlyList<string> FilesUnder(string rootId, string relativeFolderPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootId);
        ArgumentNullException.ThrowIfNull(relativeFolderPath);

        RootIndex? index = this.GetOrBuild(rootId);
        if (index is not { Available: true })
        {
            return [];
        }

        string prefix = relativeFolderPath.Length == 0 ? string.Empty : relativeFolderPath + "/";
        List<string> results = [];
        foreach (string file in index.AllFiles)
        {
            if (file.Length > prefix.Length && FolderSnapshot.PathComparer.Equals(file[..prefix.Length], prefix))
            {
                results.Add(file);
            }
        }

        return results;
    }

    /// <summary>Resolves <paramref name="link"/> as written inside <paramref name="from"/>, against every file
    /// currently indexed in <paramref name="from"/>'s root. Unlike <see cref="Backlinks"/>/<see cref="LinksTo"/>,
    /// which filter the resolution each link was built with, this always re-resolves: <paramref name="link"/>
    /// need not be one the index has seen (e.g. freshly typed in an open editor). An unresolved result when the
    /// root's index isn't available.</summary>
    /// <param name="from">The note the link appears in.</param>
    /// <param name="link">The parsed link.</param>
    internal WikiLinkResolution Resolve(LibraryPath from, WikiLink link)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(link);

        RootIndex? index = this.GetOrBuild(from.Root.Id);
        if (index is not { Available: true })
        {
            return new WikiLinkResolution(null, false);
        }

        return WikiLinkResolver.Resolve(index.AllFiles, from.RelativePath, link.Target);
    }

    /// <summary>The notes in <paramref name="from"/>'s root whose name matches <paramref name="query"/>, each written
    /// as the shortest link target that resolves back to it from <paramref name="from"/> (the same
    /// <see cref="WikiLinkResolver.ShortestTarget"/> a rename uses), for the editor's <c>[[</c> completion. Nearest first, as
    /// Spec §6.5 resolution is: notes in the same folder, then by the fewest folder steps from the open note, then names that
    /// start with the query before names that merely contain it, then ordinal path order. The note itself is left out.
    /// Empty when the root's index isn't available.</summary>
    /// <param name="from">The note being edited; it fixes the root and the folder the shortest form is measured from.</param>
    /// <param name="query">What the human has typed after <c>[[</c>; empty matches every note.</param>
    /// <param name="max">The most suggestions to return.</param>
    internal IReadOnlyList<string> SuggestTargets(LibraryPath from, string query, int max)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(query);

        RootIndex? index = this.GetOrBuild(from.Root.Id);
        if (index is not { Available: true } || max <= 0)
        {
            return [];
        }

        List<(string Path, string Name, bool StartsWith, int Distance)> matches = [];
        foreach (string file in index.AllFiles)
        {
            if (!LibraryFileKinds.IsMarkdown(file) || FolderSnapshot.PathComparer.Equals(file, from.RelativePath))
            {
                continue;
            }

            // A query with a '/' is a path suffix the human is spelling out, so it is matched against the whole path.
            string name = query.Contains('/', StringComparison.Ordinal)
                ? file[..^System.IO.Path.GetExtension(file).Length]
                : System.IO.Path.GetFileNameWithoutExtension(file);
            bool startsWith = name.StartsWith(query, StringComparison.OrdinalIgnoreCase);
            if (startsWith || name.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add((file, name, startsWith, WikiLinkResolver.FolderDistance(from.RelativePath, file)));
            }
        }

        matches.Sort(static (a, b) =>
        {
            int byDistance = a.Distance.CompareTo(b.Distance);
            if (byDistance != 0)
            {
                return byDistance;
            }

            int byGroup = b.StartsWith.CompareTo(a.StartsWith);
            return byGroup != 0 ? byGroup : string.CompareOrdinal(a.Path, b.Path);
        });

        List<string> suggestions = [];
        foreach ((string path, _, _, _) in matches.Take(max))
        {
            suggestions.Add(WikiLinkResolver.ShortestTarget(index.AllFiles, from.RelativePath, path));
        }

        return suggestions;
    }

    /// <summary><see cref="SuggestTargets"/> off the caller's thread: the first call for a root walks its disk to build
    /// the index, which must not stall the Blazor circuit that is waiting on the human's next keystroke.</summary>
    /// <param name="from">The note being edited.</param>
    /// <param name="query">What the human has typed after <c>[[</c>.</param>
    /// <param name="max">The most suggestions to return.</param>
    /// <param name="ct">Cancels the lookup before it starts; a build already under way finishes and is cached.</param>
    internal Task<IReadOnlyList<string>> SuggestTargetsAsync(LibraryPath from, string query, int max, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(query);

        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            return this.SuggestTargets(from, query, max);
        }, ct);
    }

    /// <summary>Drops the cached index for <paramref name="rootId"/> and bumps its generation, so a build
    /// already in flight for it (fix card 8.4 item 1) is discarded rather than cached once it finishes.</summary>
    /// <param name="rootId">The Library Root id.</param>
    internal void Invalidate(string rootId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootId);

        lock (this.gate)
        {
            this.BumpGeneration(rootId);
            _ = this.cache.Remove(rootId);
        }

        this.Invalidated?.Invoke(rootId);
    }

    /// <summary>Drops every cached root and bumps every known root's generation: a re-pin, a hide/show or a
    /// reset can move or remove any of them, including one whose build is still in flight.</summary>
    private void OnRootsChanged()
    {
        List<string> rootIds = [];
        lock (this.gate)
        {
            foreach (LibraryRoot root in this.roots.Roots)
            {
                this.BumpGeneration(root.Id);
                rootIds.Add(root.Id);
            }

            this.cache.Clear();
        }

        foreach (string rootId in rootIds)
        {
            this.Invalidated?.Invoke(rootId);
        }
    }

    /// <summary>Increments <paramref name="rootId"/>'s generation counter, starting it at 1 the first time.
    /// Callers hold <see cref="gate"/>.</summary>
    private void BumpGeneration(string rootId) =>
        this.generations[rootId] = this.generations.TryGetValue(rootId, out int current) ? current + 1 : 1;

    /// <summary>
    /// Returns the cached index for <paramref name="rootId"/>, building it outside <see cref="gate"/> on a
    /// cache miss (a build never runs while the lock is held). The build is stored only when
    /// <paramref name="rootId"/>'s generation is unchanged from the moment the build started: an
    /// <see cref="Invalidate"/> or a <see cref="LibraryRootStore.RootsChanged"/> that lands while this build was
    /// running bumps the generation, so the now-stale result is returned to THIS caller (it was correct for the
    /// state the walk actually saw) but never cached — the next call rebuilds instead of reusing it forever
    /// (fix card 8.4 item 1).
    /// </summary>
    private RootIndex? GetOrBuild(string rootId)
    {
        int startGeneration;
        lock (this.gate)
        {
            if (this.cache.TryGetValue(rootId, out RootIndex? cached))
            {
                return cached;
            }

            startGeneration = this.generations.TryGetValue(rootId, out int g) ? g : 0;
        }

        LibraryRoot? root = null;
        foreach (LibraryRoot candidate in this.roots.Roots)
        {
            if (string.Equals(candidate.Id, rootId, StringComparison.Ordinal))
            {
                root = candidate;
                break;
            }
        }

        if (root is null)
        {
            return null;
        }

        RootIndex built = this.BuildRootIndex(root);
        this.afterWalkForTests?.Invoke();

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

    /// <summary>Walks <paramref name="root"/> once, collecting every file's root-relative path (as a
    /// resolution target) and, for Markdown files, its parsed links with their line text. Stops and marks the
    /// root unavailable the moment a file over <see cref="LibraryOptions.MaxIndexedFiles"/> is found. Once
    /// every file is known, resolves each link exactly once (fix card 8.4 item 3) rather than per query.</summary>
    private RootIndex BuildRootIndex(LibraryRoot root)
    {
        int max = this.options.Value.Library.MaxIndexedFiles;
        IReadOnlyList<string> ignoredFolders = this.options.Value.FileChanges.EffectiveIgnore;
        bool hideUnderscoreFolders = root.Kind == LibraryRootKind.Teams;

        List<string> allFiles = [];
        Dictionary<string, List<(WikiLink Link, string LineText)>> rawNoteLinks = new(StringComparer.Ordinal);

        if (!this.resolver.TryResolve(root.Id, string.Empty, out LibraryPath? rootPath, out _))
        {
            return new RootIndex(false, allFiles, ToResolved(rawNoteLinks, allFiles), []);
        }

        HashSet<string> visitedFolders = new(FolderSnapshot.PathComparer) { rootPath.FullPath };
        Stack<LibraryPath> pending = new();
        pending.Push(rootPath);

        while (pending.Count > 0)
        {
            LibraryPath folder = pending.Pop();
            List<FileSystemInfo> children;
            try
            {
                children = [.. new DirectoryInfo(folder.FullPath).EnumerateFileSystemInfos()];
            }
            catch (DirectoryNotFoundException)
            {
                continue;
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (FileSystemInfo child in children)
            {
                bool isFolder = child is DirectoryInfo;
                if (isFolder && LibraryHiddenFolders.IsHidden(child.Name, ignoredFolders, hideUnderscoreFolders))
                {
                    continue;
                }

                if (!this.resolver.TryResolveChild(folder, child, out LibraryPath? childPath, out _))
                {
                    continue;
                }

                if (isFolder)
                {
                    if (visitedFolders.Add(childPath.FullPath))
                    {
                        pending.Push(childPath);
                    }

                    continue;
                }

                allFiles.Add(childPath.RelativePath);
                if (allFiles.Count > max)
                {
                    return new RootIndex(false, allFiles, ToResolved(rawNoteLinks, allFiles), []);
                }

                if (LibraryFileKinds.IsMarkdown(childPath.RelativePath) && TryReadNote(childPath.FullPath, out string? text))
                {
                    rawNoteLinks[childPath.RelativePath] = ParseNoteLinks(text);
                }
            }
        }

        List<string> sortedNotes = [.. rawNoteLinks.Keys];
        sortedNotes.Sort(StringComparer.Ordinal);
        return new RootIndex(true, allFiles, ToResolved(rawNoteLinks, allFiles), sortedNotes);
    }

    /// <summary>Resolves every note's links exactly once, now that <paramref name="allFiles"/> (the complete
    /// candidate list for this build) is fully known.</summary>
    private static Dictionary<string, IReadOnlyList<(WikiLink Link, string LineText, string? ResolvedPath)>> ToResolved(
        Dictionary<string, List<(WikiLink Link, string LineText)>> rawNoteLinks, IReadOnlyList<string> allFiles)
    {
        Dictionary<string, IReadOnlyList<(WikiLink Link, string LineText, string? ResolvedPath)>> resolved = new(StringComparer.Ordinal);
        foreach ((string notePath, List<(WikiLink Link, string LineText)> entries) in rawNoteLinks)
        {
            List<(WikiLink Link, string LineText, string? ResolvedPath)> withResolution = new(entries.Count);
            foreach ((WikiLink link, string lineText) in entries)
            {
                string? resolvedPath = WikiLinkResolver.Resolve(allFiles, notePath, link.Target).Path;
                withResolution.Add((link, lineText, resolvedPath));
            }

            resolved[notePath] = withResolution;
        }

        return resolved;
    }

    /// <summary>Reads and decodes a note's text for parsing. A locked or otherwise unreadable file, or one
    /// that fails to decode, is skipped: it stays a valid resolution target (already added to
    /// <c>allFiles</c>) but contributes no outgoing links.</summary>
    private bool TryReadNote(string fullPath, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? text)
    {
        text = null;
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(fullPath);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }

        if (bytes.LongLength > this.options.Value.Library.MaxEditableBytes)
        {
            return false;
        }

        return TextFileCodec.TryDecode(bytes, LineEnding.CrLf, out text, out _);
    }

    /// <summary>Parses <paramref name="text"/>'s wikilinks once and pairs each with its line's text, computed
    /// from a single pass over the text rather than rescanning it per link.</summary>
    private static List<(WikiLink Link, string LineText)> ParseNoteLinks(string text)
    {
        IReadOnlyList<WikiLink> links = WikiLinkParser.Parse(text);
        if (links.Count == 0)
        {
            return [];
        }

        List<string> lines = SplitLines(text);
        List<(WikiLink, string)> result = new(links.Count);
        foreach (WikiLink link in links)
        {
            string lineText = link.Line >= 1 && link.Line <= lines.Count ? lines[link.Line - 1] : string.Empty;
            result.Add((link, lineText));
        }

        return result;
    }

    /// <summary>Splits <paramref name="text"/> into lines in one pass, using the same line-break rule as
    /// <see cref="WikiLink.Line"/>: <c>\n</c>, or a lone <c>\r</c> not followed by <c>\n</c>.</summary>
    private static List<string> SplitLines(string text)
    {
        List<string> lines = [];
        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            bool isLoneCr = c == '\r' && (i + 1 >= text.Length || text[i + 1] != '\n');
            if (c == '\n' || isLoneCr)
            {
                lines.Add(text[start..i]);
                start = i + 1;
            }
        }

        lines.Add(text[start..]);
        return lines;
    }

    /// <summary>One root's cached state: every file's root-relative path (resolution targets), each Markdown
    /// note's parsed links with line text and its precomputed resolution, and the note keys in a stable sort
    /// order.</summary>
    /// <param name="Available">Whether the root's file count is at or below <see cref="LibraryOptions.MaxIndexedFiles"/>.</param>
    /// <param name="AllFiles">Every file's root-relative path, markdown or not.</param>
    /// <param name="NoteLinks">Each Markdown note's parsed links, keyed by root-relative path, each already
    /// resolved against <see cref="AllFiles"/> at build time.</param>
    /// <param name="SortedNotes"><see cref="NoteLinks"/>'s keys, sorted ordinally.</param>
    private sealed record RootIndex(
        bool Available,
        IReadOnlyList<string> AllFiles,
        IReadOnlyDictionary<string, IReadOnlyList<(WikiLink Link, string LineText, string? ResolvedPath)>> NoteLinks,
        IReadOnlyList<string> SortedNotes);
}
