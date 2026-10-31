namespace Agency.Huddle.App.Feed;

/// <summary>A Room waits on the Human: a mention, question, form or budget stop. Carries no Message text.</summary>
/// <param name="RoomId">The Room id.</param>
/// <param name="RoomName">The Room name, filled in by the writer when the sender did not have it.</param>
/// <param name="Team">The Room's Team, filled in by the writer, or <see langword="null"/> for a root Room.</param>
internal sealed record RoomWaitPayload(string RoomId, string? RoomName, string? Team) : FeedPayload;
