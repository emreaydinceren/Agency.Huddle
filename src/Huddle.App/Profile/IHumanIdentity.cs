namespace Agency.Huddle.App.Profile;

/// <summary>
/// The Human's current identity, read live. Every place that needs the Human's Name reads it here
/// rather than from <see cref="TeamOptions.HumanName"/>, which is only the first-run default: the
/// Human can rename themselves on the Settings Profile tab, and a value copied at startup would go
/// stale.
/// </summary>
public interface IHumanIdentity
{
    /// <summary>The Human's current Name.</summary>
    string Name { get; }

    /// <summary>The whole current profile.</summary>
    HumanProfile Current { get; }

    /// <summary>Whether the Human has saved a profile yet. <see langword="false"/> is the first-run state.</summary>
    bool IsSaved { get; }

    /// <summary>Raised after the profile changes, whether saved from the UI or edited by hand.</summary>
    event Action? ProfileChanged;

    /// <summary>Whether <paramref name="nameOrAlias"/> is the Human's Name or Alias, ignoring case.</summary>
    /// <param name="nameOrAlias">The Name or Alias to test; <see langword="null"/> never matches.</param>
    bool Matches(string? nameOrAlias);
}
