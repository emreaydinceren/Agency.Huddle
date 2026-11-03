using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Prompts;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// Refuses an Agent's tool call that edits, deletes or moves anything in the Prompts folder, or deletes or moves that folder or one above it
/// (Spec §6.6, SB-R09). The files there are Huddle's prompts and only the Human changes them.
/// </summary>
/// <remarks>
/// A read is never refused. Like <see cref="RawGuard"/> it sees path arguments only, so a shell command gets past it: that boundary is soft and
/// documented. Paths are compared ignoring case, so the result is the same on Linux. A path inside the folder is refused whatever its spelling,
/// so a Windows 8.3 short name or a stream name on a prompt file needs no separate rule; one in the part of the path that leads to the folder is not detected.
/// </remarks>
/// <param name="promptsRoot">The Prompts folder.</param>
/// <param name="workDir">The Agent's own Work Dir, where a relative path resolves.</param>
/// <param name="prompts">Renders the refusal reason (<c>systemPrompt.promptsRefusal</c>).</param>
internal sealed class PromptsRootGuard(string promptsRoot, string workDir, IPromptSource prompts)
{
    /// <summary>Decides whether the tool call must be refused.</summary>
    /// <param name="toolCall">The call the Agent is asking permission for.</param>
    /// <returns>The refusal reason, or <see langword="null"/> when the call does not write to or remove the Prompts folder.</returns>
    public string? RefusalFor(ToolCallInfo toolCall)
    {
        ArgumentNullException.ThrowIfNull(toolCall);

        if (!ToolCallPaths.ChangesFiles(toolCall.Kind))
        {
            return null;
        }

        bool removes = toolCall.Kind is ToolKind.Delete or ToolKind.Move;
        string root = Path.GetFullPath(promptsRoot.Replace('\\', '/'));
        foreach (string resolved in ToolCallPaths.From(toolCall, workDir))
        {
            string path = Path.GetFullPath(resolved.Replace('\\', '/'));
            if (Inside(path, root) || (removes && Inside(root, path)))
            {
                return prompts.Render("systemPrompt.promptsRefusal");
            }
        }

        return null;
    }

    /// <summary>Whether <paramref name="path"/> is <paramref name="folder"/> or inside it, ignoring case.</summary>
    /// <param name="path">A full path.</param>
    /// <param name="folder">A full path to a folder.</param>
    /// <returns><see langword="true"/> when the path is the folder or inside it.</returns>
    private static bool Inside(string path, string folder) =>
        PathContainment.IsInside(path, folder, includeRoot: true, StringComparison.OrdinalIgnoreCase);
}
