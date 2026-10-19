namespace Agency.Huddle.App.Components.Layout;

/// <summary>One button in the Activity bar's top toolbar: the view it selects, its tooltip and label, and its icon.</summary>
/// <param name="View">The Sidebar view the button selects.</param>
/// <param name="Label">The tooltip and <c>aria-label</c>.</param>
/// <param name="Icon">The entity's icon from design-system.md "Icons".</param>
internal sealed record SidebarViewItem(SidebarView View, string Label, string Icon);