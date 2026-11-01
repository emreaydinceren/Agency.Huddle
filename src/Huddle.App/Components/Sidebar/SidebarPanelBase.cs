using Agency.Huddle.App.Sidebar;
using Microsoft.AspNetCore.Components;

namespace Agency.Huddle.App.Components.Sidebar;

/// <summary>
/// The three parameters every Right sidebar panel declares. <c>RightSidebar</c> hands a panel its Subject and Tab
/// id by name through a <c>DynamicComponent</c> dictionary built from <see cref="Subject"/> and <see cref="TabId"/>,
/// so deriving from this class is what keeps a panel and its host agreeing on those names. A panel derives from
/// it and says what its Subject must be to draw anything. A panel that already has another base class declares
/// the same two parameters itself, and the panel registration test checks that it did.
/// </summary>
public abstract class SidebarPanelBase : ComponentBase
{
    /// <summary>What the page shows. A panel draws only for the Subject kinds it applies to and renders nothing for any other.</summary>
    [Parameter, EditorRequired]
    public required PageSubject Subject { get; set; }

    /// <summary>The Tab this panel belongs to, for the commands it sends to that Tab's page.</summary>
    [Parameter, EditorRequired]
    public required string TabId { get; set; }

    /// <summary>The frame's context, through which the panel reports its count to the header. Null outside a frame.</summary>
    [CascadingParameter]
    public SidebarPanelContext? Panel { get; set; }
}
