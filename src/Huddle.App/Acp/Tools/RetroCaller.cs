using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Acp.Tools;

/// <summary>The caller check the retro tools share: an Agent who holds the <c>retrospective</c> Skill and is the Team's manager or its Retro agent.</summary>
internal static class RetroCaller
{
    /// <summary>The Skill a caller must hold.</summary>
    public const string SkillName = "retrospective";

    /// <summary>Checks the caller belongs to the Team, holds the Skill and is its manager or Retro agent.</summary>
    /// <param name="personas">Finds the caller's identity.</param>
    /// <param name="types">Finds the Team type's Retro role.</param>
    /// <param name="team">The named Team.</param>
    /// <param name="caller">The caller's name.</param>
    /// <param name="notAllowed">The refusal for a member who holds the Skill but has neither role.</param>
    /// <returns>The refusal, or <see langword="null"/> when the caller may go on.</returns>
    public static string? Check(PersonaStore personas, TeamTypeCatalog types, TeamSummary team, string caller, string notAllowed)
    {
        if (!team.Members.Contains(caller, StringComparer.OrdinalIgnoreCase))
        {
            return $"You are not a member of Team {team.Name}.";
        }

        PersonaIdentity? identity = personas.Get(caller) is { } persona
            && PersonaFrontmatter.TryReadIdentity(persona.Text, out PersonaIdentity? read, out _)
            ? read
            : null;
        if (identity?.Skills is not { } skills || !skills.Contains(SkillName, StringComparer.Ordinal))
        {
            return "You do not hold the retrospective Skill.";
        }

        if (string.Equals(team.Manager, caller, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string? retroRole = team.TeamType is { } typeRef ? types.Find(typeRef.Id)?.RetroRole : null;
        bool isRetro = retroRole is not null
            && team.TeamType is { } type
            && identity.Role is { } role
            && string.Equals(role.TypeId, type.Id, StringComparison.Ordinal)
            && string.Equals(role.RoleId, retroRole, StringComparison.Ordinal);
        return isRetro ? null : notAllowed;
    }
}
