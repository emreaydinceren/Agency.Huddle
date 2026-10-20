namespace Agency.Huddle.App.Feed;

/// <summary>Mark every unseen Notification up to an id as seen by the bell.</summary>
/// <param name="UpToId">The highest id to mark.</param>
/// <param name="At">When it was seen.</param>
internal sealed record MarkSeenCommand(long UpToId, DateTimeOffset At) : FeedCommand(At);
