using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Memory;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// Refuses an Agent's own tool call that edits, deletes or moves something inside <c>Teams/&lt;Team&gt;/memory/</c> or
/// <c>Teams/&lt;Team&gt;/&lt;Project&gt;/memory/</c>, or that deletes or moves a folder holding one (the Teams root, the Team folder, a Project folder),
/// unless the Persona is that Team's manager or the Chief of Staff (Instructions spec §5.6). The <c>memory</c> tool enforces the same rule;
/// this keeps a Team's Memory to one writer for an Adapter whose file tools could reach it directly.
/// </summary>
/// <remarks>
/// A read is never refused, and a folder that is not a Team in the catalog, or a sub-folder that is not one of the Team's
/// Projects, is not Team Memory and is left alone. Like the other guards it sees path arguments only, so a shell redirect
/// gets past it; both the source and the destination of a move are read. A write through a Windows 8.3 short name or an alternate data stream
/// <em>inside the Teams root</em> is refused outright (see <see cref="ToolCallPaths.UsesAliasInside"/>); the same spelling elsewhere is left alone.
/// A short name in the part of the path that leads to the Teams root, a junction or a symbolic link into a Memory folder is not resolved and gets past it:
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

        bool removes = toolCall.Kind is ToolKind.Delete or ToolKind.Move;
        string root = Path.GetFullPath(teamsRoot);
        Func<TeamSummary, bool>? canWrite = null;
        bool? chief = null;
        foreach (string path in ToolCallPaths.From(toolCall, workDir))
        {
            if (ToolCallPaths.UsesAliasInside(path, root))
            {
                return prompts.Render("systemPrompt.pathAliasRefusal", new Dictionary<string, string> { ["{{path}}"] = path });
            }

            chief ??= access.IsChiefOfStaff(personaName);
            if (chief.Value)
            {
                continue;
            }

            if (IsHuddleMemory(path, root, removes))
            {
                return prompts.Render("systemPrompt.huddleMemoryRefusal", new Dictionary<string, string>());
            }

            foreach (TeamSummary team in this.TeamsAffected(path, root, removes))
            {
                canWrite ??= access.WriterFor(personaName);
                if (!canWrite(team))
                {
                    return this.Reason(team);
                }
            }
        }

        return null;
    }

    /// <summary>Whether the write reaches Huddle-wide Memory: the path is inside <c>Teams/memory/</c>, or for a delete or move is that folder or one above it.</summary>
    /// <param name="fullPath">A full path.</param>
    /// <param name="root">The full path of the Teams root.</param>
    /// <param name="removes">Whether the call deletes or moves.</param>
    private static bool IsHuddleMemory(string fullPath, string root, bool removes)
    {
        string folder = Path.Combine(root, MemoryFolder);
        if (Inside(fullPath, folder))
        {
            return true;
        }

        return removes && Inside(folder, fullPath);
    }

    /// <summary>
    /// Whether <paramref name="path"/> is <paramref name="folder"/> or inside it, ignoring case on every OS: this guard refuses, so
    /// <c>Teams/MEMORY/x.md</c> on a case-sensitive file system must not slip past it (it did on Linux CI).
    /// </summary>
    /// <param name="path">A full path.</param>
    /// <param name="folder">A full path to a folder.</param>
    private static bool Inside(string path, string folder) =>
        PathContainment.IsInside(path, folder, includeRoot: true, StringComparison.OrdinalIgnoreCase);

    /// <summary>The Teams whose Memory the write reaches: the one whose Memory folder contains (or is) the path and, for a delete or move, every Team whose Memory folder is inside the path.</summary>
    /// <param name="fullPath">A full path.</param>
    /// <param name="root">The full path of the Teams root.</param>
    /// <param name="removes">Whether the call deletes or moves, so a folder above a Memory folder counts.</param>
    private List<TeamSummary> TeamsAffected(string fullPath, string root, bool removes)
    {
        List<TeamSummary> affected = [];
        if (this.TeamMemoryOwner(fullPath, root) is { } owner)
        {
            affected.Add(owner);
        }

        if (removes)
        {
            affected.AddRange(access.AllTeams.Where(team => MemoryFolders(team, root).Any(folder => Inside(folder, fullPath))));
        }

        return affected;
    }

    /// <summary>The full paths of a Team's Memory folder and of each of its Projects' Memory folders.</summary>
    /// <param name="team">The Team.</param>
    /// <param name="root">The full path of the Teams root.</param>
    private static IEnumerable<string> MemoryFolders(TeamSummary team, string root)
    {
        string teamFolder = Path.Combine(root, team.Name);
        yield return Path.Combine(teamFolder, MemoryFolder);
        foreach (string project in team.Projects)
        {
            yield return Path.Combine(teamFolder, project, MemoryFolder);
        }
    }

    /// <summary>The Team whose Memory folder contains (or is) <paramref name="fullPath"/>, or <see langword="null"/> when it is not in any.</summary>
    /// <param name="fullPath">A full path.</param>
    /// <param name="root">The full path of the Teams root.</param>
    private TeamSummary? TeamMemoryOwner(string fullPath, string root)
    {
        if (!Inside(fullPath, root))
        {
            return null;
        }

        string[] segments = Path.GetRelativePath(root, fullPath).Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
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

    /// <summary>The reason a write to the Team's Memory is refused: only its manager writes it, or it has no manager.</summary>
    /// <param name="team">The Team.</param>
    private string Reason(TeamSummary team) => TeamMemoryRefusals.ManagerOnly(prompts, team);
}
