namespace Agency.Huddle.App.Sidebar;

/// <summary>A Right sidebar panel: a descriptor the catalog filters, and the component that draws its body (Spec D-3).</summary>
/// <param name="Id">Stable id; keys the collapsed state.</param>
/// <param name="Title">The heading the host draws.</param>
/// <param name="Icon">The icon the host draws beside the title.</param>
/// <param name="Order">Sort position; panels with equal Order keep their registration order.</param>
/// <param name="AppliesTo">Whether the panel applies to a Subject.</param>
/// <param name="Component">The component that receives the Subject and the Tab id.</param>
/// <param name="IsEnabled">Null when the panel is always on; otherwise a feature switch read from the bound options.</param>
internal sealed record SidebarPanel(string Id, string Title, string Icon, int Order, Func<PageSubject, bool> AppliesTo, Type Component, Func<TeamOptions, bool>? IsEnabled = null);
