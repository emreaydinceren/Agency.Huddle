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

    /// <summary>The link to the Agents page.</summary>
    Agents,

    /// <summary>The saved Task Views.</summary>
    Tasks,

    /// <summary>The link to the Library page.</summary>
    Library,
}