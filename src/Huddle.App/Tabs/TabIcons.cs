using MudBlazor;

namespace Agency.Huddle.App.Tabs;

/// <summary>Maps the symbolic icon names a <see cref="Tab"/> stores to MudBlazor icons, so the stored form never holds SVG.</summary>
internal static class TabIcons
{
    /// <summary>The MudBlazor icon for <paramref name="name"/>, or the generic page icon for a name it does not know.</summary>
    /// <param name="name">A name from <see cref="TabDescriptor.Icon"/>.</param>
    public static string For(string name) => name switch
    {
        "chat" => Icons.Material.Filled.Chat,
        "team" => Icons.Material.Outlined.Groups,
        "project" => Icons.Material.Filled.LibraryBooks,
        "tasks" => Icons.Material.Outlined.TaskAlt,
        "teammates" => Icons.Material.Filled.Group,
        "settings" => Icons.Material.Filled.Settings,
        "library" => Icons.Material.Filled.MenuBook,
        _ => Icons.Material.Filled.Description,
    };
}
