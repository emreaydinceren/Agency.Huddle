namespace Agency.Huddle.App.Profile;

/// <summary>
/// A fixed <see cref="IHumanIdentity"/> with no Alias, used where no <see cref="HumanProfileStore"/>
/// is supplied - the default of the optional identity parameter on services built by hand, such as
/// in tests and the seeder. It never changes, so <see cref="ProfileChanged"/> is never raised.
/// </summary>
/// <param name="name">The Human's Name.</param>
public sealed class ConfiguredHumanIdentity(string name) : IHumanIdentity
{
    /// <inheritdoc />
    public string Name { get; } = name;

    /// <inheritdoc />
    public HumanProfile Current => new(this.Name, null, null, null);

    /// <inheritdoc />
    public bool IsSaved => true;

    /// <inheritdoc />
    public event Action? ProfileChanged
    {
        add { /* Never raised: this identity never changes. */ }
        remove { /* Nothing was stored by add. */ }
    }

    /// <inheritdoc />
    public bool Matches(string? nameOrAlias) => string.Equals(nameOrAlias, this.Name, StringComparison.OrdinalIgnoreCase);
}
