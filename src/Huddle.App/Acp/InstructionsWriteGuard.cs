using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// Refuses an Agent's tool call that edits, deletes or moves an <c>Instructions.md</c> at the Teams root, in a Team folder or
/// in the Agent's own Work Dir (Instructions spec §3.3). Instructions are the Human's file.
/// </summary>
/// <remarks>
/// A read is never refused: the guard looks at the tool call's kind, unlike the <c>~/.claude</c> guard beside it, which
/// ignores it. Like that guard it sees path arguments only, so a shell redirect gets past it. A write through a Windows 8.3 short name or an
/// alternate data stream is refused outright (see <see cref="ToolCallPaths.UsesAlias"/>), but a junction or symbolic link that points into a
/// guarded folder is not resolved and gets past it. That boundary is soft and documented, not a jail.
/// </remarks>
/// <param name="teamsRoot">The Teams root folder.</param>
/// <param name="workDir">The Agent's own Work Dir, which is also where a relative path resolves.</param>
/// <param name="prompts">Renders the refusal reason (<c>systemPrompt.instructionsRefusal</c>).</param>
internal sealed class InstructionsWriteGuard(string teamsRoot, string workDir, IPromptSource prompts)
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

        foreach (string path in ToolCallPaths.From(toolCall, workDir))
        {
            if (ToolCallPaths.UsesAlias(path))
            {
                return prompts.Render("systemPrompt.pathAliasRefusal", new Dictionary<string, string> { ["{{path}}"] = path });
            }

            if (this.IsInstructionsFile(path))
            {
                return prompts.Render("systemPrompt.instructionsRefusal", new Dictionary<string, string> { ["{{path}}"] = path });
            }
        }

        return null;
    }

    /// <summary>Whether <paramref name="fullPath"/> is an <c>Instructions.md</c> directly inside one of the three places.</summary>
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
            || (Path.GetDirectoryName(folder) is { } parent && ToolCallPaths.Same(parent, root));
    }
}
