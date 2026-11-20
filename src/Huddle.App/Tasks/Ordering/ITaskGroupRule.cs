namespace Agency.Huddle.App.Tasks.Ordering;

/// <summary>
/// What <see cref="TaskOrder"/> needs to know about "one Task In Progress per Project" (TaskOrder spec §10.3, ADR-0103): whether
/// the rule is on for a group, and which Task already holds it. A group is one Team and one Project, or the Team's Tasks with
/// no Project. The order itself stays pure; the caller supplies the answers.
/// </summary>
internal interface ITaskGroupRule
{
    /// <summary>Whether the group admits one In Progress Task at a time.</summary>
    /// <param name="team">The Team of the group.</param>
    /// <param name="project">The Project of the group, or <see langword="null"/> for the Team's Tasks with no Project.</param>
    /// <returns><see langword="true"/> when the effective rule of the group is on.</returns>
    bool IsOn(string team, string? project);

    /// <summary>The Task In Progress in the group, wherever it is assigned.</summary>
    /// <param name="team">The Team of the group.</param>
    /// <param name="project">The Project of the group, or <see langword="null"/> for the Team's Tasks with no Project.</param>
    /// <returns>That Task, or <see langword="null"/> when the group has none.</returns>
    TaskItem? InProgressTask(string team, string? project);
}
