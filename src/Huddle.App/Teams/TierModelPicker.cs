using Agency.Huddle.App.TeamTypes;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// Maps a role's tier to an adapter and model (HR-6, MS-3, NF-9, decision D10). Manifests name
/// tiers, never models; <c>Team:Teams:Tiers:&lt;adapterId&gt;:&lt;tier&gt;</c> maps a tier to a model id.
/// </summary>
internal static class TierModelPicker
{
    /// <summary>
    /// Picks an adapter and model for <paramref name="tier"/>. Only adapters in <paramref name="answered"/>
    /// qualify; one outside <paramref name="avoidAdapters"/> is preferred, else any answered adapter is used.
    /// A mapped model the adapter does not list is ignored (the pick carries <see langword="null"/>).
    /// </summary>
    /// <param name="tier">The tier the role asks for.</param>
    /// <param name="answered">The adapters that answered their probe, in preference order.</param>
    /// <param name="tierMap">Configured model ids by adapter id, then tier.</param>
    /// <param name="avoidAdapters">Adapter ids to avoid when another adapter answered.</param>
    /// <returns>The pick, or <see langword="null"/> when no adapter answered.</returns>
    public static ModelPick? Pick(ModelTier tier, IReadOnlyList<AdapterAvailability> answered,
        IReadOnlyDictionary<string, IReadOnlyDictionary<ModelTier, string>> tierMap, IReadOnlyCollection<string> avoidAdapters)
    {
        ArgumentNullException.ThrowIfNull(answered);
        ArgumentNullException.ThrowIfNull(tierMap);
        ArgumentNullException.ThrowIfNull(avoidAdapters);

        AdapterAvailability? chosen = null;
        foreach (AdapterAvailability candidate in answered)
        {
            if (!avoidAdapters.Contains(candidate.AdapterId, StringComparer.OrdinalIgnoreCase))
            {
                chosen = candidate;
                break;
            }
        }

        chosen ??= answered.Count > 0 ? answered[0] : null;
        if (chosen is null)
        {
            return null;
        }

        string? model = null;
        if (tierMap.TryGetValue(chosen.AdapterId, out IReadOnlyDictionary<ModelTier, string>? tiers)
            && tiers.TryGetValue(tier, out string? mapped)
            && chosen.Models.Contains(mapped, StringComparer.Ordinal))
        {
            model = mapped;
        }

        return new ModelPick(chosen.AdapterId, model);
    }
}
