namespace Agency.Huddle.App.Components.Feed;

/// <summary>The one-line status under a Notification (Feed spec §6.12): a dot or a check, then the facts.</summary>
/// <param name="Resolved">Whether the Notification has been resolved, which swaps the Accent dot for a Success check.</param>
/// <param name="Text">The facts, such as <c>Asked 14:31 · waiting for you</c>.</param>
internal sealed record FeedStatusLine(bool Resolved, string Text);
