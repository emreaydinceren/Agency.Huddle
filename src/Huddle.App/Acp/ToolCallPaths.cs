using System.Text.Json;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// Reads the filesystem paths a tool call names, for the guards in <see cref="WorkDirPermissionHandler"/> that refuse a
/// write to a file the Human owns (Instructions spec §3.3, §5.6). It sees path arguments only, never shell redirects:
/// the same soft boundary as the <c>~/.claude</c> guard.
/// </summary>
internal static class ToolCallPaths
{
    /// <summary>
    /// Argument names that carry a filesystem path in the tool calls this application sees, matched ignoring case: the
    /// single-file tools' <c>file_path</c>, <c>path</c> and <c>notebook_path</c>, and the names a move or copy tool gives its source
    /// and destination, so a Move's destination is read as well as its source. A name that holds an array of strings is read element by element.
    /// </summary>
    private static readonly string[] PathArgumentNames =
    [
        "file_path", "path", "notebook_path", "old_path", "new_path", "old_file_path", "new_file_path",
        "source", "source_path", "src", "from", "destination", "destination_path", "dest", "dest_path",
        "target", "target_path", "to", "paths", "file_paths", "sources",
    ];

    /// <summary>
    /// Whether the tool call changes a file. This is the set <c>RoomSession</c> uses for the paths a Turn touched, so a
    /// guard that uses it never refuses a read.
    /// </summary>
    /// <param name="kind">The tool call's kind.</param>
    /// <returns><see langword="true"/> for an edit, a delete or a move.</returns>
    public static bool ChangesFiles(ToolKind kind) => kind is ToolKind.Edit or ToolKind.Delete or ToolKind.Move;

    /// <summary>The full paths named by the tool call's path arguments, a relative path resolved against <paramref name="baseDir"/>.</summary>
    /// <param name="toolCall">The call the Agent is asking permission for.</param>
    /// <param name="baseDir">The working directory the Adapter resolves a relative path against: the Agent's Work Dir.</param>
    /// <returns>The paths; empty when the input carries none, is not JSON, or names a path that cannot be resolved.</returns>
    public static IReadOnlyList<string> From(ToolCallInfo toolCall, string baseDir)
    {
        ArgumentNullException.ThrowIfNull(toolCall);

        if (toolCall.RawInputJson is not { Length: > 0 } json)
        {
            return [];
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object ? ReadPaths(document.RootElement, baseDir) : [];
        }
        catch (JsonException)
        {
            // Not something the guards can judge; they refuse only what they can positively identify.
            return [];
        }
    }

    /// <summary>Whether two paths name the same place, ignoring case and a trailing separator.</summary>
    /// <param name="left">One full path.</param>
    /// <param name="right">The other full path.</param>
    /// <returns><see langword="true"/> when they are equal.</returns>
    public static bool Same(string left, string right) =>
        string.Equals(Path.TrimEndingDirectorySeparator(left), Path.TrimEndingDirectorySeparator(right), StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether <paramref name="fullPath"/> is <paramref name="root"/> or inside it, ignoring case and a trailing separator.</summary>
    /// <param name="fullPath">A full path.</param>
    /// <param name="root">A full path to a folder.</param>
    /// <returns><see langword="true"/> when the path is the folder or below it.</returns>
    public static bool IsInside(string fullPath, string root)
    {
        ArgumentNullException.ThrowIfNull(fullPath);
        ArgumentNullException.ThrowIfNull(root);

        string relative = Path.GetRelativePath(Path.GetFullPath(root), fullPath);
        return !Path.IsPathRooted(relative)
            && !string.Equals(relative, "..", StringComparison.Ordinal)
            && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal);
    }

    /// <summary>Whether <paramref name="ancestor"/> is <paramref name="fullPath"/> itself or a folder above it: deleting or moving the ancestor takes the path with it.</summary>
    /// <param name="fullPath">A full path.</param>
    /// <param name="ancestor">A full path to a folder, or the same path.</param>
    /// <returns><see langword="true"/> when the path is the ancestor or below it.</returns>
    public static bool IsAtOrAbove(string ancestor, string fullPath) => IsInside(fullPath, ancestor);

    /// <summary>
    /// Whether a full path inside <paramref name="root"/> names its place through an alias the guards cannot compare by name: on Windows, an 8.3
    /// short-name segment (<c>INSTRU~1.MD</c>; the extension is set aside, because the Library's own alias check ignores it) or a <c>:</c> after the
    /// drive (an alternate data stream, <c>Instructions.md:x</c>). Only the part below <paramref name="root"/> is judged, and a path outside it is
    /// never an alias here: a guard decides only inside the place it guards, so an ordinary edit elsewhere (a Work Dir under a
    /// <c>RUNNER~1</c> profile, a file called <c>notes~2.md</c> outside) is not refused. A guard refuses such a write rather than guess what it
    /// resolves to. A short name in the part of the path that leads to <paramref name="root"/> is not detected, and neither are junctions or
    /// symbolic links: that gap is documented on the guards and in the design's Appendix B, not resolved.
    /// </summary>
    /// <param name="fullPath">A full path.</param>
    /// <param name="root">The guarded folder the path must be inside for the question to arise.</param>
    /// <param name="maxDepth">How many segments below <paramref name="root"/> the guarded files can be; a deeper path is not an alias the guard has to decide.</param>
    /// <param name="shortNameOf">When set, the last segment counts as an alias only if it is a short name of this file name (<c>INSTRU~1.MD</c> for <c>Instructions.md</c>), so <c>notes~2.md</c> is left alone.</param>
    /// <returns><see langword="true"/> when the path is inside the folder and goes through an alias.</returns>
    public static bool UsesAliasInside(string fullPath, string root, int maxDepth = int.MaxValue, string? shortNameOf = null)
    {
        ArgumentNullException.ThrowIfNull(fullPath);
        ArgumentNullException.ThrowIfNull(root);

        if (!OperatingSystem.IsWindows() || !IsInside(fullPath, root))
        {
            return false;
        }

        string below = Path.GetRelativePath(Path.GetFullPath(root), fullPath);
        string[] segments = below.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length > maxDepth)
        {
            return false;
        }

        if (below.Contains(':'))
        {
            return true;
        }

        for (int index = 0; index < segments.Length; index++)
        {
            bool last = index == segments.Length - 1;
            if (LibrarySegments.Check(Path.GetFileNameWithoutExtension(segments[index]))?.Kind != SegmentProblemKind.ShortNameAlias)
            {
                continue;
            }

            if (!last || shortNameOf is null || IsShortNameOf(segments[index], shortNameOf))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether a segment has the shape of the 8.3 short name Windows would give <paramref name="longName"/>: its characters before the <c>~</c> start the long name and its extension starts the long extension.</summary>
    /// <param name="segment">A path segment such as <c>INSTRU~1.MD</c>.</param>
    /// <param name="longName">A file name such as <c>Instructions.md</c>.</param>
    private static bool IsShortNameOf(string segment, string longName)
    {
        string name = Path.GetFileNameWithoutExtension(segment);
        int tilde = name.LastIndexOf('~');
        return tilde > 0
            && Path.GetFileNameWithoutExtension(longName).StartsWith(name[..tilde], StringComparison.OrdinalIgnoreCase)
            && Path.GetExtension(longName).StartsWith(Path.GetExtension(segment), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The full path of <paramref name="path"/>, or <see langword="null"/> when it cannot be resolved.</summary>
    /// <param name="path">A path, absolute or relative.</param>
    /// <param name="baseDir">The directory a relative path resolves against.</param>
    /// <returns>The full path.</returns>
    public static string? Resolve(string path, string baseDir)
    {
        try
        {
            return Path.GetFullPath(path, baseDir);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    /// <summary>Reads each recognised string (or array of strings) argument out of the call's input object.</summary>
    /// <param name="root">The input object.</param>
    /// <param name="baseDir">The directory a relative path resolves against.</param>
    private static List<string> ReadPaths(JsonElement root, string baseDir)
    {
        List<string> paths = [];
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (!PathArgumentNames.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement value in property.Value.EnumerateArray())
                {
                    AddPath(value, baseDir, paths);
                }
            }
            else
            {
                AddPath(property.Value, baseDir, paths);
            }
        }

        return paths;
    }

    /// <summary>Adds the element's full path when it is a non-empty string that resolves.</summary>
    /// <param name="value">The argument or array element.</param>
    /// <param name="baseDir">The directory a relative path resolves against.</param>
    /// <param name="paths">The list to add to.</param>
    private static void AddPath(JsonElement value, string baseDir, List<string> paths)
    {
        if (value.ValueKind == JsonValueKind.String
            && value.GetString() is { Length: > 0 } candidate
            && Resolve(candidate, baseDir) is { } full)
        {
            paths.Add(full);
        }
    }
}
