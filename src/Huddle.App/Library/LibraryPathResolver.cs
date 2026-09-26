using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.App.Library;

/// <summary>
/// The Library's path boundary (Spec §6.1): resolves a root-relative path to a
/// <see cref="LibraryPath"/> proven to lie inside a <see cref="LibraryRoot"/>, following steps 1-7
/// in order, per corrections-B3 4.2.i. The role a resolved path receives follows the RESOLVED
/// location, not the root id the caller named (item 25): a pinned root that happens to reach into
/// the Teams or Teammates tree still gets that tree's role and reserved-folder rules.
/// </summary>
/// <param name="roots">Every configured Library Root, including the two built-ins.</param>
/// <param name="options">The bound <see cref="TeamOptions"/>, for <see cref="TeamOptions.DataDir"/>.</param>
/// <param name="logger">Logs the boundary reason behind a <see cref="TryResolveScope"/> refusal (item 33).</param>
internal sealed class LibraryPathResolver(LibraryRootStore roots, IOptions<TeamOptions> options, ILogger<LibraryPathResolver> logger)
{
    private const string OutsideLibraryError = "That path isn't inside the Library.";
    private const string ReservedFolderError = "That folder is reserved.";
    private const string ScopeUnavailableError = "This folder isn't available in the Library.";

    private readonly LibraryRootStore roots = roots;
    private readonly IOptions<TeamOptions> options = options;
    private readonly ILogger<LibraryPathResolver> logger = logger;

    /// <summary>
    /// Resolves <paramref name="relativePath"/> against the root named <paramref name="rootId"/>,
    /// following Spec §6.1 steps 1-7. Step 2's per-segment check is
    /// <see cref="LibrarySegments.Refusal(string)"/>; step 4 follows every reparse point along the
    /// way, re-checking containment against the resolved target; steps 5-7 classify the FINAL
    /// resolved location, which may differ from the root the caller named when a link or an
    /// overlapping pinned root reaches into the Teams or Teammates tree (item 25).
    /// </summary>
    /// <param name="rootId">The Library Root id (<see cref="LibraryRoot.Id"/>), compared ordinally.</param>
    /// <param name="relativePath">The path to resolve, relative to the root, either separator.</param>
    /// <param name="path">The resolved path, when this returns <see langword="true"/>.</param>
    /// <param name="error">The refusal reason, when this returns <see langword="false"/>.</param>
    /// <returns><see langword="true"/> when the path resolves inside the Library.</returns>
    internal bool TryResolve(string rootId, string relativePath, [NotNullWhen(true)] out LibraryPath? path, [NotNullWhen(false)] out string? error)
    {
        ArgumentNullException.ThrowIfNull(rootId);
        ArgumentNullException.ThrowIfNull(relativePath);

        path = null;
        error = null;

        LibraryRoot? root = FindRoot(this.roots.Roots, rootId);
        if (root is null)
        {
            error = $"Unknown Library root '{rootId}'.";
            return false;
        }

        string normalized = relativePath.Replace('\\', '/');
        if (normalized.Length == 0)
        {
            path = new LibraryPath(root, string.Empty, root.FullPath, LibraryNodeRole.Root);
            return true;
        }

        string[] segments = normalized.Split('/');
        foreach (string segment in segments)
        {
            string? refusal = LibrarySegments.Refusal(segment);
            if (refusal is not null)
            {
                error = refusal;
                return false;
            }
        }

        if (!TryWalk(root, segments, out string current, out error))
        {
            return false;
        }

        if (!this.TryClassify(current, out LibraryNodeRole role, out error))
        {
            return false;
        }

        path = new LibraryPath(root, normalized, current, role);
        return true;
    }

    /// <summary>
    /// Resolves one child of an already-resolved folder (Addendum A2): runs step 2 and steps 5-7 on
    /// <paramref name="child"/>'s name alone, following it only when it is itself a reparse point with
    /// a non-null <c>LinkTarget</c> (containment as step 4).
    /// </summary>
    /// <param name="parent">An already-resolved folder.</param>
    /// <param name="child">The child file or directory, as enumerated from <paramref name="parent"/>.</param>
    /// <param name="path">The resolved child path, when this returns <see langword="true"/>.</param>
    /// <param name="error">The refusal reason, when this returns <see langword="false"/>.</param>
    /// <returns><see langword="true"/> when the child resolves inside the Library.</returns>
    internal bool TryResolveChild(LibraryPath parent, FileSystemInfo child, [NotNullWhen(true)] out LibraryPath? path, [NotNullWhen(false)] out string? error)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(child);

        path = null;
        error = null;

        string name = child.Name;
        string? refusal = LibrarySegments.Refusal(name);
        if (refusal is not null)
        {
            error = refusal;
            return false;
        }

        string current = Path.Combine(parent.FullPath, name);
        string? rootResolvedTarget = ResolveIfLink(parent.Root.FullPath);

        if (child.Attributes.HasFlag(FileAttributes.ReparsePoint) && ResolveLinkTarget(child) is string target)
        {
            if (!IsWithinRoot(target, parent.Root.FullPath, rootResolvedTarget))
            {
                error = OutsideLibraryError;
                return false;
            }

            current = target;
        }

        if (!this.TryClassify(current, out LibraryNodeRole role, out error))
        {
            return false;
        }

        string relativePath = parent.RelativePath.Length == 0 ? name : $"{parent.RelativePath}/{name}";
        path = new LibraryPath(parent.Root, relativePath, current, role);
        return true;
    }

    /// <summary>
    /// Resolves an absolute Windows path or a <c>file:</c> URL (Spec §6.6) written in a chat Message.
    /// Only text starting <c>file:</c> (case-insensitive) is parsed as a URI first (item 32); the
    /// candidate must then satisfy <see cref="Path.IsPathFullyQualified(string)"/> BEFORE
    /// <see cref="Path.GetFullPath(string)"/> canonicalises it, so relative text and a Windows-shaped
    /// path on a platform that doesn't root it are refused without ever reaching a root. The winning
    /// root is whichever configured <see cref="LibraryRoot.FullPath"/> is the longest
    /// separator-terminated prefix of the canonical path; a tie goes to the first root in
    /// <see cref="LibraryRootStore.Roots"/> order (built-ins first). There is no fallback to a
    /// shorter root when the winner's own <see cref="TryResolve"/> call refuses.
    /// </summary>
    /// <param name="absolutePathOrFileUrl">An absolute path or <c>file:</c> URL.</param>
    /// <param name="path">The resolved path, when this returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the path resolves inside a Library Root.</returns>
    internal bool TryResolveAbsolute(string absolutePathOrFileUrl, [NotNullWhen(true)] out LibraryPath? path)
    {
        ArgumentNullException.ThrowIfNull(absolutePathOrFileUrl);
        path = null;

        string candidate = absolutePathOrFileUrl;
        if (candidate.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            if (!Uri.TryCreate(candidate, UriKind.Absolute, out Uri? uri) || !uri.IsFile)
            {
                return false;
            }

            candidate = uri.LocalPath;
        }

        if (!Path.IsPathFullyQualified(candidate))
        {
            return false;
        }

        string fullPath = Path.GetFullPath(candidate);

        LibraryRoot? winner = null;
        foreach (LibraryRoot candidateRoot in this.roots.Roots)
        {
            bool matches = PathEquals(fullPath, candidateRoot.FullPath) || HasPrefix(fullPath, candidateRoot.FullPath + Path.DirectorySeparatorChar);
            if (matches && (winner is null || candidateRoot.FullPath.Length > winner.FullPath.Length))
            {
                winner = candidateRoot;
            }
        }

        if (winner is null)
        {
            return false;
        }

        string relative = PathEquals(fullPath, winner.FullPath) ? string.Empty : fullPath[(winner.FullPath.Length + 1)..];
        return this.TryResolve(winner.Id, relative, out path, out _);
    }

    /// <summary>
    /// Resolves a Library Explorer scope (Spec §6.16): the scope is a location, never a path, so it
    /// goes through <see cref="TryResolve"/> like any other, and an existing file is refused since a
    /// scope must be a folder (item 33). Every failure returns the same host-facing text; the actual
    /// boundary reason is logged, not shown.
    /// </summary>
    /// <param name="scope">The root id and folder path the host wants to show.</param>
    /// <param name="folder">The resolved folder, when this returns <see langword="true"/>.</param>
    /// <param name="error">The host-facing refusal text, when this returns <see langword="false"/>.</param>
    /// <returns><see langword="true"/> when the scope resolves to a folder inside the Library.</returns>
    internal bool TryResolveScope(LibraryLocation scope, [NotNullWhen(true)] out LibraryPath? folder, [NotNullWhen(false)] out string? error)
    {
        ArgumentNullException.ThrowIfNull(scope);

        if (!this.TryResolve(scope.RootId, scope.FolderPath, out LibraryPath? resolved, out string? reason))
        {
            if (this.logger.IsEnabled(LogLevel.Debug))
            {
                this.logger.LogDebug("Library scope '{RootId}/{FolderPath}' refused: {Reason}", scope.RootId, scope.FolderPath, reason);
            }

            folder = null;
            error = ScopeUnavailableError;
            return false;
        }

        if (resolved.Role == LibraryNodeRole.File)
        {
            if (this.logger.IsEnabled(LogLevel.Debug))
            {
                this.logger.LogDebug("Library scope '{RootId}/{FolderPath}' is a file, not a folder.", scope.RootId, scope.FolderPath);
            }

            folder = null;
            error = ScopeUnavailableError;
            return false;
        }

        folder = resolved;
        error = null;
        return true;
    }

    /// <summary>Finds a root by id (<see cref="LibraryRoot.Id"/>), compared with <see cref="StringComparer.Ordinal"/> (Spec §6.1 step 1).</summary>
    private static LibraryRoot? FindRoot(IReadOnlyList<LibraryRoot> allRoots, string rootId)
    {
        foreach (LibraryRoot candidate in allRoots)
        {
            if (string.Equals(candidate.Id, rootId, StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// Walks each segment from the root down (step 3-4): joins and canonicalises, requires
    /// containment after every join, and after every reparse point with a non-null <c>LinkTarget</c>
    /// resolves the final target and re-checks containment against it.
    /// </summary>
    private static bool TryWalk(LibraryRoot root, IReadOnlyList<string> segments, out string current, [NotNullWhen(false)] out string? error)
    {
        current = root.FullPath;
        error = null;
        string? rootResolvedTarget = ResolveIfLink(root.FullPath);

        foreach (string segment in segments)
        {
            string canonical = Path.GetFullPath(Path.Combine(current, segment));
            if (!IsWithinRoot(canonical, root.FullPath, rootResolvedTarget))
            {
                error = OutsideLibraryError;
                current = string.Empty;
                return false;
            }

            current = canonical;

            if (ResolveIfLink(current) is string target)
            {
                if (!IsWithinRoot(target, root.FullPath, rootResolvedTarget))
                {
                    error = OutsideLibraryError;
                    current = string.Empty;
                    return false;
                }

                current = target;
            }
        }

        return true;
    }

    /// <summary>
    /// Classifies the resolved location <paramref name="current"/> against <see cref="TeamOptions.DataDir"/>
    /// and the canonical Teams and Teammates roots (steps 5-7), regardless of which root the caller
    /// named to reach it (item 25).
    /// </summary>
    private bool TryClassify(string current, out LibraryNodeRole role, [NotNullWhen(false)] out string? error)
    {
        role = LibraryNodeRole.Folder;
        error = null;

        string dataDir = Path.TrimEndingDirectorySeparator(Path.GetFullPath(this.options.Value.DataDir));
        if (HasPrefix(current, dataDir + Path.DirectorySeparatorChar))
        {
            string relativeToDataDir = current[(dataDir.Length + 1)..];
            string firstSegment = relativeToDataDir.Split(Path.DirectorySeparatorChar)[0];
            if (WatchedFolderResolver.ReservedFolders.Contains(firstSegment, StringComparer.OrdinalIgnoreCase))
            {
                error = ReservedFolderError;
                return false;
            }
        }

        LibraryRoot? teamsRoot = FindRoot(this.roots.Roots, "teams");
        if (teamsRoot is not null && HasPrefix(current, teamsRoot.FullPath + Path.DirectorySeparatorChar))
        {
            return TryClassifyUnderTeams(current, teamsRoot.FullPath, out role, out error);
        }

        LibraryRoot? teammatesRoot = FindRoot(this.roots.Roots, "teammates");
        if (teammatesRoot is not null && HasPrefix(current, teammatesRoot.FullPath + Path.DirectorySeparatorChar))
        {
            role = ClassifyUnderTeammates(current, teammatesRoot.FullPath);
            return true;
        }

        role = File.Exists(current) ? LibraryNodeRole.File : LibraryNodeRole.Folder;
        return true;
    }

    /// <summary>Step 6: refuses any segment under Teams that is <c>_</c>- or <c>.</c>-prefixed, else assigns depth-1/depth-2 roles.</summary>
    private static bool TryClassifyUnderTeams(string current, string teamsRoot, out LibraryNodeRole role, [NotNullWhen(false)] out string? error)
    {
        role = LibraryNodeRole.Folder;
        error = null;

        string[] relativeSegments = current[(teamsRoot.Length + 1)..].Split(Path.DirectorySeparatorChar);
        foreach (string segment in relativeSegments)
        {
            if (TaskLayout.IsReservedFolderName(segment))
            {
                error = ReservedFolderError;
                return false;
            }
        }

        if (File.Exists(current))
        {
            role = LibraryNodeRole.File;
        }
        else
        {
            role = relativeSegments.Length switch
            {
                1 => LibraryNodeRole.TeamFolder,
                2 => LibraryNodeRole.ProjectFolder,
                _ => LibraryNodeRole.Folder,
            };
        }

        return true;
    }

    /// <summary>Step 7: the Teammate folder, its <c>&lt;Name&gt;.md</c> definition, and its Work Dir.</summary>
    private static LibraryNodeRole ClassifyUnderTeammates(string current, string teammatesRoot)
    {
        string[] relativeSegments = current[(teammatesRoot.Length + 1)..].Split(Path.DirectorySeparatorChar);
        bool isFile = File.Exists(current);

        if (relativeSegments.Length == 1)
        {
            if (isFile)
            {
                return LibraryNodeRole.File;
            }

            return TaskLayout.IsReservedFolderName(relativeSegments[0]) ? LibraryNodeRole.Folder : LibraryNodeRole.TeammateFolder;
        }

        if (relativeSegments.Length == 2)
        {
            string teammateName = relativeSegments[0];
            string leaf = relativeSegments[1];

            if (isFile
                && string.Equals(Path.GetExtension(leaf), ".md", StringComparison.OrdinalIgnoreCase)
                && string.Equals(Path.GetFileNameWithoutExtension(leaf), teammateName, StringComparison.OrdinalIgnoreCase))
            {
                return LibraryNodeRole.TeammateDefinition;
            }

            if (!isFile && FolderSnapshot.PathComparer.Equals(leaf, "work"))
            {
                return LibraryNodeRole.WorkDir;
            }
        }

        return isFile ? LibraryNodeRole.File : LibraryNodeRole.Folder;
    }

    /// <summary>
    /// When <paramref name="path"/> exists and is a reparse point with a non-null <c>LinkTarget</c>,
    /// resolves its final target; a reparse point with a <see langword="null"/> <c>LinkTarget</c>
    /// (a OneDrive placeholder, a dedup entry) is an ordinary entry (item 26).
    /// </summary>
    private static string? ResolveIfLink(string path)
    {
        if (Directory.Exists(path))
        {
            DirectoryInfo info = new(path);
            return info.Attributes.HasFlag(FileAttributes.ReparsePoint) ? ResolveLinkTarget(info) : null;
        }

        if (File.Exists(path))
        {
            FileInfo info = new(path);
            return info.Attributes.HasFlag(FileAttributes.ReparsePoint) ? ResolveLinkTarget(info) : null;
        }

        return null;
    }

    /// <summary>Resolves <paramref name="entry"/>'s final link target, or <see langword="null"/> when its <c>LinkTarget</c> is itself null (item 26).</summary>
    private static string? ResolveLinkTarget(FileSystemInfo entry) =>
        entry.LinkTarget is not null && entry.ResolveLinkTarget(returnFinalTarget: true) is FileSystemInfo resolved
            ? resolved.FullName
            : null;

    /// <summary>Containment (step 3, item 26): equal to or under the root's lexical path, or - when the root is itself a link - equal to or under the root's own resolved target.</summary>
    private static bool IsWithinRoot(string candidate, string rootLexical, string? rootResolvedTarget)
    {
        if (PathEquals(candidate, rootLexical) || HasPrefix(candidate, rootLexical + Path.DirectorySeparatorChar))
        {
            return true;
        }

        return rootResolvedTarget is not null
            && (PathEquals(candidate, rootResolvedTarget) || HasPrefix(candidate, rootResolvedTarget + Path.DirectorySeparatorChar));
    }

    /// <summary>True when <paramref name="value"/> starts with <paramref name="prefix"/>, compared with <see cref="FolderSnapshot.PathComparer"/>.</summary>
    private static bool HasPrefix(string value, string prefix) =>
        value.Length >= prefix.Length && FolderSnapshot.PathComparer.Equals(value[..prefix.Length], prefix);

    /// <summary>Path equality compared with <see cref="FolderSnapshot.PathComparer"/>.</summary>
    private static bool PathEquals(string a, string b) => FolderSnapshot.PathComparer.Equals(a, b);
}
