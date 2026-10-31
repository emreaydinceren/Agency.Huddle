namespace Agency.Huddle.App.Feed;

/// <summary>Resolve every unresolved Notification of a type about a target.</summary>
/// <param name="TargetKey">The target.</param>
/// <param name="Type">The source type.</param>
/// <param name="At">When it was resolved.</param>
internal sealed record ResolveCommand(string TargetKey, FeedSourceType Type, DateTimeOffset At) : FeedCommand(At);
