namespace Agency.Huddle.App.Components.Feed;

/// <summary>The status pill of a Notification card.</summary>
/// <param name="Kind">Which pill.</param>
/// <param name="Text">What it says.</param>
/// <param name="Pause">Whether a pause glyph comes before the text (a Budget stop).</param>
internal sealed record FeedPill(FeedPillKind Kind, string Text, bool Pause = false);
