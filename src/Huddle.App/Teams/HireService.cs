using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teammates;

namespace Agency.Huddle.App.Teams;

/// <summary>One hire: a resolved card for a role of a team type, to be written as a Teammate of a Team.</summary>
/// <param name="Team">The Team the Teammate joins.</param>
/// <param name="TypeId">The team type id.</param>
/// <param name="RoleId">The role id within the type.</param>
/// <param name="Card">The resolved card carrying the name, model pick, skills and packs.</param>
internal sealed record HireRequest(string Team, string TypeId, string RoleId, CardState Card);

/// <summary>The outcome of one hire.</summary>
/// <param name="Ok">Whether the Teammate file was written.</param>
/// <param name="Error">Why the hire was refused, or what failed after the Teammate was written; <see langword="null"/> when nothing went wrong.</param>
/// <param name="PersonaName">The new Teammate's name when the file was written; otherwise <see langword="null"/>.</param>
internal sealed record HireResult(bool Ok, string? Error, string? PersonaName);

/// <summary>
/// Turns a resolved card into an ordinary Teammate file, a model row and an avatar (HR-5, HR-6,
/// Spec §Hiring steps 4-5). Refusals write nothing; a failure after the file is written is logged and
/// reported, and the Teammate stays.
/// </summary>
/// <param name="personas">Where the Teammate file is written.</param>
/// <param name="types">The team types, for the role and its candidate.</param>
/// <param name="packs">The skill packs, for their consult-when additions.</param>
/// <param name="avatars">Where the avatar is saved.</param>
/// <param name="limits">The shared hire-limit check.</param>
/// <param name="logger">Receives post-write failures.</param>
internal sealed class HireService(
    PersonaStore personas,
    TeamTypeCatalog types,
    SkillPackStore packs,
    AvatarStore avatars,
    HireLimitGuard limits,
    ILogger<HireService> logger)
{
    /// <summary>Hires one Teammate.</summary>
    /// <param name="request">What to hire.</param>
    /// <returns>The outcome; a refusal writes nothing.</returns>
    public HireResult Hire(HireRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        TeamTypeManifest? type = types.Find(request.TypeId);
        if (type is null)
        {
            return HireService.Refuse($"Unknown team type '{request.TypeId}'.");
        }

        RoleSpec? role = type.Roles.FirstOrDefault(r => string.Equals(r.Id, request.RoleId, StringComparison.Ordinal));
        CandidateTemplate? template = role is null ? null : types.ReadCandidate(request.TypeId, request.RoleId);
        if (role is null || template is null)
        {
            return HireService.Refuse($"Team type '{request.TypeId}' has no role '{request.RoleId}'.");
        }

        string name = request.Card.Name;
        string alias = name.ToLowerInvariant();
        if (personas.ResolveByNameOrAlias(name) is not null || personas.ResolveByNameOrAlias(alias) is not null)
        {
            return HireService.Refuse($"The name '{name}' is already in use.");
        }

        if (role.MaxPerTeam > 0 && this.CountInRole(request.Team, request.TypeId, request.RoleId) >= role.MaxPerTeam)
        {
            return HireService.Refuse($"Team '{request.Team}' already has the most {role.Title} Teammates it allows ({role.MaxPerTeam}).");
        }

        string? consultWhen = this.MergeConsultWhen(template, request.Card);
        Candidate candidate = new(name, alias, template.Title, template.Stance, [request.Team], consultWhen);
        HireRefusal? refusal = limits.Check([candidate]);
        if (refusal is not null)
        {
            return HireService.Refuse(HireService.Describe(refusal));
        }

        PersonaIdentity identity = new(
            name,
            template.Title,
            alias,
            [request.Team],
            request.Card.Pick?.AdapterId ?? template.Adapter,
            [.. request.Card.Skills.Select(static s => s.Name)],
            Role: new RoleRef(request.TypeId, request.RoleId, type.Version),
            Packs: request.Card.Packs,
            ConsultWhen: consultWhen);

        try
        {
            _ = personas.Add(identity, template.Stance, request.Card.Pick?.Model);
        }
        catch (ChatException ex)
        {
            return HireService.Refuse(ex.Message);
        }

        return this.SaveAvatar(name);
    }

    /// <summary>Saves the new Teammate's default avatar; a failure is logged and reported while the Teammate stays.</summary>
    private HireResult SaveAvatar(string name)
    {
        try
        {
            avatars.Save(name, Avatar.None);
            return new HireResult(true, null, name);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Teammate {PersonaName} was hired but its avatar could not be saved.", name);
            return new HireResult(true, $"{name} was hired, but the avatar could not be saved.", name);
        }
    }

    /// <summary>Counts the Team's Teammates hired into a role.</summary>
    private int CountInRole(string team, string typeId, string roleId)
    {
        int count = 0;
        foreach (PersonaEntry entry in personas.Entries)
        {
            if (entry.Teams.Contains(team, StringComparer.OrdinalIgnoreCase)
                && PersonaFrontmatter.TryReadIdentity(entry.Text, out PersonaIdentity? identity, out _)
                && identity.Role is { } hired
                && string.Equals(hired.TypeId, typeId, StringComparison.Ordinal)
                && string.Equals(hired.RoleId, roleId, StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Joins the candidate's consult-when with each pack's addition by <c>"; "</c> (MS-4).</summary>
    private string? MergeConsultWhen(CandidateTemplate template, CardState card)
    {
        List<string> parts = [];
        string? own = template.JobDescriptionFields
            .FirstOrDefault(static f => string.Equals(f.Key, "consult_when", StringComparison.OrdinalIgnoreCase))?.Value;
        if (!string.IsNullOrWhiteSpace(own))
        {
            parts.Add(own.Trim());
        }

        foreach (VersionedRef reference in card.Packs)
        {
            string? adds = packs.Get(reference.Id)?.ConsultWhenAdds;
            if (!string.IsNullOrWhiteSpace(adds))
            {
                parts.Add(adds.Trim());
            }
        }

        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    /// <summary>Words a hire-limit refusal for the user.</summary>
    private static string Describe(HireRefusal refusal)
    {
        return refusal.Kind == HireLimitKind.PerTeam
            ? $"Team '{refusal.Team}' is full: {refusal.Used} of {refusal.Limit} Teammates."
            : $"{refusal.Used} of {refusal.Limit} agent processes are running, so no more Teammates can be hired.";
    }

    /// <summary>Builds the result of a refused hire.</summary>
    private static HireResult Refuse(string error)
    {
        return new HireResult(false, error, null);
    }
}
