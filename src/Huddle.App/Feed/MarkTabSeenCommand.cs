namespace Agency.Huddle.App.Feed;

/// <summary>Move the Feed Tab's own seen mark (<c>feed_state</c>) forward; it never moves back.</summary>
/// <param name="UpToId">The highest Feed item id the Tab has shown.</param>
/// <param name="At">When the Tab showed it.</param>
internal sealed record MarkTabSeenCommand(long UpToId, DateTimeOffset At) : FeedCommand(At);
