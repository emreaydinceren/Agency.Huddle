using MudBlazor;

namespace Agency.Huddle.App.Components.Layout;

/// <summary>
/// What the Sidebar (<c>S2</c>) shows. The Activity bar (<c>S5</c>) selects one, as VS Code's Activity Bar
/// selects a view container. Public because <see cref="ActivityBar"/> takes it as a <c>[Parameter]</c>.
/// </summary>
public enum SidebarView
{
    /// <summary>The Needs You card and the Rooms that belong to no Team.</summary>
    Chats,

    /// <summary>The Teams, each with its Rooms and Projects.</summary>
    Teams,

    /// <summary>The link to the Teammates page.</summary>
    Teammates,

    /// <summary>The saved Task Views.</summary>
    Tasks,

    /// <summary>The link to the Library page.</summary>
    Library,
}

/// <summary>One button in the Activity bar's top toolbar: the view it selects, its tooltip and label, and its icon.</summary>
/// <param name="View">The Sidebar view the button selects.</param>
/// <param name="Label">The tooltip and <c>aria-label</c>.</param>
/// <param name="Icon">The entity's icon from design-system.md "Icons".</param>
internal sealed record SidebarViewItem(SidebarView View, string Label, string Icon);

/// <summary>The Activity bar's rules, kept pure so they are tested without rendering.</summary>
internal static class SidebarViews
{
    /// <summary>The view the Sidebar opens on.</summary>
    public const SidebarView Default = SidebarView.Chats;

    private static readonly SidebarViewItem[] All =
    [
        new(SidebarView.Chats, "Chats", Icons.Material.Filled.Chat),
        new(SidebarView.Teams, "Teams", Icons.Material.Outlined.Groups),
        new(SidebarView.Teammates, "Teammates", Icons.Material.Filled.Group),
        new(SidebarView.Tasks, "Tasks", Icons.Material.Outlined.TaskAlt),
        new(SidebarView.Library, "Library", Icons.Material.Filled.MenuBook),
    ];

    /// <summary>
    /// The buttons to draw, top to bottom. Tasks and Library are left out when their feature is off, because
    /// their views would then be empty.
    /// </summary>
    /// <param name="options">The installation's options.</param>
    public static IReadOnlyList<SidebarViewItem> Available(TeamOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return [.. All.Where(item => item.View switch
        {
            SidebarView.Tasks => options.Tasks.Enabled,
            SidebarView.Library => options.Library.Enabled,
            _ => true,
        })];
    }

    /// <summary>
    /// The view after a click on <paramref name="clicked"/>: that view, or <see langword="null"/> (the Sidebar
    /// collapsed) when it was already the selected one.
    /// </summary>
    /// <param name="current">The selected view, or <see langword="null"/> while the Sidebar is collapsed.</param>
    /// <param name="clicked">The view whose button was clicked.</param>
    public static SidebarView? Next(SidebarView? current, SidebarView clicked) => current == clicked ? null : clicked;
}