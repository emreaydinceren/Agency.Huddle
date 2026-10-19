namespace Agency.Huddle.App.References;

/// <summary>Everything the picker draws for one search: the groups, the lines outside them, and the highlight (spec §5.3, §5.7, §11).</summary>
/// <param name="Groups">The groups that have rows, in order.</param>
/// <param name="EmptyText">The line shown when there is no row, or null.</param>
/// <param name="Footer">The footer line that names the prefixes.</param>
/// <param name="LiveText">The text for the polite live region.</param>
/// <param name="HighlightIndex">The index of the highlighted row among all rows, clamped into them.</param>
public sealed record ReferencePickerView(IReadOnlyList<ReferencePickerGroup> Groups, string? EmptyText, string Footer, string LiveText, int HighlightIndex);
