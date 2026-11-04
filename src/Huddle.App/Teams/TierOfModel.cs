using Agency.Huddle.App.TeamTypes;

namespace Agency.Huddle.App.Teams;

/// <summary>Which tier an Agent's chip should say: its role's tier only until the Human picks a Model that tier does not name.</summary>
internal static class TierOfModel
{
    /// <summary>The tier to show for an Agent.</summary>
    /// <param name="roleTier">The tier its role asks for, or <see langword="null"/> when it has no role.</param>
    /// <param name="model">The Model it runs on, or <see langword="null"/> for its Adapter's default.</param>
    /// <param name="tierMap">The models each Adapter's tiers name.</param>
    /// <returns>The role's tier when it names the Model or no Model is chosen, the tier that does name it, or <see langword="null"/> when none does, because no chip beats a wrong one.</returns>
    public static ModelTier? Of(ModelTier? roleTier, string? model, IReadOnlyDictionary<string, IReadOnlyDictionary<ModelTier, string>> tierMap)
    {
        ArgumentNullException.ThrowIfNull(tierMap);

        if (roleTier is null || string.IsNullOrWhiteSpace(model))
        {
            return roleTier;
        }

        List<ModelTier> naming = [.. tierMap.Values
            .SelectMany(static tiers => tiers)
            .Where(tier => string.Equals(tier.Value, model, StringComparison.OrdinalIgnoreCase))
            .Select(static tier => tier.Key)];
        if (naming.Contains(roleTier.Value))
        {
            return roleTier;
        }

        return naming.Count > 0 ? naming[0] : null;
    }
}
