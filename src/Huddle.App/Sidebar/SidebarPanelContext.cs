using Agency.Huddle.App.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.App.Sidebar;

/// <summary>
/// What the host cascades to a Right sidebar panel (Spec D-3): the panel reports its count here and the
/// host's frame draws it in the header.
/// </summary>
/// <param name="logger">Reports a <see cref="Changed"/> subscriber that threw; defaults to a logger that discards.</param>
public sealed class SidebarPanelContext(ILogger<SidebarPanelContext>? logger = null)
{
    private readonly ILogger<SidebarPanelContext> logger = logger ?? NullLogger<SidebarPanelContext>.Instance;

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
        EventRaise.Isolated(this.Changed, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.Changed)));
    }
}
