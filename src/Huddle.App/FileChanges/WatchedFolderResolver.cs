using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;
using Agency.Huddle.App.Acp;

namespace Agency.Huddle.App.FileChanges;

/// <summary>A Watched Folder entry, resolved to a full path, per FC §6.2-§6.3.</summary>
/// <param name="Entry">The entry as written (or as passed to <see cref="WatchedFolderResolver.TryResolve"/>).</param>
/// <param name="FullPath">The entry's resolved full path.</param>
/// <param name="PruneUnderscore">
/// True when this folder sits inside the Teams root, per Spec §6.13: its scan prunes any
/// <c>_</c>-prefixed sub-folder (a Team's Tasks or drafts folder), so Task files never appear in
/// File Changes.
/// </param>
internal sealed record WatchedFolder(string Entry, string FullPath, bool PruneUnderscore = false);

/// <summary>
/// Resolves a Watched Folder entry — a Teammate Name, a full path, or a path relative to
/// <c>DataDir</c> — into a <see cref="WatchedFolder"/>, or refuses it with a reason, per
/// FC §6.2-§6.3. Pure apart from <see cref="Path.GetFullPath(string)"/>; never touches the disk.
/// </summary>
internal sealed class WatchedFolderResolver(IOptions<TeamOptions> options, TeammatePaths teammatePaths)
{
    /// <summary>
    /// The folder names Huddle reserves for its own data, compared case-insensitively: an entry
    /// resolving inside one of these is refused. Finding P-3 adds <c>room-sessions</c> to the four
    /// FC §6.3 names, ahead of <c>Huddle.RoomSessions-Specifications.md</c>'s own use of it.
    /// </summary>
    internal static IReadOnlyList<string> ReservedFolders { get; } = ["rooms", "logs", "file-state", "avatars", "room-sessions"];

    /// <summary>
    /// Resolves <paramref name="entry"/>. Checks, in order: not blank; a <c>./</c>/<c>.\</c>
    /// prefix means a folder relative to <c>DataDir</c>; else a Teammate Name (matched
    /// case-insensitively against <paramref name="teammateNames"/>) resolves to that Teammate's
    /// Work Dir; else a fully qualified path resolves to itself; else the entry is relative to
    /// <c>DataDir</c>, with either separator. The resolved full path must then lie inside
    /// <c>DataDir</c>, must not be <c>DataDir</c> itself, and its first segment relative to
    /// <c>DataDir</c> must not be a <see cref="ReservedFolders"/> entry.
    /// </summary>
    /// <param name="entry">The entry text, as written in frontmatter or passed to <c>watch_folder</c>.</param>
    /// <param name="teammateNames">Every Teammate's Name, for the Teammate-Name resolution step.</param>
    /// <param name="folder">The resolved <see cref="WatchedFolder"/>, when this returns <see langword="true"/>.</param>
    /// <param name="reason">The refusal reason, when this returns <see langword="false"/>.</param>
    internal bool TryResolve(
        string entry,
        IReadOnlyCollection<string> teammateNames,
        [NotNullWhen(true)] out WatchedFolder? folder,
        [NotNullWhen(false)] out string? reason)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(teammateNames);

        folder = null;
        reason = null;

        if (string.IsNullOrWhiteSpace(entry))
        {
            reason = "A Watched Folder entry is blank.";
            return false;
        }

        string dataDir = options.Value.DataDir;
        string fullPath = this.ResolveFullPath(entry, dataDir, teammateNames);

        if (string.Equals(fullPath, dataDir, StringComparison.OrdinalIgnoreCase))
        {
            reason = $"'{entry}' is Huddle's own data folder, not a working folder.";
            return false;
        }

        string prefix = dataDir + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            reason = $"'{fullPath}' is outside {Path.GetFileName(dataDir)}. Only folders inside it can be watched.";
            return false;
        }

        string relativeToDataDir = fullPath[prefix.Length..];
        string firstSegment = relativeToDataDir.Split(Path.DirectorySeparatorChar, 2)[0];
        if (ReservedFolders.Contains(firstSegment, StringComparer.OrdinalIgnoreCase))
        {
            reason = $"'{entry}' holds Huddle's own data, not working files.";
            return false;
        }

        folder = new WatchedFolder(entry, fullPath);
        return true;
    }

    /// <summary>Resolves <paramref name="entry"/> to a full path, per the ordered rules in FC §6.3, before the containment checks run.</summary>
    private string ResolveFullPath(string entry, string dataDir, IReadOnlyCollection<string> teammateNames)
    {
        if (entry.StartsWith("./", StringComparison.Ordinal) || entry.StartsWith(".\\", StringComparison.Ordinal))
        {
            return Path.GetFullPath(Path.Combine(dataDir, WatchedFolderResolver.NormalizeSeparators(entry[2..])));
        }

        string? canonicalName = teammateNames.FirstOrDefault(name => string.Equals(name, entry, StringComparison.OrdinalIgnoreCase));
        if (canonicalName is not null)
        {
            return Path.GetFullPath(teammatePaths.WorkDir(canonicalName));
        }

        if (Path.IsPathFullyQualified(entry))
        {
            return Path.GetFullPath(entry);
        }

        return Path.GetFullPath(Path.Combine(dataDir, WatchedFolderResolver.NormalizeSeparators(entry)));
    }

    /// <summary>
    /// Maps both <c>/</c> and <c>\</c> to <see cref="Path.DirectorySeparatorChar"/> (FC §6.2-§6.3: an
    /// entry may use either separator, regardless of the OS the app runs on). Only a fully qualified
    /// path skips this - <see cref="Path.IsPathFullyQualified(string)"/> and <see cref="Path.GetFullPath(string)"/>
    /// already interpret it with the platform's own separator rules.
    /// </summary>
    private static string NormalizeSeparators(string relativeEntry) =>
        relativeEntry.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
}
