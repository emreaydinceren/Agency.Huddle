namespace Agency.Huddle.App.Tasks;

/// <summary>A line of <c>sprints.md</c> that looks like a Sprint line and is not one (Sprints spec §6).</summary>
/// <param name="Line">The 1-based line number.</param>
/// <param name="Text">The problem, starting with <c>Line N:</c>.</param>
internal sealed record SprintProblem(int Line, string Text);
