using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Teams;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;

namespace Agency.Huddle.App.FileChanges;

/// <summary>
/// Resolves a Watched Folder entry — an Agent Name, a full path, or a path relative to
/// <c>DataDir</c> — into a <see cref="WatchedFolder"/>, or refuses it with a reason, per
/// FC §6.2-§6.3. Pure apart from <see cref="Path.GetFullPath(string)"/>; never touches the disk.
/// </summary>
internal sealed class WatchedFolderResolver(IOptions<TeamOptions> options, AgentPaths agentPaths, ITeamRenames? renames = null)
{
    /// <summary>
    /// The folder names Huddle reserves for its own data, compared case-insensitively: an entry
    /// resolving inside one of these is refused. Finding P-3 adds <c>room-sessions</c> to the four
    /// FC §6.3 names, ahead of <c>Huddle.RoomSessions-Specifications.md</c>'s own use of it.
    /// </summary>
    internal static IReadOnlyList<string> ReservedFolders { get; } = ["rooms", "logs", "file-state", "avatars", "room-sessions"];

    /// <summary>
    /// Resolves <paramref name="entry"/>. Checks, in order: not blank; a <c>./</c>/<c>.\</c>
    /// prefix means a folder relative to <c>DataDir</c>; else an Agent Name (matched
    /// case-insensitively against <paramref name="agentNames"/>) resolves to that Agent's
    /// Work Dir; else a fully qualified path resolves to itself; else the entry is relative to
    /// <c>DataDir</c>, with either separator. The resolved full path must then lie inside
    /// <c>DataDir</c>, must not be <c>DataDir</c> itself, and its first segment relative to
    /// <c>DataDir</c> must not be a <see cref="ReservedFolders"/> entry.
    /// </summary>
    /// <param name="entry">The entry text, as written in frontmatter or passed to <c>watch_folder</c>.</param>
    /// <param name="agentNames">Every Agent's Name, for the Agent-Name resolution step.</param>
    /// <param name="folder">The resolved <see cref="WatchedFolder"/>, when this returns <see langword="true"/>.</param>
    /// <param name="reason">The refusal reason, when this returns <see langword="false"/>.</param>
    internal bool TryResolve(
        string entry,
        IReadOnlyCollection<string> agentNames,
        [NotNullWhen(true)] out WatchedFolder? folder,
        [NotNullWhen(false)] out string? reason)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(agentNames);

        folder = null;
        reason = null;

        if (string.IsNullOrWhiteSpace(entry))
        {
            reason = "A Watched Folder entry is blank.";
            return false;
        }

        string dataDir = options.Value.DataDir;
        string fullPath = this.ResolveFullPath(entry, dataDir, agentNames);
        if (renames is not null)
        {
            fullPath = RenamedTeamPaths.Map(Path.GetFullPath(Path.Combine(dataDir, options.Value.Teams.Dir)), fullPath, renames);
        }

        if (string.Equals(fullPath, dataDir, StringComparison.OrdinalIgnoreCase))
        {
            reason = $"'{entry}' is Huddle's own data folder, not a working folder.";
            return false;
        }

        if (!PathContainment.IsInside(fullPath, dataDir, includeRoot: false, PathContainment.Comparison))
        {
            reason = $"'{fullPath}' is outside {Path.GetFileName(dataDir)}. Only folders inside it can be watched.";
            return false;
        }

        string relativeToDataDir = Path.GetRelativePath(dataDir, fullPath);
        string firstSegment = relativeToDataDir.Split(Path.DirectorySeparatorChar, 2)[0];
        if (ReservedFolders.Contains(firstSegment, StringComparer.OrdinalIgnoreCase))
        {
            reason = $"'{entry}' holds Huddle's own data, not working files.";
            return false;
        }

        string uploadsRoot = Path.GetFullPath(Path.Combine(dataDir, options.Value.Library.Raw.Dir));
        string teamsRoot = Path.GetFullPath(Path.Combine(dataDir, options.Value.Teams.Dir));
        // Spec 6.8 refuses the raw folder whichever case it is typed in, so these refusals ignore case on every platform.
        if (PathContainment.IsInside(fullPath, uploadsRoot, includeRoot: true, StringComparison.OrdinalIgnoreCase)
            || WatchedFolderResolver.IsTeamRaw(fullPath, teamsRoot))
        {
            reason = "Uploads are announced in the chat, not watched.";
            return false;
        }

        folder = new WatchedFolder(entry, fullPath);
        return true;
    }

    /// <summary>True when <paramref name="fullPath"/> is a Team's raw folder or lies under it: its segments below the Teams root are <c>[Team, raw, ...]</c>.</summary>
    /// <param name="fullPath">The resolved full path.</param>
    /// <param name="teamsRoot">The full path of the Teams root.</param>
    /// <returns><see langword="true"/> for a Team's raw folder or anything under it.</returns>
    private static bool IsTeamRaw(string fullPath, string teamsRoot)
    {
        if (PathContainment.Below(fullPath, teamsRoot, StringComparison.OrdinalIgnoreCase) is not { Length: > 0 } below)
        {
            return false;
        }

        string[] segments = below.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 2 && string.Equals(segments[1], TeamNames.RawFolder, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Resolves <paramref name="entry"/> to a full path, per the ordered rules in FC §6.3, before the containment checks run.</summary>
    private string ResolveFullPath(string entry, string dataDir, IReadOnlyCollection<string> agentNames)
    {
        if (entry.StartsWith("./", StringComparison.Ordinal) || entry.StartsWith(".\\", StringComparison.Ordinal))
        {
            return Path.GetFullPath(Path.Combine(dataDir, WatchedFolderResolver.NormalizeSeparators(entry[2..])));
        }

        string? canonicalName = agentNames.FirstOrDefault(name => string.Equals(name, entry, StringComparison.OrdinalIgnoreCase));
        if (canonicalName is not null)
        {
            return Path.GetFullPath(agentPaths.WorkDir(canonicalName));
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
