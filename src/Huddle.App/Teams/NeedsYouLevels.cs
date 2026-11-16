using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// Resolves the Needs You level that applies to a Team, a Project or a Task, and lets the Human set it (ADR-0088). The most specific scope
/// that sets a level wins: the Project's, else the Team's, else the global value (the one Settings saved, else <c>Team:NeedsYouLevel</c>).
/// The spend cap of the scope that supplied the level comes with it. Every scope's level and cap live in <c>team.db</c> under the scope's id
/// (<see cref="ScopeIdStore"/>), so a rename keeps them and no file an Agent can write holds them.
/// </summary>
/// <remarks>Only the Human sets a level: the setters here are for the Settings, Team and Project pages, and no Agent tool calls them.</remarks>
/// <param name="teams">Finds a Team by name, ignoring case.</param>
/// <param name="store">Holds each scope's level and cap.</param>
/// <param name="ids">Maps a Team or Project name to the id its scope is stored under.</param>
/// <param name="options">Holds the configured global level.</param>
/// <param name="logger">Logs a store that could not be read; optional.</param>
/// <param name="renames">Resolves a Team's or Project's old name to today's; optional.</param>
/// <param name="caps">Counts spend against a cap; without it a cap bounds nothing. Optional.</param>
internal sealed class NeedsYouLevels(
    ITeamCatalog teams,
    NeedsYouLevelStore store,
    ScopeIdStore ids,
    IOptions<TeamOptions> options,
    ILogger<NeedsYouLevels>? logger = null,
    ITeamRenames? renames = null,
    NeedsYouCaps? caps = null)
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
        return this.WithinCap(this.Resolve(team, project), this.TeamNameOf(team));
    }

    /// <summary>The level and cap a scope itself sets, not what it inherits.</summary>
    /// <param name="team">The Team's name, or <see langword="null"/> or blank for the global scope.</param>
    /// <param name="project">The Project's name, or <see langword="null"/> or blank for the Team's own scope.</param>
    /// <returns>The pair; the level is <see langword="null"/> when the Team or Project sets none. The global scope always has one.</returns>
    public (NeedsYouLevel? Level, decimal? Cap) Own(string? team, string? project)
    {
        if (string.IsNullOrWhiteSpace(team))
        {
            EffectiveLevel global = this.Global();
            return (global.Level, global.SpendCap);
        }

        try
        {
            return this.FindKey(team, project) is { } key && store.Get(key) is { } saved ? (saved.Level, saved.SpendCap) : (null, null);
        }
        catch (SqliteException ex)
        {
            logger?.LogWarning(ex, "The Needs You level of a scope could not be read; it shows as unset.");
            return (null, null);
        }
    }

    /// <summary>The key the scope's spend count and open question are stored under, without creating an id.</summary>
    /// <param name="team">The Team's name, or <see langword="null"/> or blank for the global scope.</param>
    /// <param name="project">The Project's name, or <see langword="null"/> or blank for the Team's own scope.</param>
    /// <returns>The key, or <see langword="null"/> when nothing has been saved for the scope yet, so it has nothing to count.</returns>
    public string? KeyOf(string? team, string? project) =>
        string.IsNullOrWhiteSpace(team) ? NeedsYouCaps.GlobalKey : this.FindKey(team, project);

    /// <summary>Whether a Team, or one of its Projects, exists, so the Human can set a level for it.</summary>
    /// <param name="team">The Team's name.</param>
    /// <param name="project">The Project's name, or <see langword="null"/> or blank for the Team itself.</param>
    /// <returns><see langword="true"/> when the Team, and the Project when one is named, exists.</returns>
    public bool CanSet(string team, string? project) => this.Known(team, project) is not null;

    /// <summary>Sets or clears a Team's level and cap. A cap travels with a level, so clearing the level clears the cap too.</summary>
    /// <param name="team">The Team's name.</param>
    /// <param name="level">The level, or <see langword="null"/> to inherit the global one.</param>
    /// <param name="spendCap">The cap in USD, or <see langword="null"/> for none; zero or less is none.</param>
    /// <returns><see langword="false"/> when there is no such Team; nothing is stored then. Saving what the scope already has is a success and changes nothing.</returns>
    public bool SetTeam(string team, NeedsYouLevel? level, decimal? spendCap)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(team);

        if (this.Known(team, null) is not { } known)
        {
            return false;
        }

        this.Save(NeedsYouCaps.TeamKey(ids.TeamId(known.Team)), level, spendCap);
        return true;
    }

    /// <summary>Sets or clears a Project's level and cap. A cap travels with a level, so clearing the level clears the cap too.</summary>
    /// <param name="team">The Team's name.</param>
    /// <param name="project">The Project's name.</param>
    /// <param name="level">The level, or <see langword="null"/> to inherit the Team's.</param>
    /// <param name="spendCap">The cap in USD, or <see langword="null"/> for none; zero or less is none.</param>
    /// <returns><see langword="false"/> when the Team has no such Project; nothing is stored then. Saving what the scope already has is a success and changes nothing.</returns>
    public bool SetProject(string team, string project, NeedsYouLevel? level, decimal? spendCap)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(team);
        ArgumentException.ThrowIfNullOrWhiteSpace(project);

        if (this.Known(team, project) is not { Project: { } projectName } known)
        {
            return false;
        }

        this.Save(NeedsYouCaps.ProjectKey(ids.ProjectId(known.Team, projectName)), level, spendCap);
        return true;
    }

    /// <summary>Saves the global level and cap in <c>team.db</c>; from then on they win over <c>Team:NeedsYouLevel</c>.</summary>
    /// <param name="level">The level.</param>
    /// <param name="spendCap">The cap in USD, or <see langword="null"/> for none; zero or less is none.</param>
    public void SetGlobal(NeedsYouLevel level, decimal? spendCap) => this.Save(NeedsYouCaps.GlobalKey, level, spendCap);

    /// <summary>
    /// Applies the spend cap to a resolved level: a scope that runs inside the Team and has spent its allowance falls back to Ask me, and
    /// every reader of the level follows at once. Ask me and a scope without a cap are unchanged.
    /// </summary>
    /// <param name="resolved">The level the scopes name.</param>
    /// <param name="team">The Team the level was asked for.</param>
    /// <returns>The level in force.</returns>
    private EffectiveLevel WithinCap(EffectiveLevel resolved, string team)
    {
        if (caps is null || resolved is not { Level: NeedsYouLevel.RunInsideTheTeam, SpendCap: not null })
        {
            return resolved;
        }

        try
        {
            return caps.IsReached(resolved, team) ? resolved with { Level = NeedsYouLevel.AskMe, CapReached = true } : resolved;
        }
        catch (SqliteException ex)
        {
            logger?.LogWarning(ex, "The spend of a scope could not be counted; it falls back to Ask me.");
            return resolved with { Level = NeedsYouLevel.AskMe };
        }
    }

    /// <summary>
    /// Writes a scope's level and cap, and starts its spend count over, only when either differs from what the scope already has: saving the
    /// same values again neither restarts the count nor cancels an open question.
    /// </summary>
    private void Save(string key, NeedsYouLevel? level, decimal? spendCap)
    {
        decimal? cap = level is null ? null : NeedsYouLevelText.Usable(spendCap);
        SavedNeedsYouLevel? existing = store.Get(key);
        if (level is { } set)
        {
            if (existing is not null && existing.Level == set && existing.SpendCap == cap)
            {
                return;
            }

            store.Set(key, set, cap);
        }
        else
        {
            if (existing is null)
            {
                return;
            }

            store.Delete(key);
        }

        this.RestartCount(key, level, cap);
        this.RaiseChanged();
    }

    /// <summary>Starts a scope's count over after the Human set its level or cap; only a scope that runs inside the Team with a cap is counted.</summary>
    private void RestartCount(string key, NeedsYouLevel? level, decimal? cap)
    {
        try
        {
            caps?.Restart(key, level == NeedsYouLevel.RunInsideTheTeam ? cap : null);
        }
        catch (SqliteException ex)
        {
            logger?.LogWarning(ex, "The spend count of {Scope} could not be restarted; it starts when the scope is next read.", key);
        }
    }

    /// <summary>The Team as the catalog names it today and, when a Project is asked for, the Project as the Team lists it; <see langword="null"/> when either is missing.</summary>
    private (string Team, string? Project)? Known(string team, string? project)
    {
        if (teams.Find(this.TeamNameOf(team)) is not { } summary)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(project))
        {
            return (summary.Name, null);
        }

        string projectName = renames?.ResolveProject(summary.Name, project) ?? project;
        string? listed = summary.Projects.FirstOrDefault(name => string.Equals(name, projectName, StringComparison.OrdinalIgnoreCase));
        return listed is null ? null : (summary.Name, listed);
    }

    /// <summary>The key of the Project's or the Team's scope when it has an id, so a read never mints one.</summary>
    private string? FindKey(string team, string? project)
    {
        string teamName = this.TeamNameOf(team);
        if (!string.IsNullOrWhiteSpace(project))
        {
            string projectName = renames?.ResolveProject(teamName, project) ?? project;
            return ids.FindProjectId(teamName, projectName) is { } projectId ? NeedsYouCaps.ProjectKey(projectId) : null;
        }

        return ids.FindTeamId(teamName) is { } teamId ? NeedsYouCaps.TeamKey(teamId) : null;
    }

    private EffectiveLevel Resolve(string team, string? project)
    {
        string teamName = this.TeamNameOf(team);
        if (string.IsNullOrWhiteSpace(teamName))
        {
            return this.Global();
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(project)
                && this.FindKey(teamName, project) is { } projectKey
                && store.Get(projectKey) is { } projectSaved)
            {
                string projectName = renames?.ResolveProject(teamName, project) ?? project;
                return new EffectiveLevel(projectSaved.Level, NeedsYouScope.Project, projectName, projectSaved.SpendCap);
            }

            if (teams.Find(teamName) is { } summary
                && this.FindKey(summary.Name, null) is { } teamKey
                && store.Get(teamKey) is { } teamSaved)
            {
                return new EffectiveLevel(teamSaved.Level, NeedsYouScope.Team, summary.Name, teamSaved.SpendCap);
            }
        }
        catch (SqliteException ex)
        {
            logger?.LogWarning(ex, "The Needs You level of a scope could not be read; it falls back to Ask me.");
            return new EffectiveLevel(NeedsYouLevel.AskMe, NeedsYouScope.Global, null, null);
        }

        return this.Global();
    }

    /// <summary>The global level: the one Settings saved, else the configured one, else Ask me.</summary>
    private EffectiveLevel Global()
    {
        try
        {
            if (store.Get(NeedsYouCaps.GlobalKey) is { } saved)
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
