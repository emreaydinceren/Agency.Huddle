namespace Agency.Huddle.App.Feed;

/// <summary>A process gate was switched on for a Task.</summary>
/// <param name="Id">The Task id.</param>
/// <param name="Title">The Task title.</param>
/// <param name="Team">The Task's Team.</param>
/// <param name="Action">The gate's action, or <see langword="null"/>.</param>
internal sealed record TaskGatePayload(string Id, string Title, string Team, string? Action) : FeedPayload;
