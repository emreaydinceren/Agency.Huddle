using Agency.Huddle.App.Data;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// Resolves the Needs You level that applies to a Team, a Project or a Task, and lets the Human set it (ADR-0087). The most specific scope
/// that sets a level wins: the Project's <c>_project.md</c>, else the Team's <c>_team.md</c>, else the global value (the one Settings saved
/// in <c>team.db</c>, else <c>Team:NeedsYouLevel</c>). The spend cap of the scope that supplied the level comes with it.
/// </summary>
/// <remarks>
/// Only the Human sets a level: the setters here are for the Settings, Team and Project pages, and no Agent tool calls them. An Agent's
/// write of the files is refused by <c>NeedsYouLevelGuard</c>. A read is a plain file read on every call, as <see cref="ProcessGateService"/>'s is.
/// </remarks>
/// <param name="teams">Finds a Team by name, ignoring case.</param>
/// <param name="folders">Reads and rewrites <c>_team.md</c> and <c>_project.md</c>.</param>
/// <param name="store">Holds the global level Settings saved.</param>
/// <param name="options">Holds the configured global level.</param>
/// <param name="logger">Logs a store that could not be read; optional.</param>
/// <param name="renames">Resolves a Team's or Project's old name to today's; optional.</param>
internal sealed class NeedsYouLevels(
    ITeamCatalog teams,
    ITeamFolders folders,
    NeedsYouLevelStore store,
    IOptions<TeamOptions> options,
    ILogger<NeedsYouLevels>? logger = null,
    ITeamRenames? renames = null)
{
    /// <summary>Raised after the Human changed a level or cap, so a page can repaint. A subscriber that throws is logged and does not stop the others.</summary>
    public event Action? Changed;

    /// <summary>The level that applies to a Task: its Project's, else its Team's, else the global one.</summary>
    /// <param name="task">The Task; its folder is its scope.</param>
    /// <returns>The level, the scope that supplied it and that scope's cap.</returns>
    public EffectiveLevel ForTask(TaskItem task)
    {
        ArgumentNullException.ThrowIfNull(task);
        return this.For(task.Location.Team, task.Location.Project);
    }

    /// <summary>The level that applies to a Team, or to one of its Projects.</summary>
    /// <param name="team">The Team's name; an old name still works.</param>
    /// <param name="project">The Project's name, or <see langword="null"/> or blank for work directly under the Team.</param>
    /// <returns>The level, the scope that supplied it and that scope's cap.</returns>
    public EffectiveLevel For(string team, string? project)
    {
        ArgumentNullException.ThrowIfNull(team);
        return WithinCap(this.Resolve(team, project));
    }

    /// <summary>Sets or clears a Team's level and cap. A cap travels with a level, so clearing the level clears the cap too.</summary>
    /// <param name="team">The Team's name.</param>
    /// <param name="level">The level, or <see langword="null"/> to inherit the global one.</param>
    /// <param name="spendCap">The cap in USD, or <see langword="null"/> for none; zero or less is none.</param>
    /// <returns><see langword="false"/> when the Team has no readable <c>_team.md</c>.</returns>
    public bool SetTeam(string team, NeedsYouLevel? level, decimal? spendCap)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(team);

        decimal? cap = level is null ? null : NeedsYouLevelText.Usable(spendCap);
        bool written = folders.UpdateTeamFile(this.TeamNameOf(team), file => file with { NeedsYouLevel = level, SpendCap = cap });
        if (written)
        {
            this.RaiseChanged();
        }

        return written;
    }

    /// <summary>Sets or clears a Project's level and cap. A cap travels with a level, so clearing the level clears the cap too.</summary>
    /// <param name="team">The Team's name.</param>
    /// <param name="project">The Project's name.</param>
    /// <param name="level">The level, or <see langword="null"/> to inherit the Team's.</param>
    /// <param name="spendCap">The cap in USD, or <see langword="null"/> for none; zero or less is none.</param>
    /// <returns><see langword="false"/> when the Project folder does not exist or cannot be written.</returns>
    public bool SetProject(string team, string project, NeedsYouLevel? level, decimal? spendCap)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(team);
        ArgumentException.ThrowIfNullOrWhiteSpace(project);

        string teamName = this.TeamNameOf(team);
        string projectName = renames?.ResolveProject(teamName, project) ?? project;
        decimal? cap = level is null ? null : NeedsYouLevelText.Usable(spendCap);
        bool written = folders.UpdateProjectFile(teamName, projectName, file => file with { NeedsYouLevel = level, SpendCap = cap });
        if (written)
        {
            this.RaiseChanged();
        }

        return written;
    }

    /// <summary>Saves the global level and cap in <c>team.db</c>; from then on they win over <c>Team:NeedsYouLevel</c>.</summary>
    /// <param name="level">The level.</param>
    /// <param name="spendCap">The cap in USD, or <see langword="null"/> for none; zero or less is none.</param>
    public void SetGlobal(NeedsYouLevel level, decimal? spendCap)
    {
        store.Set(level, NeedsYouLevelText.Usable(spendCap));
        this.RaiseChanged();
    }

    /// <summary>Applies the spend cap to a resolved level. Today a cap bounds nothing; the slice that counts spend returns Ask me here once the scope has spent its cap.</summary>
    /// <param name="resolved">The level the scopes name.</param>
    /// <returns>The level in force.</returns>
    private static EffectiveLevel WithinCap(EffectiveLevel resolved) => resolved;

    private EffectiveLevel Resolve(string team, string? project)
    {
        string teamName = this.TeamNameOf(team);
        if (!string.IsNullOrWhiteSpace(teamName))
        {
            if (!string.IsNullOrWhiteSpace(project))
            {
                string projectName = renames?.ResolveProject(teamName, project) ?? project;
                if (folders.ReadProjectFile(teamName, projectName) is { NeedsYouLevel: { } projectLevel } projectFile)
                {
                    return new EffectiveLevel(projectLevel, NeedsYouScope.Project, projectName, projectFile.SpendCap);
                }
            }

            if (teams.Find(teamName) is { } summary
                && folders.ReadTeamFile(summary.Name) is { NeedsYouLevel: { } teamLevel } teamFile)
            {
                return new EffectiveLevel(teamLevel, NeedsYouScope.Team, summary.Name, teamFile.SpendCap);
            }
        }

        return this.Global();
    }

    /// <summary>The global level: the one Settings saved, else the configured one, else Ask me.</summary>
    private EffectiveLevel Global()
    {
        try
        {
            if (store.Get() is { } saved)
            {
                return new EffectiveLevel(saved.Level, NeedsYouScope.Global, null, saved.SpendCap);
            }
        }
        catch (SqliteException ex)
        {
            logger?.LogWarning(ex, "The saved global Needs You level could not be read; the configured level applies.");
        }

        NeedsYouOptions configured = options.Value.NeedsYouLevel;
        return new EffectiveLevel(
            NeedsYouLevelText.ParseOrNull(configured.Level) ?? NeedsYouLevel.AskMe,
            NeedsYouScope.Global,
            null,
            NeedsYouLevelText.Usable(configured.SpendCap));
    }

    /// <summary>A Team's name as it is today: an old name is resolved through the rename journal, as the gates do.</summary>
    /// <param name="team">A Team name, possibly old or blank.</param>
    private string TeamNameOf(string team) => string.IsNullOrWhiteSpace(team) ? team : renames?.Resolve(team) ?? team;

    private void RaiseChanged() =>
        EventRaise.Isolated(this.Changed, ex => logger?.LogWarning(ex, "A subscriber to the Needs You level change threw."));
}
