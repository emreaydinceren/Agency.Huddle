namespace Agency.Huddle.App.Feed;

/// <summary>A Task reached its limit of wake-ups made by Agents, so the hand-off to <paramref name="Waiting"/> waits for the Human.</summary>
/// <param name="Id">The Task's id.</param>
/// <param name="Title">The Task's title.</param>
/// <param name="Team">The Task's Team.</param>
/// <param name="Waiting">The Agent the hand-off is waiting to reach.</param>
internal sealed record TaskWakesPausedPayload(string Id, string Title, string Team, string Waiting) : FeedPayload;
