using MudBlazor;

namespace Agency.Huddle.App.Components.Layout;

/// <summary>The Activity bar's rules, kept pure so they are tested without rendering.</summary>
internal static class SidebarViews
{
    /// <summary>The view the Sidebar opens on.</summary>
    public const SidebarView Default = SidebarView.Chats;

    private static readonly SidebarViewItem[] All =
    [
        new(SidebarView.Chats, "Chats", Icons.Material.Filled.Chat),
        new(SidebarView.Teams, "Teams", Icons.Material.Outlined.Groups),
        new(SidebarView.Agents, "Agents", Icons.Material.Filled.Group),
        new(SidebarView.Tasks, "Tasks", Icons.Material.Outlined.TaskAlt),
        new(SidebarView.Library, "Library", Icons.Material.Filled.MenuBook),
        new(SidebarView.Scheduled, "Scheduled", Icons.Material.Outlined.Schedule),
    ];

    /// <summary>
    /// The buttons to draw, top to bottom. Tasks, Library and Scheduled are left out when their feature is off, because
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
            SidebarView.Scheduled => options.Jobs.Enabled,
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

    /// <summary>
    /// The page area a Tab's address belongs to: the first path segment when it names one of the places the
    /// Activity bar follows (<c>rooms</c>, <c>teams</c>, <c>agents</c>, <c>tasks</c>, <c>library</c>,
    /// <c>scheduled</c>, <c>settings</c>), otherwise <see langword="null"/> (the home page, an unknown address).
    /// </summary>
    /// <param name="url">The site-relative address of the focused pane's active Tab, or <see langword="null"/> when it has none.</param>
    public static string? AreaOf(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        string path = url.TrimStart('/');
        int end = path.IndexOfAny(['/', '?', '#']);
        string first = end < 0 ? path : path[..end];
        return first.ToLowerInvariant() switch
        {
            "rooms" => "rooms",
            "teams" => "teams",
            "agents" => "agents",
            "tasks" => "tasks",
            "library" => "library",
            "scheduled" => "scheduled",
            "settings" => "settings",
            _ => null,
        };
    }

    /// <summary>
    /// The view the Sidebar shows once the focused page moves into <paramref name="area"/> (audit B3a): the area's own
    /// view, except that a Team page keeps a Team shortcut's view open; <see langword="null"/> (the Sidebar collapsed) for
    /// Settings, whose sections are the page's own left menu;
    /// and, for a Room, the current view, or Chats when the Sidebar was collapsed, because a Room is listed under
    /// Chats or under a Team and the Human may be looking at either.
    /// </summary>
    /// <param name="area">An area from <see cref="AreaOf(string?)"/>.</param>
    /// <param name="current">The view selected now, or <see langword="null"/> while the Sidebar is collapsed.</param>
    public static SidebarView? ViewForArea(string area, SidebarView? current) => area switch
    {
        "teams" => current == SidebarView.Team ? SidebarView.Team : SidebarView.Teams,
        "agents" => SidebarView.Agents,
        "tasks" => SidebarView.Tasks,
        "library" => SidebarView.Library,
        "scheduled" => SidebarView.Scheduled,
        "settings" => null,
        _ => current ?? SidebarView.Chats,
    };
}