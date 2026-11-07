using Agency.Huddle.Acp.Abstractions;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// Refuses an Agent's tool call that edits, deletes or moves the files its own merge gate lives in (issue #432): the Work Dir's
/// <c>.claude/settings.local.json</c> (the file Huddle writes its hooks into, read by the Adapter on the next tool call, so an Agent that
/// rewrites it can drop the hook), the hook script <c>merge-gate-ask.js</c> wherever it is named, and anything in Huddle's own hooks folder.
/// </summary>
/// <remarks>
/// Deliberately narrow. A project's own <c>.claude/settings.json</c>, <c>.claude/hooks/*</c> and <c>.mcp.json</c> are not guarded: a development
/// Team's Work Dir is its repository, which may commit those files, and its Agent may edit them legitimately; they are also not where Huddle
/// writes, and a project <c>settings.json</c> allow rule did not skip the handler when measured. Like the other guards it sees path arguments
/// only, so a shell command (<c>printf … &gt; .claude/settings.local.json</c>) gets past it: that boundary is soft and documented in known-limits.md.
/// Paths are compared ignoring case. A read is never refused.
/// </remarks>
/// <param name="workDir">The Agent's own Work Dir, where a relative path resolves.</param>
/// <param name="hooksDir">Huddle's hooks folder (<c>{DataDir}/hooks</c>), or <see langword="null"/> when there is none to guard.</param>
internal sealed class AgentSettingsGuard(string workDir, string? hooksDir = null) : IToolCallGuard
{
    /// <inheritdoc />
    string? IToolCallGuard.RefusalFor(ToolCallInfo toolCall, string personaName) => this.RefusalFor(toolCall);

    private const string SettingsFolder = ".claude";

    /// <summary>The refusal text. It is logged only, because the permission reply carries no reason.</summary>
    private const string Reason = "Huddle owns this Agent's local settings and merge gate hook; an Agent does not edit them.";

    /// <summary>Decides whether the tool call must be refused.</summary>
    /// <param name="toolCall">The call the Agent is asking permission for.</param>
    /// <returns>The refusal reason, or <see langword="null"/> when the call touches none of the guarded files.</returns>
    public string? RefusalFor(ToolCallInfo toolCall)
    {
        ArgumentNullException.ThrowIfNull(toolCall);

        if (!ToolCallPaths.ChangesFiles(toolCall.Kind))
        {
            return null;
        }

        bool removes = toolCall.Kind is ToolKind.Delete or ToolKind.Move;
        string settingsFile = Path.GetFullPath(Path.Combine(workDir, SettingsFolder, DocHistory.DocHistoryStopHook.SettingsFile));
        foreach (string resolved in ToolCallPaths.From(toolCall, workDir))
        {
            string path = Path.GetFullPath(resolved.Replace('\\', '/'));
            if (ToolCallPaths.Same(path, settingsFile)
                || string.Equals(Path.GetFileName(path), MergeGateHook.ScriptName, StringComparison.OrdinalIgnoreCase)
                || (hooksDir is not null && ToolCallPaths.IsInside(path, Path.GetFullPath(hooksDir)))
                || (removes && ToolCallPaths.IsAtOrAbove(path, settingsFile)))
            {
                return Reason;
            }
        }

        return null;
    }
}
