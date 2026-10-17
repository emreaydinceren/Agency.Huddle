namespace Agency.Huddle.App.Sidebar;

/// <summary>
/// What the host cascades to a Right sidebar panel (Spec D-3): the panel reports its count here and the
/// host's frame draws it in the header.
/// </summary>
public sealed class SidebarPanelContext
{
    /// <summary>Raised after <see cref="Count"/> changes.</summary>
    public event Action? Changed;

    /// <summary>The count the panel reported, or null when it shows none.</summary>
    public int? Count { get; private set; }

    /// <summary>Reports the panel's count; an equal repeat does nothing and raises nothing.</summary>
    /// <param name="count">The count to show in the header, or null for none.</param>
    public void SetCount(int? count)
    {
        if (this.Count == count)
        {
            return;
        }

        this.Count = count;
        this.Changed?.Invoke();
    }
}
