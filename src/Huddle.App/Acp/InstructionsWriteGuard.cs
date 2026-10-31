using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// Refuses an Agent's tool call that edits, deletes or moves an <c>Instructions.md</c> at the Teams root, in a Team folder or
/// in any Agent's Work Dir (Instructions spec §3.3), or that deletes or moves a folder that holds one. Instructions are the Human's file,
/// and another Agent's Work Dir <c>Instructions.md</c> is that Agent's highest-precedence level.
/// </summary>
/// <remarks>
/// A read is never refused: the guard looks at the tool call's kind, unlike the <c>~/.claude</c> guard beside it, which
/// ignores it. Like that guard it sees path arguments only, so a shell redirect gets past it. Both the source and the destination of a
/// move are read. A write through a Windows 8.3 short name or an alternate data stream <em>inside a guarded place</em> is refused outright
/// (see <see cref="ToolCallPaths.UsesAliasInside"/>); the same spelling elsewhere is not the guard's business. A short name in the part of the path that
/// leads to the guarded place, a junction or a symbolic link that points into a guarded folder is not resolved and gets past it.
/// That boundary is soft and documented, not a jail.
/// </remarks>
/// <param name="teamsRoot">The Teams root folder.</param>
/// <param name="workDir">The Agent's own Work Dir, which is also where a relative path resolves.</param>
/// <param name="prompts">Renders the refusal reason (<c>systemPrompt.instructionsRefusal</c>).</param>
/// <param name="agentPaths">Locates every Agent's Work Dir, so the Agents' <c>Instructions.md</c> files are guarded too; <see langword="null"/> guards the Agent's own Work Dir only.</param>
internal sealed class InstructionsWriteGuard(string teamsRoot, string workDir, IPromptSource prompts, AgentPaths? agentPaths = null)
{
    /// <summary>Decides whether the tool call must be refused.</summary>
    /// <param name="toolCall">The call the Agent is asking permission for.</param>
    /// <returns>The refusal reason, or <see langword="null"/> when the call is not a write to an Instructions file.</returns>
    public string? RefusalFor(ToolCallInfo toolCall)
    {
        ArgumentNullException.ThrowIfNull(toolCall);

        if (!ToolCallPaths.ChangesFiles(toolCall.Kind))
        {
            return null;
        }

        bool removes = toolCall.Kind is ToolKind.Delete or ToolKind.Move;
        foreach (string path in ToolCallPaths.From(toolCall, workDir))
        {
            if (this.GuardedPlace(path, removes) is var (root, depth, shortNameOf)
                && ToolCallPaths.UsesAliasInside(path, root, depth, shortNameOf))
            {
                return prompts.Render("systemPrompt.pathAliasRefusal", new Dictionary<string, string> { ["{{path}}"] = path });
            }

            if (this.IsInstructionsFile(path) || (removes && this.HoldsInstructions(path)))
            {
                return prompts.Render("systemPrompt.instructionsRefusal", new Dictionary<string, string> { ["{{path}}"] = path });
            }
        }

        return null;
    }

    /// <summary>
    /// The guarded place a path is inside, whose alias spellings are refused: the Teams root (an <c>Instructions.md</c> is at most two segments below it) or the Agent's own
    /// or another Agent's Work Dir (one segment below).
    /// </summary>
    /// <param name="fullPath">A full path.</param>
    /// <param name="removes">Whether the call deletes or moves, so a Team folder's short name counts as well as the file's.</param>
    /// <returns>
    /// The guarded folder, how deep the guarded files are in it, and the file name a last segment must be a short name of (none when any short name counts),
    /// or <see langword="null"/> when the path is in none.
    /// </returns>
    private (string Root, int Depth, string? ShortNameOf)? GuardedPlace(string fullPath, bool removes)
    {
        string root = Path.GetFullPath(teamsRoot);
        if (ToolCallPaths.IsInside(fullPath, root))
        {
            return (root, 2, removes ? null : TeamPaths.InstructionsFileName);
        }

        string own = Path.GetFullPath(workDir);
        if (ToolCallPaths.IsInside(fullPath, own))
        {
            return (own, 1, TeamPaths.InstructionsFileName);
        }

        return this.AgentWorkDirContaining(fullPath) is { } agentWorkDir ? (agentWorkDir, 1, TeamPaths.InstructionsFileName) : null;
    }

    /// <summary>The Work Dir of any Agent that contains <paramref name="fullPath"/>, under the Agents definitions root.</summary>
    /// <param name="fullPath">A full path.</param>
    /// <returns>The Work Dir, or <see langword="null"/> when the path is in none.</returns>
    private string? AgentWorkDirContaining(string fullPath)
    {
        if (agentPaths is null)
        {
            return null;
        }

        string[] below = SegmentsBelow(Path.GetFullPath(agentPaths.DefinitionsRoot), fullPath);
        string[] workSegments = WorkDirSegments(agentPaths);
        if (below.Length < 1 + workSegments.Length
            || !below.Skip(1).Take(workSegments.Length).SequenceEqual(workSegments, StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        return Path.Combine([Path.GetFullPath(agentPaths.DefinitionsRoot), .. below.Take(1 + workSegments.Length)]);
    }

    /// <summary>Whether <paramref name="fullPath"/> is an <c>Instructions.md</c> directly inside the Teams root, a Team folder or an Agent's Work Dir.</summary>
    /// <param name="fullPath">A full path.</param>
    private bool IsInstructionsFile(string fullPath)
    {
        if (!string.Equals(Path.GetFileName(fullPath), TeamPaths.InstructionsFileName, StringComparison.OrdinalIgnoreCase)
            || Path.GetDirectoryName(fullPath) is not { } folder)
        {
            return false;
        }

        string root = Path.GetFullPath(teamsRoot);
        return ToolCallPaths.Same(folder, root)
            || ToolCallPaths.Same(folder, Path.GetFullPath(workDir))
            || (Path.GetDirectoryName(folder) is { } parent && ToolCallPaths.Same(parent, root))
            || (this.AgentWorkDirContaining(folder) is { } agentWorkDir && ToolCallPaths.Same(agentWorkDir, folder));
    }

    /// <summary>
    /// Whether deleting or moving <paramref name="fullPath"/> would take an <c>Instructions.md</c> with it: it is the Teams root or a folder above it,
    /// an existing folder directly under it (a Team folder), the Agent's own Work Dir or a folder above it, or an Agent's Work Dir
    /// or a folder above one.
    /// </summary>
    /// <param name="fullPath">A full path.</param>
    private bool HoldsInstructions(string fullPath)
    {
        string root = Path.GetFullPath(teamsRoot);
        return ToolCallPaths.IsAtOrAbove(fullPath, root)
            || ToolCallPaths.IsAtOrAbove(fullPath, Path.GetFullPath(workDir))
            || (Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(fullPath)) is { } parent
                && ToolCallPaths.Same(parent, root)
                && Directory.Exists(fullPath))
            || this.HoldsAgentWorkDir(fullPath);
    }

    /// <summary>Whether <paramref name="fullPath"/> is the Agents definitions root or above it, an Agent's folder, or the way down to an Agent's Work Dir.</summary>
    /// <param name="fullPath">A full path.</param>
    private bool HoldsAgentWorkDir(string fullPath)
    {
        if (agentPaths is null)
        {
            return false;
        }

        string agentsRoot = Path.GetFullPath(agentPaths.DefinitionsRoot);
        if (ToolCallPaths.IsAtOrAbove(fullPath, agentsRoot))
        {
            return true;
        }

        string[] below = SegmentsBelow(agentsRoot, fullPath);
        string[] workSegments = WorkDirSegments(agentPaths);
        return below.Length >= 1
            && below.Length <= 1 + workSegments.Length
            && below.Skip(1).SequenceEqual(workSegments.Take(below.Length - 1), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The path's segments below <paramref name="root"/>; empty when it is not inside it.</summary>
    /// <param name="root">A full path to a folder.</param>
    /// <param name="fullPath">A full path.</param>
    private static string[] SegmentsBelow(string root, string fullPath) =>
        ToolCallPaths.IsInside(fullPath, root)
            ? Path.GetRelativePath(root, fullPath).Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries)
            : [];

    /// <summary>The Work Dir's segments below an Agent's folder: <c>work</c> by default, whatever the <c>WorkDir</c> option says.</summary>
    /// <param name="paths">Where the Agents live.</param>
    private static string[] WorkDirSegments(AgentPaths paths) =>
        Path.GetRelativePath(paths.AgentFolder("x"), paths.WorkDir("x"))
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
}
