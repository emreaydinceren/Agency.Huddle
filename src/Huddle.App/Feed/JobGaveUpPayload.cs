namespace Agency.Huddle.App.Feed;

/// <summary>A one-off Job was removed without posting (Turn Queue spec §7.5).</summary>
/// <param name="JobId">The Job's id.</param>
/// <param name="Title">The Job's title.</param>
/// <param name="Reason">Why it did not run, as the sentence's tail: "its Room is gone".</param>
/// <param name="RoomId">The Job's Room, or <see langword="null"/> when it has none.</param>
internal sealed record JobGaveUpPayload(string JobId, string Title, string Reason, string? RoomId) : FeedPayload;
