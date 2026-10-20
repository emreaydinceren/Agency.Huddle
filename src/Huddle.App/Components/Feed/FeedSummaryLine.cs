namespace Agency.Huddle.App.Components.Feed;

/// <summary>The words of one Feed item's first line, ready to draw (Feed spec §7).</summary>
/// <param name="Segments">The summary, in order, after the actor's Name unless <paramref name="OmitActor"/> is set.</param>
/// <param name="Context">Trailing context drawn after " · " in Quiet ink, or <see langword="null"/>.</param>
/// <param name="OmitActor">Whether the line stands on its own, with no actor's Name in front of it.</param>
internal sealed record FeedSummaryLine(IReadOnlyList<FeedSegment> Segments, string? Context, bool OmitActor = false);
