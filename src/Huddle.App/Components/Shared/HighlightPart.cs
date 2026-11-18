namespace Agency.Huddle.App.Components.Shared;

/// <summary>One run of text drawn by <c>SearchHighlight</c>.</summary>
/// <param name="Text">The run's characters.</param>
/// <param name="Matched">Whether the run is an occurrence of the search term.</param>
internal readonly record struct HighlightPart(string Text, bool Matched);
