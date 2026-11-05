using Agency.Huddle.App.Profile;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// A fixed <see cref="IHumanIdentity"/> with a Name and an Alias, for a bUnit test that needs the Human to carry a
/// chosen name. It never changes, so <see cref="ProfileChanged"/> is never raised.
/// </summary>
/// <param name="name">The Human's Name.</param>
/// <param name="alias">The Human's Alias, or <see langword="null"/> for none.</param>
internal sealed class AliasedHumanIdentity(string name, string? alias = null) : IHumanIdentity
{
    /// <inheritdoc />
    public string Name { get; } = name;

    /// <inheritdoc />
    public HumanProfile Current => new(this.Name, alias, null, null);

    /// <inheritdoc />
    public bool IsSaved => true;

    /// <inheritdoc />
    public event Action? ProfileChanged
    {
        add { /* Never raised: this identity never changes. */ }
        remove { /* Nothing was stored by add. */ }
    }

    /// <inheritdoc />
    public bool Matches(string? nameOrAlias) =>
        string.Equals(nameOrAlias, this.Name, StringComparison.OrdinalIgnoreCase)
        || (alias is not null && string.Equals(nameOrAlias, alias, StringComparison.OrdinalIgnoreCase));
}
