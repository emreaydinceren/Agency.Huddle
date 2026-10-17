using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Sidebar;

/// <summary>Chooses which Right sidebar panels a Subject gets (Spec D-3): a pure function over the registered panels and the bound options.</summary>
/// <param name="panels">Every registered panel, in registration order.</param>
/// <param name="options">The bound Team options, read for each panel's feature switch.</param>
internal sealed class SidebarPanelCatalog(IEnumerable<SidebarPanel> panels, IOptions<TeamOptions> options)
{
    /// <summary>The registered panels, materialised once so every lookup sees the same list.</summary>
    private readonly IReadOnlyList<SidebarPanel> registered = [.. panels];

    /// <summary>The bound Team options, read on each lookup so a switch change applies to the next one.</summary>
    private readonly IOptions<TeamOptions> teamOptions = options;

    /// <summary>The panels for a Subject: enabled, applicable, and sorted by Order (a stable sort, so ties keep registration order).</summary>
    /// <param name="subject">The Subject the page is showing, or null when it shows none.</param>
    /// <returns>The panels to show, in display order; empty when there is no Subject.</returns>
    internal IReadOnlyList<SidebarPanel> For(PageSubject? subject)
    {
        if (subject is null)
        {
            return [];
        }

        TeamOptions current = this.teamOptions.Value;
        return [.. this.registered
            .Where(panel => (panel.IsEnabled?.Invoke(current) ?? true) && panel.AppliesTo(subject))
            .OrderBy(panel => panel.Order)];
    }
}
