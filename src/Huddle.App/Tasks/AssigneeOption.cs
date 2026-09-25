namespace Agency.Huddle.App.Tasks;

/// <summary>
/// One candidate in <c>TaskDetail</c>'s Assignee <c>MudAutocomplete</c> (Spec §13.6): a known Persona's
/// Name and title, and its current presence for the item template's badge. Internal (the visibility
/// table): it is not itself one of <c>TaskDetail</c>'s <c>[Parameter]</c>s, only an implementation
/// detail of one field's autocomplete - <c>InternalsVisibleTo</c> already covers the test project.
/// </summary>
/// <param name="Name">The Persona's Name.</param>
/// <param name="Title">The Persona's title, or an empty string when it has none.</param>
/// <param name="Presence">The Persona's current presence, or <see langword="null"/> when it cannot be resolved (no directory User row for this Name).</param>
internal sealed record AssigneeOption(string Name, string Title, PresenceState? Presence);
