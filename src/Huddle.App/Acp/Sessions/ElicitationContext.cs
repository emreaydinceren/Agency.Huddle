namespace Agency.Huddle.App.Acp.Sessions;

/// <summary>Where an <see cref="Agency.Huddle.Acp.Abstractions.ElicitationRequest"/> comes from: the Room whose Turn is asking, and the Agent asking.</summary>
/// <param name="RoomId">The Room of the Turn the request was made in, never the session's own Room, which is null for a shared session.</param>
/// <param name="AgentId">The id of the Agent whose session made the request.</param>
internal sealed record ElicitationContext(string RoomId, string AgentId);
