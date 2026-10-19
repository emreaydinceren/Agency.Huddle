using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// An <see cref="ISprintCatalog"/> with no Sprints, for tests that build a <see cref="TaskService"/> and
/// never set a Sprint. Tests that exercise Sprints use a real <see cref="SprintStore"/> instead.
/// </summary>
internal sealed class EmptySprintCatalog : ISprintCatalog
{
    /// <summary>Never raised: this catalogue never changes.</summary>
#pragma warning disable CS0067 // Required by ISprintCatalog; this catalogue never changes, so the event is never raised.
    public event Action? SprintsChanged;
#pragma warning restore CS0067

    /// <summary>Always empty.</summary>
    public IReadOnlyList<Sprint> All => [];

    /// <summary>Always <see langword="null"/>.</summary>
    public Sprint? Current => null;

    /// <summary>Always <see langword="null"/>.</summary>
    /// <param name="id">Ignored.</param>
    public Sprint? Get(SprintId id) => null;

    /// <summary>Always <see langword="null"/>.</summary>
    /// <param name="name">Ignored.</param>
    public Sprint? FindByName(string name) => null;
}
