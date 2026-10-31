namespace Agency.Huddle.App.Feed;

/// <summary>A Room was archived.</summary>
/// <param name="RoomId">The Room id.</param>
/// <param name="RoomName">The Room name.</param>
/// <param name="Team">The Room's Team, or <see langword="null"/> for a root Room.</param>
internal sealed record RoomArchivedPayload(string RoomId, string RoomName, string? Team) : FeedPayload;
