using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// Holds the set of Adapters this installation can launch, and answers which one a Persona
/// means. Frozen at construction from <see cref="AcpOptions.Adapters"/>; when that list is null
/// or empty, exactly one profile is synthesised from the legacy <see cref="AcpOptions.Command"/>,
/// <see cref="AcpOptions.Args"/> and <see cref="AcpOptions.AdapterPath"/> keys, so a stock
/// installation behaves exactly as it does today (Spec §4, P6; Spec §6.1).
/// </summary>
internal sealed class AdapterCatalog
{
    /// <summary>Builds the catalog from configuration, throwing on a misconfigured profile.</summary>
    /// <param name="options">The bound <see cref="TeamOptions"/>, read once at construction.</param>
    public AdapterCatalog(IOptions<TeamOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        AcpOptions acp = options.Value.Acp;
        List<AdapterProfile> profiles = AdapterCatalog.BuildProfiles(acp);
        AdapterCatalog.Validate(profiles);
        this.Profiles = profiles;
    }

    /// <summary>Every configured Adapter, in configuration order.</summary>
    internal IReadOnlyList<AdapterProfile> Profiles { get; }

    /// <summary>The first profile in configuration order.</summary>
    internal AdapterProfile Default => this.Profiles[0];

    /// <summary>Looks up a profile by id, matching <see cref="StringComparer.OrdinalIgnoreCase"/>.</summary>
    /// <param name="id">The id to look up, or <see langword="null"/>.</param>
    /// <returns>The matching profile, or <see langword="null"/> when none matches.</returns>
    internal AdapterProfile? Find(string? id)
    {
        if (id is null)
        {
            return null;
        }

        foreach (AdapterProfile profile in this.Profiles)
        {
            if (string.Equals(profile.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return profile;
            }
        }

        return null;
    }

    private static List<AdapterProfile> BuildProfiles(AcpOptions acp)
    {
        if (acp.Adapters is not { Count: > 0 })
        {
            // No EnvironmentOverrides here, deliberately: the legacy Command/Args/AdapterPath keys
            // describe the Node adapter, which needs no injected configuration. An installation
            // that needs per-process environment declares an Adapters entry instead.
            return
            [
                new AdapterProfile(
                    Id: "claude",
                    DisplayName: "Claude",
                    Description: null,
                    Command: acp.Command,
                    Args: acp.Args,
                    AdapterPath: acp.AdapterPath,
                    UsesToolNamePrefix: true,
                    EnvironmentOverrides: null,
                    ReadsFiles: true,
                    IsolateUserSettings: true,
                    SessionPerRoom: false),
            ];
        }

        List<AdapterProfile> profiles = new(acp.Adapters.Count);
        foreach (AdapterProfileOptions entry in acp.Adapters)
        {
            profiles.Add(new AdapterProfile(
                Id: entry.Id,
                DisplayName: string.IsNullOrWhiteSpace(entry.DisplayName) ? entry.Id : entry.DisplayName,
                Description: entry.Description,
                Command: entry.Command,
                Args: entry.Args,
                AdapterPath: entry.AdapterPath,
                UsesToolNamePrefix: entry.UsesToolNamePrefix,
                EnvironmentOverrides: AdapterCatalog.CopyEnvironment(entry.EnvironmentOverrides),
                ReadsFiles: entry.ReadsFiles,
                IsolateUserSettings: entry.IsolateUserSettings,
                SessionPerRoom: entry.SessionPerRoom));
        }

        return profiles;
    }

    // Copies rather than aliases entry.EnvironmentOverrides: the catalog is frozen at construction
    // and AdapterProfile is handed to a Razor [Parameter], so a profile must not keep pointing at a
    // mutable IOptions-backed dictionary the binder (or a caller) could still change out from under
    // it. Empty normalises to null so AgentProcessLauncher's environment loop is skipped entirely,
    // keeping a stock install's launch byte-identical to before this feature existed.
    private static Dictionary<string, string>? CopyEnvironment(IReadOnlyDictionary<string, string>? overrides) =>
        overrides is { Count: > 0 } ? new Dictionary<string, string>(overrides, StringComparer.Ordinal) : null;

    // Fail-fast at startup, the same shape as the Team:Acp:PersonaDir rename guard in
    // ServiceCollectionExtensions: a misconfigured profile is a startup error, never a first-Turn
    // one (Spec §6.1, Constraints).
    private static void Validate(List<AdapterProfile> profiles)
    {
        HashSet<string> seenIds = new(StringComparer.OrdinalIgnoreCase);
        foreach (AdapterProfile profile in profiles)
        {
            if (string.IsNullOrWhiteSpace(profile.Command))
            {
                throw new InvalidOperationException(
                    $"Adapter '{profile.Id}' has a blank Command. Set 'Team:Acp:Adapters:*:Command' " +
                    "for every configured Adapter.");
            }

            if (!seenIds.Add(profile.Id))
            {
                throw new InvalidOperationException(
                    $"Adapter id '{profile.Id}' is configured more than once under 'Team:Acp:Adapters'. " +
                    "Adapter ids must be unique (compared case-insensitively).");
            }
        }
    }
}
