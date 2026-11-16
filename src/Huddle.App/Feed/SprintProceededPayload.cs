namespace Agency.Huddle.App.Feed;

/// <summary>A Sprint was approved without the Human, because every Task it names runs under the level Run inside the Team (ADR-0088).</summary>
/// <param name="Name">The Sprint's name.</param>
/// <param name="Team">The Team of the Sprint's Tasks, or <see langword="null"/> when they are in more than one.</param>
/// <param name="Scope">The scope that supplied each Task's level (<c>global</c>, <c>Team</c> or <c>project</c>), the distinct ones joined with <c>and</c>.</param>
/// <param name="TaskCount">How many Tasks were added to it.</param>
internal sealed record SprintProceededPayload(string Name, string? Team, string Scope, int TaskCount) : FeedPayload;
