namespace Agency.Huddle.App.Feed;

/// <summary>A Task proceeded under the level Run inside the Team: its Proof is complete and its merge into main needs no Accept (ADR-0087).</summary>
/// <param name="Id">The Task id.</param>
/// <param name="Title">The Task title.</param>
/// <param name="Team">The Task's Team.</param>
/// <param name="Scope">The scope that supplied the level: <c>global</c>, <c>Team</c> or <c>project</c>.</param>
internal sealed record TaskProceededPayload(string Id, string Title, string Team, string Scope) : FeedPayload;
