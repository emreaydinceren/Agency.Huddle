namespace Agency.Huddle.App.Tasks.Views;

/// <summary>One swimlane of a Board: its raw grouping key, its display label, and its cells. Key is <c>""</c> when ungrouped.</summary>
public sealed record BoardLane(string Key, string? Label, IReadOnlyList<BoardCell> Cells);
