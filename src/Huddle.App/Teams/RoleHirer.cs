using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Agents;
using Agency.Huddle.App.TeamTypes;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// Hires a Candidate that carries a role (HR-13): the card is resolved from the role through
/// <see cref="CardResolver"/> and written by <see cref="HireService"/>, so the Agent gets the
/// role's skills, packs and model tier instead of the Candidate's own text.
/// </summary>
/// <param name="hires">Writes the Agent.</param>
/// <param name="types">The team types, for the role.</param>
/// <param name="skills">The registry a card draws its Packs and Skills from.</param>
/// <param name="context">The adapters and connectors the card resolves against.</param>
internal sealed class RoleHirer(HireService hires, TeamTypeCatalog types, ISkillAvailability skills, IHireContext context)
{
    /// <summary>
    /// Hires <paramref name="candidate"/> into its first Team as its role. When no tier mapping names a model, the
    /// Adapter's default model is read first (this may probe the Adapter once) and stored, as Build a team does (#623).
    /// </summary>
    /// <param name="candidate">A Candidate with a <see cref="Candidate.Role"/> and at least one Team.</param>
    /// <param name="defaults">The default models learned in this Approve; <see langword="null"/> stores no Model when the tier names none.</param>
    /// <param name="ct">Cancels the default-model probe.</param>
    /// <returns>The hire outcome; a refusal writes nothing.</returns>
    public async Task<HireResult> HireAsync(Candidate candidate, DefaultModelReads? defaults, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        string[] parts = candidate.Role?.Split('/') ?? [];
        TeamTypeManifest? type = parts.Length == 2 ? types.Find(parts[0]) : null;
        RoleSpec? role = type?.Roles.FirstOrDefault(r => string.Equals(r.Id, parts[1], StringComparison.Ordinal));
        if (type is null || role is null || candidate.Teams.Count == 0)
        {
            return new HireResult(false, $"{candidate.Name} does not name a known role and Team.", null);
        }

        CardState card = CardResolver.Resolve(role, [], context.Answered, context.TierMap, context.AvoidAdapters, skills, context.Connectors, candidate.Name);
        if (card.Pick is { Model: null } pick && defaults is not null)
        {
            string? model = await defaults.ReadAsync(pick.AdapterId, ct).ConfigureAwait(false);
            if (model is not null)
            {
                card = card with { Pick = pick with { Model = model } };
            }
        }

        return hires.Hire(new HireRequest(candidate.Teams[0], type.Id, role.Id, card));
    }
}
