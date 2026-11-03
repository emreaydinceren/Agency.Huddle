using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// Refuses an Agent's tool call that edits an existing original, or deletes or moves an original or a folder holding one, in a
/// <c>raw/</c> position: below the Raw root, or in a Team's or Project's <c>raw</c> folder under the Teams root or the Archive (Spec §8.6, SB-R05).
/// An Agent adds a new file beside the original instead.
/// </summary>
/// <remarks>
/// Like <see cref="AgentsMdGuard"/> it sees path arguments only, so a shell command gets past it: that boundary is soft and documented.
/// A read is never refused, and neither is the creation of a file that does not exist yet.
/// </remarks>
/// <param name="teamsRoot">The Teams root folder.</param>
/// <param name="rawRoot">The Raw root folder.</param>
/// <param name="archiveRoot">The Archive root folder.</param>
/// <param name="workDir">The Agent's own Work Dir, where a relative path resolves.</param>
/// <param name="prompts">Renders the refusal reason (<c>systemPrompt.rawRefusal</c>).</param>
internal sealed class RawGuard(string teamsRoot, string rawRoot, string archiveRoot, string workDir, IPromptSource prompts)
{
    /// <summary>Decides whether the tool call must be refused.</summary>
    /// <param name="toolCall">The call the Agent is asking permission for.</param>
    /// <returns>The refusal reason, or <see langword="null"/> when the call does not touch an original.</returns>
    public string? RefusalFor(ToolCallInfo toolCall)
    {
        ArgumentNullException.ThrowIfNull(toolCall);

        if (!ToolCallPaths.ChangesFiles(toolCall.Kind))
        {
            return null;
        }

        bool removes = toolCall.Kind is ToolKind.Delete or ToolKind.Move;
        foreach (string resolved in ToolCallPaths.From(toolCall, workDir))
        {
            string path = Path.GetFullPath(resolved.Replace('\\', '/'));
            if (ToolCallPaths.UsesAliasInside(path, Path.GetFullPath(rawRoot.Replace('\\', '/')))
                || ToolCallPaths.UsesAliasInside(path, Path.GetFullPath(archiveRoot.Replace('\\', '/'))))
            {
                return prompts.Render("systemPrompt.pathAliasRefusal", new Dictionary<string, string> { ["{{path}}"] = path });
            }

            bool refused = removes
                ? this.IsRawPosition(path) || this.HoldsRaw(path)
                : this.IsRawPosition(path) && File.Exists(path);
            if (refused)
            {
                return prompts.Render("systemPrompt.rawRefusal");
            }
        }

        return null;
    }

    /// <summary>Whether the full path is in a raw position: anywhere below the Raw root, or a <c>raw</c> folder of a Team or Project under the Teams root or the Archive.</summary>
    /// <param name="path">A full path with platform separators.</param>
    /// <returns><see langword="true"/> when the path is in a raw position.</returns>
    private bool IsRawPosition(string path) =>
        Inside(path, rawRoot) || IsRawBelow(path, teamsRoot) || IsRawBelow(path, archiveRoot);

    /// <summary>Whether deleting or moving the path would take a raw position with it: a root or a folder above one, or an existing Team or Project folder.</summary>
    /// <param name="path">A full path with platform separators.</param>
    /// <returns><see langword="true"/> when removing the path would remove a raw position.</returns>
    private bool HoldsRaw(string path) =>
        Inside(rawRoot, path) || Inside(teamsRoot, path) || Inside(archiveRoot, path)
        || HoldsRawBelow(path, teamsRoot) || HoldsRawBelow(path, archiveRoot);

    /// <summary>Whether the path is an existing Team folder or Project folder directly below <paramref name="root"/>.</summary>
    /// <param name="path">A full path.</param>
    /// <param name="root">The Teams root or the Archive.</param>
    /// <returns><see langword="true"/> when the path is such a folder and it exists.</returns>
    private static bool HoldsRawBelow(string path, string root)
    {
        string[] segments = SegmentsBelow(path, root);
        bool holder = segments.Length switch
        {
            1 => !TeamNames.IsReservedTeamName(segments[0]),
            2 => !TeamNames.IsReservedTeamName(segments[0]) && !TeamNames.IsReservedProjectName(segments[1]),
            _ => false,
        };
        return holder && Directory.Exists(path);
    }

    /// <summary>Whether the path is a <c>raw</c> folder of a Team (segment 1) or of a Project (segment 2), or inside one, below <paramref name="root"/>.</summary>
    /// <param name="path">A full path.</param>
    /// <param name="root">The Teams root or the Archive.</param>
    /// <returns><see langword="true"/> when the path is in a <c>raw</c> folder below the root.</returns>
    private static bool IsRawBelow(string path, string root)
    {
        string[] segments = SegmentsBelow(path, root);
        if (segments.Length < 2 || TeamNames.IsReservedTeamName(segments[0]))
        {
            return false;
        }

        return IsRawName(segments[1])
            || (segments.Length > 2 && IsRawName(segments[2]) && !TeamNames.IsReservedProjectName(segments[1]));
    }

    /// <summary>The path's segments below <paramref name="root"/>; empty when it is not inside it.</summary>
    /// <param name="path">A full path.</param>
    /// <param name="root">A folder.</param>
    /// <returns>The segments, or an empty array.</returns>
    private static string[] SegmentsBelow(string path, string root) =>
        PathContainment.Below(path, Path.GetFullPath(root.Replace('\\', '/')), StringComparison.OrdinalIgnoreCase)
            ?.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries)
        ?? [];

    /// <summary>Whether the segment is the <c>raw</c> folder name, ignoring case.</summary>
    /// <param name="segment">One path segment.</param>
    /// <returns><see langword="true"/> when it names a <c>raw</c> folder.</returns>
    private static bool IsRawName(string segment) => string.Equals(segment, TeamNames.RawFolder, StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether <paramref name="path"/> is <paramref name="root"/> or inside it, ignoring case.</summary>
    /// <param name="path">A full path.</param>
    /// <param name="root">A folder.</param>
    /// <returns><see langword="true"/> when the path is the root or inside it.</returns>
    private static bool Inside(string path, string root) =>
        PathContainment.IsInside(path, Path.GetFullPath(root.Replace('\\', '/')), includeRoot: true, StringComparison.OrdinalIgnoreCase);
}
