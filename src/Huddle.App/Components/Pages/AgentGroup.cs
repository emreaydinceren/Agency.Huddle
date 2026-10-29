namespace Agency.Huddle.App.Components.Pages;

using Agency.Huddle.App.Acp;

/// <summary>
/// One heading's worth of tiles on the Agents directory page: every Persona whose
/// <see cref="PersonaEntry.Teams"/> names this Team, or every Persona with no Team at all when
/// <see cref="Heading"/> is <see cref="AgentGrouping.NoTeamHeading"/>.
/// </summary>
/// <param name="Heading">The Team name shown above this group's tiles.</param>
/// <param name="Members">Every Persona belonging to this group, in <see cref="AgentGrouping.Group"/>'s input order.</param>
public sealed record AgentGroup(string Heading, IReadOnlyList<PersonaEntry> Members);
