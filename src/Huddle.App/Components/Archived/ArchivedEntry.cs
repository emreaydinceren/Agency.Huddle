namespace Agency.Huddle.App.Components.Archived;

/// <summary>
/// One row of an Archived page tab. Public because it is an <see cref="ArchivedEntryList"/> parameter (rules.md: a
/// <c>[Parameter]</c> may not be of an internal type).
/// </summary>
/// <param name="Key">What the page restores the entry by: a Team's name, a Project's key, or a Room's id. Unique within its tab.</param>
/// <param name="Name">The name shown.</param>
/// <param name="Detail">The Team a Project or a scoped Room belongs to, shown after the name; <see langword="null"/> for none.</param>
/// <param name="Date">The trailing date ("Archived Oct 3"), or <see langword="null"/> when none is known.</param>
public sealed record ArchivedEntry(string Key, string Name, string? Detail, string? Date);
