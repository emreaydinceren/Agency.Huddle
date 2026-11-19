namespace Agency.Huddle.App.Components;

/// <summary>
/// When a <c>RowActionsMenu</c> shows its ⋮ activator (design-system.md Menus). Public because it is a component parameter's type;
/// in <c>Agency.Huddle.App.Components</c>, like <see cref="ButtonRole"/>, because a public type in a namespace ending <c>Shared</c>
/// fails CA1716.
/// </summary>
public enum RowActionsReveal
{
    /// <summary>
    /// Hidden until the row is hovered or holds keyboard focus, and always shown on a device with no hover. The row is a
    /// <c>.hover-reveal-row</c> (the sidebar) or carries <c>.row-actions-host</c> (a table row or a card); the activator
    /// stays in the tab order either way.
    /// </summary>
    OnHover,

    /// <summary>Always shown, for a menu that is not on a row: a page header's, a board column's or a roster card's.</summary>
    Always,
}
