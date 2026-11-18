using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// Refuses an Agent's tool call that edits, writes, deletes or moves a Team's Playbook file, <c>Teams/&lt;Team&gt;/_member-playbook.md</c> or
/// <c>_manager-playbook.md</c> (Team Playbooks spec §7.6). Those files are the Human's; an Agent proposes a change in a retrospective.
/// </summary>
/// <remarks>
/// A read is never refused. Like <see cref="AgentsMdGuard"/> it sees path arguments only, so a shell command gets past it: that boundary is soft and
/// documented. The guarded position is exactly depth 2 below the Teams root, under a Team that is not a reserved name. Paths are compared
/// ignoring case. A write through a Windows 8.3 short name or an alternate data stream of a guarded file is refused outright.
/// </remarks>
/// <param name="teamsRoot">The Teams root folder.</param>
/// <param name="workDir">The Agent's own Work Dir, where a relative path resolves.</param>
/// <param name="prompts">Renders the refusal reason (<c>systemPrompt.teamFileRefusal</c>).</param>
internal sealed class HumanTeamFilesGuard(string teamsRoot, string workDir, IPromptSource prompts) : IToolCallGuard
{
    /// <summary>The number of segments below the Teams root at which a Playbook file is guarded: the Team folder, then the file.</summary>
    private const int GuardedDepth = 2;

    private static readonly string[] GuardedNames = [PlaybookFile.FileName(manager: false), PlaybookFile.FileName(manager: true)];

    /// <inheritdoc />
    string? IToolCallGuard.RefusalFor(ToolCallInfo toolCall, string personaName) => this.RefusalFor(toolCall);

    /// <summary>Decides whether the tool call must be refused.</summary>
    /// <param name="toolCall">The call the Agent is asking permission for.</param>
    /// <returns>The refusal reason, or <see langword="null"/> when the call does not change a Playbook file.</returns>
    public string? RefusalFor(ToolCallInfo toolCall)
    {
        ArgumentNullException.ThrowIfNull(toolCall);

        if (!ToolCallPaths.ChangesFiles(toolCall.Kind))
        {
            return null;
        }

        string root = Path.GetFullPath(teamsRoot.Replace('\\', '/'));
        foreach (string resolved in ToolCallPaths.From(toolCall, workDir))
        {
            string path = Path.GetFullPath(resolved.Replace('\\', '/'));
            string[] segments = SegmentsBelow(path, root);
            if (segments.Length != GuardedDepth || TeamNames.IsReservedTeamName(segments[0]))
            {
                continue;
            }

            if (UsesAlias(root, segments))
            {
                return prompts.Render("systemPrompt.pathAliasRefusal", new Dictionary<string, string> { ["{{path}}"] = path });
            }

            if (GuardedNames.Contains(segments[1], StringComparer.OrdinalIgnoreCase))
            {
                return prompts.Render("systemPrompt.teamFileRefusal");
            }
        }

        return null;
    }

    /// <summary>
    /// Whether the path names a guarded file through an alias: a stream name or short name on the file, or a short name for the Team folder.
    /// Other names are left alone, so a short name outside the guarded ones is an ordinary path.
    /// </summary>
    /// <param name="root">The full Teams root.</param>
    /// <param name="segments">The two segments below the root.</param>
    /// <returns><see langword="true"/> when the path is an alias to refuse.</returns>
    private static bool UsesAlias(string root, string[] segments)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        string last = segments[1];
        int stream = last.IndexOf(':', StringComparison.Ordinal);
        string name = stream < 0 ? last : last[..stream];
        bool shortName = GuardedNames.Any(guarded => IsShortNameOf(name, guarded));
        bool guardedName = shortName || GuardedNames.Contains(name, StringComparer.OrdinalIgnoreCase);
        if (!guardedName)
        {
            return false;
        }

        return stream >= 0 || shortName || ToolCallPaths.UsesAliasInside(Path.Combine(root, segments[0]), root, 1);
    }

    /// <summary>
    /// Whether a name has the shape of a short name of a guarded file: some characters that start the long name, then <c>~</c> and digits, with
    /// an extension that starts the long one. The length of the stem is not judged, so <c>_MANAGE~1.MD</c> counts as well as <c>_MANAG~1.MD</c>.
    /// </summary>
    /// <param name="name">A file name, without any stream.</param>
    /// <param name="longName">A guarded file name.</param>
    /// <returns><see langword="true"/> when the name reads as a short name of the file.</returns>
    private static bool IsShortNameOf(string name, string longName)
    {
        string stem = Path.GetFileNameWithoutExtension(name);
        int tilde = stem.LastIndexOf('~');
        return tilde > 0
            && tilde < stem.Length - 1
            && stem[(tilde + 1)..].All(char.IsAsciiDigit)
            && Path.GetFileNameWithoutExtension(longName).StartsWith(stem[..tilde], StringComparison.OrdinalIgnoreCase)
            && Path.GetExtension(longName).StartsWith(Path.GetExtension(name), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The path's segments below <paramref name="root"/>; empty when it is not inside it.</summary>
    /// <param name="path">A full path.</param>
    /// <param name="root">A full path to a folder.</param>
    /// <returns>The segments, or an empty array.</returns>
    private static string[] SegmentsBelow(string path, string root) =>
        PathContainment.Below(path, root, StringComparison.OrdinalIgnoreCase)
            ?.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries)
        ?? [];
}
