using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Memory;

/// <summary>
/// Turns a <c>memory</c> call's <c>folder</c> and <c>file</c> arguments into a full path (Instructions spec §5.3). The one
/// implementation of that rule: the <c>memory</c> tool resolves with it before it acts, and a Room Session resolves with it
/// to record the file a memory call changed as the Agent's own for File Changes.
/// </summary>
/// <param name="agentPaths">Locates a Persona's Work Dir, and so its own memory folder.</param>
/// <param name="access">Decides which Team folders a Persona may resolve.</param>
/// <param name="options">Supplies the Teams root.</param>
internal sealed class MemoryPaths(AgentPaths agentPaths, TeamMemoryAccess access, IOptions<TeamOptions> options)
{
    /// <summary>The <c>folder</c> value naming the caller's own Memory, also what an absent <c>folder</c> means.</summary>
    public const string OwnFolder = "me";

    /// <summary>
    /// Resolves the <c>folder</c> argument to exactly one directory, or <see langword="null"/>. A Team and a Project are matched
    /// ignoring case against the catalog and the path is built from the catalog's spelling, never from the argument.
    /// </summary>
    /// <param name="personaName">The calling Persona's Name.</param>
    /// <param name="argument">The argument as passed.</param>
    /// <returns>The folder, or <see langword="null"/> when the Persona may not use it or it does not exist.</returns>
    public MemoryFolder? ResolveFolder(string personaName, string? argument)
    {
        string name = string.IsNullOrWhiteSpace(argument) ? OwnFolder : argument.Trim();
        if (string.Equals(name, OwnFolder, StringComparison.OrdinalIgnoreCase))
        {
            return new MemoryFolder(Path.Combine(agentPaths.WorkDir(personaName), "memory"), OwnFolder, null);
        }

        string[] parts = name.Split('/');
        if (parts.Length > 2 || Array.Exists(parts, static part => string.IsNullOrWhiteSpace(part)))
        {
            return null;
        }

        TeamSummary? team = access.Find(parts[0]);
        if (team is null || !team.HasFolder || !access.CanRead(personaName, team))
        {
            return null;
        }

        if (parts.Length == 1)
        {
            return this.Scope(team, null);
        }

        string? project = team.Projects.FirstOrDefault(candidate => string.Equals(candidate, parts[1], StringComparison.OrdinalIgnoreCase));
        return project is null ? null : this.Scope(team, project);
    }

    /// <summary>
    /// The full path of <paramref name="file"/> in the folder, or <see langword="null"/> unless it is a memory file name that sits
    /// directly inside it.
    /// </summary>
    /// <param name="folder">The resolved folder.</param>
    /// <param name="file">The <c>file</c> argument as passed.</param>
    /// <returns>The file's full path, or <see langword="null"/>.</returns>
    public static string? FilePath(MemoryFolder folder, string file)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(file);

        return MemoryFileRules.IsValidFileName(file) ? SafePath(folder, file) : null;
    }

    /// <summary>
    /// Resolves a folder label for Huddle itself, writing on behalf of <paramref name="team"/> (Team Memory Helper spec §6.6): only
    /// the Team's own folder or one of its Projects', matched ignoring case against the catalog. No Persona's access is consulted, so
    /// the label is the only thing that can widen the write, and it cannot: <c>me</c>, another Team, a path and <c>..</c> are refused.
    /// </summary>
    /// <param name="team">The Team the write is for, as the catalog spells it.</param>
    /// <param name="label">The folder label as given, <c>Team</c> or <c>Team/Project</c>.</param>
    /// <param name="folder">The folder when the label resolves.</param>
    /// <returns>Whether the label names one of the Team's memory folders.</returns>
    public bool TryResolveTeamFolder(string team, string? label, [NotNullWhen(true)] out MemoryFolder? folder)
    {
        ArgumentNullException.ThrowIfNull(team);

        folder = null;
        string[] parts = (label ?? string.Empty).Trim().Split('/');
        if (parts.Length > 2 || Array.Exists(parts, static part => string.IsNullOrWhiteSpace(part)) || !string.Equals(parts[0].Trim(), team, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (access.Find(team) is not { HasFolder: true } summary)
        {
            return false;
        }

        if (parts.Length == 1)
        {
            folder = this.Scope(summary, null);
            return folder is not null;
        }

        string? project = summary.Projects.FirstOrDefault(candidate => string.Equals(candidate, parts[1].Trim(), StringComparison.OrdinalIgnoreCase));
        folder = project is null ? null : this.Scope(summary, project);
        return folder is not null;
    }

    /// <summary>The folders Huddle may write for <paramref name="team"/>: the Team's own, then each Project's, in the catalog's order.</summary>
    /// <param name="team">The Team.</param>
    /// <returns>The folders; empty when the Team has no folder.</returns>
    public IReadOnlyList<MemoryFolder> TeamFolders(TeamSummary team)
    {
        ArgumentNullException.ThrowIfNull(team);
        if (!team.HasFolder)
        {
            return [];
        }

        IEnumerable<string?> projects = [null, .. team.Projects];
        return [.. projects.Select(project => this.Scope(team, project)).OfType<MemoryFolder>()];
    }

    /// <summary>Resolves a <c>folder</c> and <c>file</c> pair to the file's full path, as the <c>memory</c> tool would.</summary>
    /// <param name="personaName">The calling Persona's Name.</param>
    /// <param name="folderArgument">The <c>folder</c> argument, or <see langword="null"/> for the caller's own.</param>
    /// <param name="file">The <c>file</c> argument.</param>
    /// <param name="fullPath">The file's full path when the pair resolves.</param>
    /// <returns>Whether the pair names a file the Persona may use.</returns>
    public bool TryResolve(string personaName, string? folderArgument, string? file, [NotNullWhen(true)] out string? fullPath)
    {
        fullPath = file is not null && this.ResolveFolder(personaName, folderArgument) is { } folder
            ? FilePath(folder, file)
            : null;
        return fullPath is not null;
    }

    /// <summary>
    /// The memory folder of a Team, or of one of its Projects, built from the catalog's spelling; <see langword="null"/> when the Team
    /// folder, the Project folder or the memory folder is a junction or link. The Team Memory index skips such a folder
    /// (<see cref="TeamMemoryIndex.IsSkipped"/>), so the tool and the Helper must not read or write through it either.
    /// </summary>
    /// <param name="team">The Team.</param>
    /// <param name="project">The Project, or <see langword="null"/> for the Team-wide folder.</param>
    private MemoryFolder? Scope(TeamSummary team, string? project)
    {
        string teamFolder = Path.Combine(this.TeamsRoot(), team.Name);
        string scopeFolder = project is null ? teamFolder : Path.Combine(teamFolder, project);
        string memory = Path.Combine(scopeFolder, "memory");
        if (TeamMemoryIndex.IsSkipped(teamFolder) || TeamMemoryIndex.IsSkipped(scopeFolder) || TeamMemoryIndex.IsSkipped(memory))
        {
            return null;
        }

        return new MemoryFolder(memory, project is null ? team.Name : $"{team.Name}/{project}", team);
    }

    /// <summary>The Teams root folder.</summary>
    private string TeamsRoot() => Path.GetFullPath(Path.Combine(options.Value.DataDir, options.Value.Teams.Dir));

    /// <summary>
    /// The full path of <paramref name="file"/> in the folder, or <see langword="null"/> unless it sits directly inside it. A second
    /// check on top of the file-name rules, not the only one.
    /// </summary>
    /// <param name="folder">The resolved folder.</param>
    /// <param name="file">A file name that passed <see cref="MemoryFileRules.IsValidFileName"/>.</param>
    private static string? SafePath(MemoryFolder folder, string file)
    {
        string full = Path.GetFullPath(Path.Combine(folder.Path, file));
        string? parent = Path.GetDirectoryName(full);
        return parent is not null && string.Equals(Path.TrimEndingDirectorySeparator(parent), Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder.Path)), StringComparison.OrdinalIgnoreCase)
            ? full
            : null;
    }
}
