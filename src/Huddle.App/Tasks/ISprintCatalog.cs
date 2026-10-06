namespace Agency.Huddle.App.Tasks;

/// <summary>
/// The read-only face of the Sprint store (Sprints spec §7.3). <c>TaskService</c>, the task tools and the
/// pages that only show Sprints depend on this, so an Agent cannot create or remove a Sprint.
/// </summary>
public interface ISprintCatalog
{
    /// <summary>Raised after the set of Sprints changed, outside any lock.</summary>
    event Action? SprintsChanged;

    /// <summary>Every Sprint, oldest first (by number).</summary>
    IReadOnlyList<Sprint> All { get; }

    /// <summary>The Sprint with the highest number, or <see langword="null"/> when there are none.</summary>
    Sprint? Current { get; }

    /// <summary>Finds a Sprint by id.</summary>
    /// <param name="id">The id to look up.</param>
#pragma warning disable CA1716 // The spec (§7.3) fixes the name Get; the catalogue is consumed from C# only, so the VB keyword clash is harmless.
    Sprint? Get(SprintId id);
#pragma warning restore CA1716

    /// <summary>Finds a Sprint by name, comparing with <see cref="StringComparison.OrdinalIgnoreCase"/>.</summary>
    /// <param name="name">The name to look up.</param>
    Sprint? FindByName(string name);
}
