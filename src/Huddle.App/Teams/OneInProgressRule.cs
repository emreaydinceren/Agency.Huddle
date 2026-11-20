using System.Diagnostics.CodeAnalysis;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// The <c>oneInProgressPerProject</c> setting (ADR-0103): how its key is read and written in <c>_team.md</c> and
/// <c>_project.md</c>, and the one function that resolves the effective rule. The gate, <c>next_task</c> and the Task order
/// panel call <see cref="Resolve"/>; nobody re-derives it.
/// </summary>
internal static class OneInProgressRule
{
    /// <summary>The frontmatter key in both files.</summary>
    internal const string Key = "oneInProgressPerProject";

    /// <summary>Resolves the rule: the Project's value when it has one, else the Team's, where an absent Team value is off.</summary>
    /// <param name="team">The Team's value.</param>
    /// <param name="project">The Project's value; ignored when the Task has no Project.</param>
    /// <param name="taskHasProject">Whether the Task sits in a Project (<c>TaskLocation.Project is not null</c>); a Task with none uses the Team's value.</param>
    /// <returns>Whether the rule is on, and which level decided it.</returns>
    public static ResolvedRule Resolve(RuleSetting team, RuleSetting project, bool taskHasProject)
    {
        if (taskHasProject && project != RuleSetting.Absent)
        {
            return new ResolvedRule(project == RuleSetting.On, RuleSource.Project);
        }

        return new ResolvedRule(team == RuleSetting.On, RuleSource.Team);
    }

    /// <summary>Reads a stored value; anything but <c>on</c> or <c>off</c> (any case) counts as absent.</summary>
    /// <param name="raw">The unquoted value, or <see langword="null"/> when the key is missing.</param>
    /// <returns>The setting.</returns>
    public static RuleSetting Parse(string? raw)
    {
        string? value = raw?.Trim();
        if (string.Equals(value, "on", StringComparison.OrdinalIgnoreCase))
        {
            return RuleSetting.On;
        }

        return string.Equals(value, "off", StringComparison.OrdinalIgnoreCase) ? RuleSetting.Off : RuleSetting.Absent;
    }

    /// <summary>The text a setting is stored as.</summary>
    /// <param name="setting">The setting.</param>
    /// <param name="wire">The stored text, <c>on</c> or <c>off</c>; <see langword="null"/> for <see cref="RuleSetting.Absent"/>.</param>
    /// <returns><see langword="true"/> when the key is to be written.</returns>
    public static bool TryToWire(RuleSetting setting, [NotNullWhen(true)] out string? wire)
    {
        wire = setting switch
        {
            RuleSetting.On => "on",
            RuleSetting.Off => "off",
            _ => null,
        };
        return wire is not null;
    }
}
