namespace Agency.Huddle.App.Acp;

/// <summary>The process priming of a hired member whose Team has a <c>Process.md</c> (Task 58, PR-4).</summary>
/// <param name="IsManager">Whether the member is the Team's manager, who receives the whole text rather than its path.</param>
/// <param name="Content">The whole <c>Process.md</c> text for the manager, or its Library path for every other member.</param>
internal sealed record ProcessBlockInput(bool IsManager, string Content);
