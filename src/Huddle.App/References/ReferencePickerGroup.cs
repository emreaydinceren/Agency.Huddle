namespace Agency.Huddle.App.References;

/// <summary>A group of the picker: its heading and its rows, in render order (spec §5.3).</summary>
/// <param name="Heading">The heading text, as spec §5.7 names it.</param>
/// <param name="Rows">The rows of the group.</param>
public sealed record ReferencePickerGroup(string Heading, IReadOnlyList<ReferencePickerRow> Rows);
