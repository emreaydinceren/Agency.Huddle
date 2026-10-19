namespace Agency.Huddle.App.Components.Pages;

using Agency.Huddle.App.Acp;

/// <summary>
/// One heading's worth of tiles on the Teammates directory page: every Persona whose
/// <see cref="PersonaEntry.Teams"/> names this Team, or every Persona with no Team at all when
/// <see cref="Heading"/> is <see cref="TeammateGrouping.NoTeamHeading"/>.
/// </summary>
/// <param name="Heading">The Team name shown above this group's tiles.</param>
/// <param name="Members">Every Persona belonging to this group, in <see cref="TeammateGrouping.Group"/>'s input order.</param>
public sealed record TeammateGroup(string Heading, IReadOnlyList<PersonaEntry> Members);
