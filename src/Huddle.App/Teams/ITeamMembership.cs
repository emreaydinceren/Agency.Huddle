namespace Agency.Huddle.App.Teams;

/// <summary>Adds a Persona to a Team or removes it, by rewriting only the <c>teams</c> field of the Persona's definition.</summary>
internal interface ITeamMembership
{
    /// <summary>Adds the Persona to the Team, using the catalog's spelling of the Team when one exists.</summary>
    /// <param name="team">The Team to join, compared ignoring case.</param>
    /// <param name="personaName">The Persona's Name.</param>
    /// <returns>Added, AlreadyMember, NotFound (no such Persona) or Rejected (an unwritable Team name or a failed write).</returns>
    MembershipResult Add(string team, string personaName);

    /// <summary>Removes the Persona from the Team, dropping every case variant of the label.</summary>
    /// <param name="team">The Team to leave, compared ignoring case.</param>
    /// <param name="personaName">The Persona's Name.</param>
    /// <returns>Removed, NotMember, NotFound (no such Persona) or Rejected (a failed write).</returns>
    MembershipResult Remove(string team, string personaName);
}
