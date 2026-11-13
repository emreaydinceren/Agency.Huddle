using Agency.Huddle.App.Data;

namespace Agency.Huddle.App.Teams;

/// <summary>The live name checks and the mid-Turn read the rename UI asks of the service.</summary>
internal sealed partial class TeamRenameService : ITeamRenamer
{
    /// <inheritdoc />
    public string? CheckTeamName(string oldName, string newName)
    {
        ArgumentNullException.ThrowIfNull(oldName);
        ArgumentNullException.ThrowIfNull(newName);

        TeamSummary? summary = catalog.Find(oldName);
        return summary is null ? $"There is no Team named \"{oldName}\"." : this.Validate(summary.Name, newName);
    }

    /// <inheritdoc />
    public string? CheckProjectName(string team, string oldProject, string newProject)
    {
        ArgumentNullException.ThrowIfNull(team);
        ArgumentNullException.ThrowIfNull(oldProject);
        ArgumentNullException.ThrowIfNull(newProject);

        TeamSummary? summary = catalog.Find(team);
        if (summary is null)
        {
            return $"There is no Team named \"{team}\".";
        }

        string? current = summary.Projects.FirstOrDefault(p => string.Equals(p, oldProject, StringComparison.OrdinalIgnoreCase));
        return current is null ? $"{summary.Name} has no Project named \"{oldProject}\"." : this.ValidateProject(summary, current, newProject);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> MidTurnMembersAsync(string team, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(team);

        if (turnActivity is null || catalog.Find(team) is not { } summary)
        {
            return [];
        }

        List<string> busy = [];
        foreach (string member in summary.Members)
        {
            User? user = await directory.FindUserByNameAsync(member, ct).ConfigureAwait(false);
            if (user is not null && turnActivity.IsBusy(user.Id))
            {
                busy.Add(member);
            }
        }

        return busy;
    }
}
