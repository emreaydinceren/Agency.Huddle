namespace Agency.Huddle.App.Feed;

/// <summary>Someone joined a Room.</summary>
/// <param name="RoomId">The Room id.</param>
/// <param name="RoomName">The Room name.</param>
/// <param name="Team">The Room's Team, or <see langword="null"/> for a root Room.</param>
/// <param name="Member">The joiner's Name.</param>
internal sealed record RoomJoinedPayload(string RoomId, string RoomName, string? Team, string Member) : FeedPayload;
