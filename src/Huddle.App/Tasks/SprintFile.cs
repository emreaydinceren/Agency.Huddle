namespace Agency.Huddle.App.Tasks;

/// <summary>The result of parsing <c>sprints.md</c>: the Sprints it defines and the lines it skipped.</summary>
/// <param name="Sprints">Every Sprint, oldest first (by number).</param>
/// <param name="Problems">Every invalid Sprint line, in file order.</param>
internal sealed record SprintFile(IReadOnlyList<Sprint> Sprints, IReadOnlyList<SprintProblem> Problems);
