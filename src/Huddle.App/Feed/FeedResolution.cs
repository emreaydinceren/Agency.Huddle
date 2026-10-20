namespace Agency.Huddle.App.Feed;

/// <summary>One Notification type about one target that a sender says no longer matters.</summary>
/// <param name="TargetKey">The target the Notification is about.</param>
/// <param name="Type">The Notification's source type.</param>
internal sealed record FeedResolution(string TargetKey, FeedSourceType Type);
