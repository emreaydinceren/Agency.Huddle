using Agency.Huddle.App.Profile;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// An <see cref="IHumanIdentity"/> for the Human "You" whose <see cref="Matches"/> throws for one Name, so a wake rule that asks about
/// that Name fails while every other lookup behaves. It is the collaborator fake that makes a real rule throw (S-B3).
/// </summary>
/// <param name="throwFor">The Name whose lookup throws, compared ignoring case.</param>
internal sealed class ThrowingHumanIdentity(string throwFor) : IHumanIdentity
{
    private readonly ConfiguredHumanIdentity inner = new("You");

    /// <inheritdoc />
    public string Name => this.inner.Name;

    /// <inheritdoc />
    public HumanProfile Current => this.inner.Current;

    /// <inheritdoc />
    public bool IsSaved => this.inner.IsSaved;

    /// <inheritdoc />
    public event Action? ProfileChanged
    {
        add { /* Never raised: this identity never changes. */ }
        remove { /* Nothing was stored by add. */ }
    }

    /// <inheritdoc />
    public bool Matches(string? nameOrAlias) =>
        string.Equals(nameOrAlias, throwFor, StringComparison.OrdinalIgnoreCase)
            ? throw new InvalidOperationException($"The Human lookup for '{nameOrAlias}' failed.")
            : this.inner.Matches(nameOrAlias);
}
