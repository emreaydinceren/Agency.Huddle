using Agency.Huddle.App.Search;

namespace Agency.Huddle.App.References;

/// <summary>One selectable row of the reference picker: a hit, the text it inserts, and how it is drawn (spec §5.3, §11).</summary>
/// <param name="Id">The element id of the row, <c>{listboxId}-{n}</c>, counted from zero across the groups.</param>
/// <param name="Hit">The result the row shows.</param>
/// <param name="InsertText">The text the pick writes into the Composer.</param>
/// <param name="Dimmed">Whether the row is drawn dimmed: a Teammate who is not a Member.</param>
/// <param name="Note">The line the card shows under its title, or null.</param>
/// <param name="AccessibleName">The card's own text, with the note after a comma when there is one.</param>
public sealed record ReferencePickerRow(string Id, SearchHit Hit, string InsertText, bool Dimmed, string? Note, string AccessibleName);
