using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// Refuses an Agent's tool call that edits, deletes or moves an <c>AGENTS.md</c> at the Teams root, in a Team folder or in a Project folder
/// (Spec §6.6, SB-R09), or that deletes or moves a folder that holds one. <c>AGENTS.md</c> is the Human's file; an Agent proposes a change in a retrospective.
/// </summary>
/// <remarks>
/// A read is never refused. Like <see cref="RawGuard"/> it sees path arguments only, so a shell command gets past it: that boundary is soft and documented.
/// The guarded positions are depth 1 (<c>Teams/AGENTS.md</c>), depth 2 (<c>Teams/&lt;Team&gt;/AGENTS.md</c>) and depth 3
/// (<c>Teams/&lt;Team&gt;/&lt;Project&gt;/AGENTS.md</c>) where the Team and the Project are not reserved names, so a file in <c>outputs</c>, <c>memory</c> or <c>raw</c>
/// is not guarded. Paths are compared ignoring case. A write through a Windows 8.3 short name or an alternate data stream inside the Teams root is refused outright.
/// </remarks>
/// <param name="teamsRoot">The Teams root folder.</param>
/// <param name="workDir">The Agent's own Work Dir, where a relative path resolves.</param>
/// <param name="prompts">Renders the refusal reason (<c>systemPrompt.agentsMdRefusal</c>).</param>
internal sealed class AgentsMdGuard(string teamsRoot, string workDir, IPromptSource prompts)
{
    /// <summary>The deepest segment count below the Teams root at which an <c>AGENTS.md</c> is guarded.</summary>
    private const int MaxDepth = 3;

    /// <summary>Decides whether the tool call must be refused.</summary>
    /// <param name="toolCall">The call the Agent is asking permission for.</param>
    /// <returns>The refusal reason, or <see langword="null"/> when the call is not a write to a guarded <c>AGENTS.md</c>.</returns>
    public string? RefusalFor(ToolCallInfo toolCall)
    {
        ArgumentNullException.ThrowIfNull(toolCall);

        if (!ToolCallPaths.ChangesFiles(toolCall.Kind))
        {
            return null;
        }

        bool removes = toolCall.Kind is ToolKind.Delete or ToolKind.Move;
        string root = Path.GetFullPath(teamsRoot.Replace('\\', '/'));
        foreach (string resolved in ToolCallPaths.From(toolCall, workDir))
        {
            string path = Path.GetFullPath(resolved.Replace('\\', '/'));
            if (ToolCallPaths.UsesAliasInside(path, root, MaxDepth, removes ? null : TeamPaths.AgentsMdFileName))
            {
                return prompts.Render("systemPrompt.pathAliasRefusal", new Dictionary<string, string> { ["{{path}}"] = path });
            }

            if (IsGuardedFile(path, root) || (removes && HoldsGuardedFile(path, root)))
            {
                return prompts.Render("systemPrompt.agentsMdRefusal");
            }
        }

        return null;
    }

    /// <summary>Whether the path is an <c>AGENTS.md</c> at a guarded depth below the Teams root.</summary>
    /// <param name="path">A full path.</param>
    /// <param name="root">The full Teams root.</param>
    /// <returns><see langword="true"/> when the path is a guarded file.</returns>
    private static bool IsGuardedFile(string path, string root)
    {
        string[] segments = SegmentsBelow(path, root);
        if (segments.Length is < 1 or > MaxDepth
            || !string.Equals(segments[^1], TeamPaths.AgentsMdFileName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return segments.Length switch
        {
            2 => !TeamNames.IsReservedTeamName(segments[0]),
            MaxDepth => !TeamNames.IsReservedTeamName(segments[0]) && !TeamNames.IsReservedProjectName(segments[1]),
            _ => true,
        };
    }

    /// <summary>Whether deleting or moving the path would take a guarded file with it: the Teams root or a folder above it, or an existing Team or Project folder.</summary>
    /// <param name="path">A full path.</param>
    /// <param name="root">The full Teams root.</param>
    /// <returns><see langword="true"/> when removing the path would remove a guarded position.</returns>
    private static bool HoldsGuardedFile(string path, string root)
    {
        if (Inside(root, path))
        {
            return true;
        }

        string[] segments = SegmentsBelow(path, root);
        bool holder = segments.Length switch
        {
            1 => !TeamNames.IsReservedTeamName(segments[0]),
            2 => !TeamNames.IsReservedTeamName(segments[0]) && !TeamNames.IsReservedProjectName(segments[1]),
            _ => false,
        };
        return holder && Directory.Exists(path);
    }

    /// <summary>Whether <paramref name="path"/> is <paramref name="folder"/> or inside it, ignoring case.</summary>
    /// <param name="path">A full path.</param>
    /// <param name="folder">A full path to a folder.</param>
    /// <returns><see langword="true"/> when the path is the folder or inside it.</returns>
    private static bool Inside(string path, string folder) =>
        PathContainment.IsInside(path, folder, includeRoot: true, StringComparison.OrdinalIgnoreCase);

    /// <summary>The path's segments below <paramref name="root"/>; empty when it is not inside it.</summary>
    /// <param name="path">A full path.</param>
    /// <param name="root">A full path to a folder.</param>
    /// <returns>The segments, or an empty array.</returns>
    private static string[] SegmentsBelow(string path, string root) =>
        PathContainment.Below(path, root, StringComparison.OrdinalIgnoreCase)
            ?.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries)
        ?? [];
}
