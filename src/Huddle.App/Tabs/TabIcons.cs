using Agency.Huddle.App.Components.Shared;
using MudBlazor;

namespace Agency.Huddle.App.Tabs;

/// <summary>Maps the symbolic icon names a <see cref="Tab"/> stores to MudBlazor icons, so the stored form never holds SVG.</summary>
internal static class TabIcons
{
    /// <summary>The icon of the Tab menu's Move Left item: the Material Symbol, which MudBlazor's own set lacks.</summary>
    public const string MoveSelectionLeft = MaterialSymbols.MoveSelectionLeft;

    /// <summary>The icon of the Tab menu's Move Right item.</summary>
    public const string MoveSelectionRight = MaterialSymbols.MoveSelectionRight;

    /// <summary>The MudBlazor icon for <paramref name="name"/>, or the generic page icon for a name it does not know.</summary>
    /// <param name="name">A name from <see cref="TabDescriptor.Icon"/>.</param>
    public static string For(string name) => name switch
    {
        "chat" => ActionIcons.Chat,
        "team" => Icons.Material.Outlined.Groups,
        "project" => Icons.Material.Filled.LibraryBooks,
        "tasks" => Icons.Material.Outlined.Checklist,
        "agents" => Icons.Material.Filled.Group,
        "settings" => Icons.Material.Outlined.Settings,
        "library" => MaterialSymbols.Newsstand,
        "needs-you" => Icons.Material.Filled.NotificationsActive,
        "feed" => Icons.Material.Outlined.Feed,
        "scheduled" => Icons.Material.Outlined.Schedule,
        _ => Icons.Material.Filled.Description,
    };
}
