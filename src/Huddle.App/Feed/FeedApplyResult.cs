namespace Agency.Huddle.App.Feed;

/// <summary>What one committed batch changed, so the writer can raise its events.</summary>
/// <param name="Added">The rows the batch inserted, in order.</param>
/// <param name="Changed">Whether any Resolve, MarkSeen or Dismiss touched a row.</param>
internal sealed record FeedApplyResult(IReadOnlyList<FeedItem> Added, bool Changed);
