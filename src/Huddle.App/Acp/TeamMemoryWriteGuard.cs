using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Memory;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// Refuses an Agent's own tool call that edits, deletes or moves something inside <c>Teams/&lt;Team&gt;/memory/</c> or
/// <c>Teams/&lt;Team&gt;/&lt;Project&gt;/memory/</c> unless the Persona is that Team's manager or the Chief of Staff
/// (Instructions spec §5.6). The <c>memory</c> tool enforces the same rule; this keeps a Team's Memory to one writer
/// for an Adapter whose file tools could reach it directly.
/// </summary>
/// <remarks>
/// A read is never refused, and a folder that is not a Team in the catalog, or a sub-folder that is not one of the Team's
/// Projects, is not Team Memory and is left alone. Like the other guards it sees path arguments only, so a shell redirect
/// gets past it. A write through a Windows 8.3 short name or an alternate data stream is refused outright (see
/// <see cref="ToolCallPaths.UsesAlias"/>), but a junction or symbolic link into a Memory folder is not resolved and gets past it:
/// a soft boundary, not a jail.
/// </remarks>
/// <param name="teamsRoot">The Teams root folder.</param>
/// <param name="workDir">The Agent's Work Dir, where a relative path resolves.</param>
/// <param name="access">Who manages and who belongs to which Team.</param>
/// <param name="prompts">Renders the refusal reasons (<c>systemPrompt.memoryManagerOnly</c> and <c>systemPrompt.memoryNoManager</c>).</param>
internal sealed class TeamMemoryWriteGuard(string teamsRoot, string workDir, TeamMemoryAccess access, IPromptSource prompts)
{
    private const string MemoryFolder = "memory";

    /// <summary>Decides whether the tool call must be refused.</summary>
    /// <param name="toolCall">The call the Agent is asking permission for.</param>
    /// <param name="personaName">The Persona that made it.</param>
    /// <returns>The refusal reason, or <see langword="null"/> when the call is not a write to Team Memory the Persona may not make.</returns>
    public string? RefusalFor(ToolCallInfo toolCall, string personaName)
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

            if (this.TeamMemoryOwner(path) is { } team && !access.CanWrite(personaName, team))
            {
                return Reason(team);
            }
        }

        return null;
    }

    /// <summary>The Team whose Memory folder contains (or is) <paramref name="fullPath"/>, or <see langword="null"/> when it is not in any.</summary>
    /// <param name="fullPath">A full path.</param>
    private TeamSummary? TeamMemoryOwner(string fullPath)
    {
        string relative = Path.GetRelativePath(Path.GetFullPath(teamsRoot), fullPath);
        if (Path.IsPathRooted(relative) || IsOutside(relative))
        {
            return null;
        }

        string[] segments = relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2 || access.Find(segments[0]) is not { } team)
        {
            return null;
        }

        bool teamWide = string.Equals(segments[1], MemoryFolder, StringComparison.OrdinalIgnoreCase);
        bool project = segments.Length >= 3
            && string.Equals(segments[2], MemoryFolder, StringComparison.OrdinalIgnoreCase)
            && team.Projects.Contains(segments[1], StringComparer.OrdinalIgnoreCase);

        return teamWide || project ? team : null;
    }

    /// <summary>Whether a relative path climbs out of the Teams root: it is ".." or starts with ".." and a separator, so a folder named "..x" is not outside.</summary>
    /// <param name="relative">The path relative to the Teams root.</param>
    private static bool IsOutside(string relative) =>
        string.Equals(relative, "..", StringComparison.Ordinal)
        || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
        || relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal);

    /// <summary>The reason a write to the Team's Memory is refused: only its manager writes it, or it has no manager.</summary>
    /// <param name="team">The Team.</param>
    private string Reason(TeamSummary team) =>
        team.Manager is { } manager
            ? prompts.Render(
                "systemPrompt.memoryManagerOnly",
                new Dictionary<string, string> { ["{{team}}"] = team.Name, ["{{manager}}"] = manager })
            : prompts.Render("systemPrompt.memoryNoManager", new Dictionary<string, string> { ["{{team}}"] = team.Name });
}
