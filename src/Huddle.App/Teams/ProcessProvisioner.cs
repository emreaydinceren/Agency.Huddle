using System.Text;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.TeamTypes;

namespace Agency.Huddle.App.Teams;

/// <summary>How a <see cref="ProcessProvisioner"/> call ended.</summary>
internal enum ProvisionResult
{
    /// <summary><c>Process.md</c> was written and <c>_team.md</c> names it.</summary>
    Created,

    /// <summary>The Team already has a <c>Process.md</c>; nothing was changed.</summary>
    AlreadyExists,

    /// <summary>The Team has no readable <c>_team.md</c>, or its team type is unknown; nothing was written.</summary>
    UnknownTeam,

    /// <summary>The team type offers no process with that id, or its file is unreadable; nothing was written.</summary>
    UnknownProcess,

    /// <summary>The file could not be written; nothing was changed.</summary>
    Failed,
}

/// <summary>
/// Copies a process template into the Team folder as <c>Process.md</c> (PR-3, Spec §Process). The
/// template text is copied verbatim, so the template id and version in its frontmatter are recorded
/// with it. An existing <c>Process.md</c> is never overwritten.
/// </summary>
/// <param name="types">Supplies the process templates of the Team's type.</param>
/// <param name="folders">Locates the Team folder and its <c>_team.md</c>.</param>
internal sealed class ProcessProvisioner(TeamTypeCatalog types, ITeamFolders folders)
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private const string BlankText = """
        ---
        title: 'Process'
        gates: []
        ---
        # Process

        ## When it applies

        ## Stages

        ## Who does what

        ## If a role is not hired

        ## Gates

        ## Cadence

        ## Escalation

        """;

    /// <summary>Copies a process template of the Team's type into <c>Teams/&lt;Team&gt;/Process.md</c>.</summary>
    /// <param name="team">The Team's name.</param>
    /// <param name="processId">The id of the process template.</param>
    /// <returns>How the call ended.</returns>
    public ProvisionResult Provision(string team, string processId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(team);
        ArgumentNullException.ThrowIfNull(processId);

        TeamFile? file = folders.ReadTeamFile(team);
        if (file is null)
        {
            return ProvisionResult.UnknownTeam;
        }

        string? existing = ProcessPath(team);
        if (existing is not null && File.Exists(existing))
        {
            return ProvisionResult.AlreadyExists;
        }

        if (types.Find(file.TeamType.Id) is null)
        {
            return ProvisionResult.UnknownTeam;
        }

        string? text = types.ReadProcess(file.TeamType.Id, processId);
        return text is null ? ProvisionResult.UnknownProcess : this.Write(team, text);
    }

    /// <summary>Writes the blank PR-6 outline, with empty sections and no gates, as <c>Process.md</c>.</summary>
    /// <param name="team">The Team's name.</param>
    /// <returns>How the call ended.</returns>
    public ProvisionResult Blank(string team)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(team);

        if (folders.ReadTeamFile(team) is null)
        {
            return ProvisionResult.UnknownTeam;
        }

        return this.Write(team, BlankText.ReplaceLineEndings("\n"));
    }

    /// <summary>Provisions the team type's default process when the Team has no <c>Process.md</c> yet.</summary>
    /// <param name="team">The Team's name.</param>
    /// <returns>How the call ended; <see cref="ProvisionResult.UnknownProcess"/> when the type offers no process.</returns>
    public ProvisionResult ProvisionDefault(string team)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(team);

        TeamFile? file = folders.ReadTeamFile(team);
        if (file is null)
        {
            return ProvisionResult.UnknownTeam;
        }

        IReadOnlyList<ProcessRef> processes = types.Find(file.TeamType.Id)?.Processes ?? [];
        ProcessRef? chosen = processes.FirstOrDefault(static p => p.Default) ?? (processes.Count > 0 ? processes[0] : null);
        return chosen is null ? ProvisionResult.UnknownProcess : this.Provision(team, chosen.Id);
    }

    private ProvisionResult Write(string team, string text)
    {
        string? path = ProcessPath(team);
        if (path is null)
        {
            return ProvisionResult.UnknownTeam;
        }

        string temp = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temp, text, Utf8);
            File.Move(temp, path);
        }
        catch (IOException) when (File.Exists(path))
        {
            return ProvisionResult.AlreadyExists;
        }
        catch (IOException)
        {
            return ProvisionResult.Failed;
        }
        catch (UnauthorizedAccessException)
        {
            return ProvisionResult.Failed;
        }
        finally
        {
            File.Delete(temp);
        }

        _ = folders.UpdateTeamFile(team, static current => current with { Process = TeamPaths.ProcessFileName });
        return ProvisionResult.Created;
    }

    private string? ProcessPath(string team)
    {
        LibraryResult<LibraryPath> folder = folders.EnsureTeam(team);
        return folder.Value is null ? null : Path.Combine(folder.Value.FullPath, TeamPaths.ProcessFileName);
    }
}
