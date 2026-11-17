namespace Agency.Huddle.App.Feed;

/// <summary>An Agent set a Follow-up in a Room (Turn Queue spec §7.3).</summary>
/// <param name="JobId">The Follow-up's id.</param>
/// <param name="AgentName">The Agent that set it.</param>
/// <param name="Title">The Follow-up's title.</param>
/// <param name="RoomId">The Room it comes back to.</param>
/// <param name="RoomName">The Room's name when it was set.</param>
/// <param name="When">When it runs, in this machine's zone: "Mon 2 Nov 2026 at 08:30".</param>
internal sealed record JobFollowUpSetPayload(string JobId, string AgentName, string Title, string RoomId, string RoomName, string When) : FeedPayload;
