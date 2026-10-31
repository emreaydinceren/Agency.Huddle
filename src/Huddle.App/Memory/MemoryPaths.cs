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
            return new MemoryFolder(Path.Combine(this.TeamsRoot(), team.Name, "memory"), team.Name, team);
        }

        string? project = team.Projects.FirstOrDefault(candidate => string.Equals(candidate, parts[1], StringComparison.OrdinalIgnoreCase));
        return project is null
            ? null
            : new MemoryFolder(Path.Combine(this.TeamsRoot(), team.Name, project, "memory"), $"{team.Name}/{project}", team);
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

        return MemoryFileRules.IsValidFileName(file) ?SafePath(folder, file) : null;
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
