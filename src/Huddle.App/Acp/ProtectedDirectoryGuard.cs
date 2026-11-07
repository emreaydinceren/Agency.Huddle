using Agency.Huddle.Acp.Abstractions;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// Refuses a tool call that names a path inside the Agent's own home configuration directory (<c>~/.claude</c>). Malformed or absent input
/// is not a match: this refuses what it can positively identify and approves everything else, because refusing whatever it cannot parse would
/// break ordinary work for every tool whose arguments it does not recognise. See <see cref="WorkDirPermissionHandler"/> for why the one
/// exception is drawn here and not around the Work Dir.
/// </summary>
/// <param name="protectedDirectory">The directory to refuse writes into, already absolute. Passed in so a test can point it somewhere harmless.</param>
/// <param name="workDir">The Agent's Work Dir, where a relative path in a tool call resolves; the process's current directory when <see langword="null"/>.</param>
internal sealed class ProtectedDirectoryGuard(string protectedDirectory, string? workDir = null) : IToolCallGuard
{
    /// <inheritdoc />
    public string? RefusalFor(ToolCallInfo toolCall, string personaName)
    {
        ArgumentNullException.ThrowIfNull(toolCall);

        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(protectedDirectory));
        foreach (string candidate in ToolCallPaths.From(toolCall, workDir ?? Directory.GetCurrentDirectory()))
        {
            // The separator matters: without it "…\.claudex" would count as inside "…\.claude".
            if (ToolCallPaths.IsInside(candidate, root))
            {
                return $"'{candidate}' is inside the agent's own configuration directory '{protectedDirectory}'.";
            }
        }

        return null;
    }
}
