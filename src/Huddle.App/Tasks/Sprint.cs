namespace Agency.Huddle.App.Tasks;

/// <summary>A Sprint: a stable id and a display name (Sprints spec §7.2).</summary>
/// <param name="Id">The Sprint's id, which a Task holds.</param>
/// <param name="Name">The Sprint's name, shown wherever the id is drawn.</param>
public sealed record Sprint(SprintId Id, string Name);

/// <summary>A line of <c>sprints.md</c> that looks like a Sprint line and is not one (Sprints spec §6).</summary>
/// <param name="Line">The 1-based line number.</param>
/// <param name="Text">The problem, starting with <c>Line N:</c>.</param>
public sealed record SprintProblem(int Line, string Text);

/// <summary>The result of parsing <c>sprints.md</c>: the Sprints it defines and the lines it skipped.</summary>
/// <param name="Sprints">Every Sprint, oldest first (by number).</param>
/// <param name="Problems">Every invalid Sprint line, in file order.</param>
public sealed record SprintFile(IReadOnlyList<Sprint> Sprints, IReadOnlyList<SprintProblem> Problems);
