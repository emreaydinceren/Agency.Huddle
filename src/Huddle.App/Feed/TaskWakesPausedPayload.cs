namespace Agency.Huddle.App.Feed;

/// <summary>A Task reached its limit of wake-ups made by Agents, so the hand-off to <paramref name="Waiting"/> waits for the Human.</summary>
/// <param name="Id">The Task's id.</param>
/// <param name="Title">The Task's title.</param>
/// <param name="Team">The Task's Team.</param>
/// <param name="Waiting">The Agent the hand-off is waiting to reach.</param>
/// <param name="RoomId">The paused Room that refused the hand-off, or <see langword="null"/> when the Task's wake budget did (and in a payload saved before this field).</param>
internal sealed record TaskWakesPausedPayload(string Id, string Title, string Team, string Waiting, string? RoomId = null) : FeedPayload;
