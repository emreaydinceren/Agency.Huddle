namespace Agency.Huddle.App.Feed;

/// <summary>The Feed items to post and the Notifications to resolve for one change, in that order.</summary>
/// <param name="Posts">The items to post.</param>
/// <param name="Resolutions">The Notifications to resolve.</param>
internal sealed record FeedEffects(IReadOnlyList<FeedMessage> Posts, IReadOnlyList<FeedResolution> Resolutions);
