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
    /// <summary>Argument names that carry a filesystem path in the tool calls this application sees, matched ignoring case.</summary>
    private static readonly string[] PathArgumentNames = ["file_path", "path", "notebook_path"];

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

    /// <summary>
    /// Whether a full path names a place through an alias the guards cannot compare by name: on Windows, an 8.3 short-name
    /// segment (<c>INSTRU~1.MD</c>; the extension is set aside, because the Library's own alias check ignores it) or a <c>:</c> after the drive (an alternate data stream, <c>Instructions.md:x</c>). A guard
    /// refuses such a write rather than guess what it resolves to. Junctions and symbolic links are not detected: that gap is
    /// documented on the guards and in the design's Appendix B, not resolved.
    /// </summary>
    /// <param name="fullPath">A full path.</param>
    /// <returns><see langword="true"/> when the path goes through an alias.</returns>
    public static bool UsesAlias(string fullPath)
    {
        ArgumentNullException.ThrowIfNull(fullPath);

        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        string afterRoot = fullPath[(Path.GetPathRoot(fullPath) ?? string.Empty).Length..];
        if (afterRoot.Contains(':'))
        {
            return true;
        }

        return afterRoot
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries)
            .Any(static segment => LibrarySegments.Check(Path.GetFileNameWithoutExtension(segment))?.Kind == SegmentProblemKind.ShortNameAlias);
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

    /// <summary>Reads each recognised string argument out of the call's input object.</summary>
    /// <param name="root">The input object.</param>
    /// <param name="baseDir">The directory a relative path resolves against.</param>
    private static List<string> ReadPaths(JsonElement root, string baseDir)
    {
        List<string> paths = [];
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (PathArgumentNames.Contains(property.Name, StringComparer.OrdinalIgnoreCase)
                && property.Value.ValueKind == JsonValueKind.String
                && property.Value.GetString() is { Length: > 0 } candidate
                && Resolve(candidate, baseDir) is { } full)
            {
                paths.Add(full);
            }
        }

        return paths;
    }
}
