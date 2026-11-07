namespace Agency.Huddle.App.Feed;

/// <summary>
/// An Agent's Message mentioned <paramref name="Waiting"/> in a Room whose Budget was spent, so the Turn was refused. One per Room and Agent,
/// not per Message; the Notification is kept across a restart, and its Wake button starts exactly one Turn (#428).
/// </summary>
/// <param name="RoomId">The Room id.</param>
/// <param name="RoomName">The Room name, or <see langword="null"/> when the sender did not have it.</param>
/// <param name="Team">The Room's Team, or <see langword="null"/> for a root Room.</param>
/// <param name="Waiting">The mentioned Agent's Name when the Turn was refused.</param>
/// <param name="AgentId">The mentioned Agent's id, which survives a rename.</param>
/// <param name="MessageId">The Message that mentioned the Agent.</param>
internal sealed record RoomMentionHeldPayload(string RoomId, string? RoomName, string? Team, string Waiting, string AgentId, string MessageId) : FeedPayload;
