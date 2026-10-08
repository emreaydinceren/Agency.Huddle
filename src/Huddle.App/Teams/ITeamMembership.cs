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
    /// <returns>Removed, IsManager (the Team's manager is never removed; nothing is written), NotMember, NotFound (no such Persona) or Rejected (a failed write).</returns>
    MembershipResult Remove(string team, string personaName);

    /// <summary>Makes a member of the Team its manager by rewriting the <c>manager</c> field of the Team's <c>_team.md</c>.</summary>
    /// <param name="team">The Team, compared ignoring case.</param>
    /// <param name="newManager">The member's Name; their <c>_role</c> must be a manager role of the Team's type.</param>
    /// <returns>ManagerReplaced, NotFound (no such Persona), NotMember, or Rejected (not a manager role, or no readable team file).</returns>
    MembershipResult ReplaceManager(string team, string newManager);
}
