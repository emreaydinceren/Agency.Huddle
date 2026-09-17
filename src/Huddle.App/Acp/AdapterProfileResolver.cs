namespace Agency.Huddle.App.Acp;

/// <summary>
/// Turns a Persona's Adapter id into a profile. Never fails; see Spec §6.2. Returning a tuple
/// rather than throwing is P4 (Spec §4, "Degrade, never reject") and mirrors
/// <c>PersonaRunner</c>'s existing treatment of an unadvertised Model: the session starts, the
/// Teammate reports <c>Degraded</c>, and the human sees why. This type produces the warning text
/// only — surfacing it through <c>PersonaSupervisor</c>'s
/// <c>RaiseStatusChanged(PersonaState.Degraded, …)</c> path is that type's responsibility, not
/// this one's.
/// </summary>
/// <param name="catalog">The Adapters this installation can launch.</param>
internal sealed class AdapterProfileResolver(AdapterCatalog catalog)
{
    /// <summary>
    /// Resolves an Adapter id to a profile. Pure and synchronous — it never touches the
    /// filesystem (Spec §6.2, Constraints). An Adapter that is configured but not installed is
    /// discovered later, by <c>AgentProcessOptionsFactory</c> returning <see langword="null"/>.
    /// </summary>
    /// <param name="adapterId">The Persona's configured Adapter id, or <see langword="null"/>.</param>
    /// <returns>
    /// The matching profile with no warning; the default profile with no warning when
    /// <paramref name="adapterId"/> is null or whitespace; or the default profile with a warning
    /// naming both ids when <paramref name="adapterId"/> does not match any configured profile.
    /// </returns>
    internal (AdapterProfile Profile, string? Warning) Resolve(string? adapterId)
    {
        if (string.IsNullOrWhiteSpace(adapterId))
        {
            return (catalog.Default, null);
        }

        AdapterProfile? match = catalog.Find(adapterId);
        if (match is not null)
        {
            return (match, null);
        }

        string warning = $"The Adapter '{adapterId}' is not configured; running on '{catalog.Default.Id}'.";
        return (catalog.Default, warning);
    }
}
