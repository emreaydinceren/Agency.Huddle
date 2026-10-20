namespace Agency.Huddle.App.Feed;

/// <summary>A Room was created.</summary>
/// <param name="RoomId">The Room id.</param>
/// <param name="RoomName">The Room name.</param>
/// <param name="Team">The Room's Team, or <see langword="null"/> for a root Room.</param>
/// <param name="Members">The Members' Names as they were when posted.</param>
internal sealed record RoomCreatedPayload(string RoomId, string RoomName, string? Team, IReadOnlyList<string> Members) : FeedPayload;
