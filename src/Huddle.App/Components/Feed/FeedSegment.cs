namespace Agency.Huddle.App.Components.Feed;

/// <summary>One piece of a Feed summary: some text, or a reference chip.</summary>
/// <param name="Kind">How it is drawn.</param>
/// <param name="Text">The visible text; a Task chip's title is already cut to fit.</param>
/// <param name="TargetId">What a chip opens: a Task id, a Room id, or a Library absolute path; <see langword="null"/> for text.</param>
/// <param name="Tooltip">The full text behind a shortened chip, or <see langword="null"/>.</param>
internal sealed record FeedSegment(FeedSegmentKind Kind, string Text, string? TargetId = null, string? Tooltip = null);
