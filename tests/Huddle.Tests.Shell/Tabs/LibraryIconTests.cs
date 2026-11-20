using Agency.Huddle.App.Components.Layout;
using MudBlazor;

namespace Agency.Huddle.App.Tabs;

/// <summary>Pins that the Library is drawn with one icon in the Activity bar and in a Library Tab, the one design-system.md names (#714).</summary>
public sealed class LibraryIconTests
{
    /// <summary>The Activity bar's Library tool and a Library Tab both use the Outlined MenuBook icon.</summary>
    [Fact]
    public void Library_ActivityBarAndTab_ShareTheMenuBookIcon()
    {
        SidebarViewItem tool = Assert.Single(SidebarViews.Available(new TeamOptions()), item => item.View == SidebarView.Library);

        Assert.Equal(Icons.Material.Outlined.MenuBook, tool.Icon);
        Assert.Equal(Icons.Material.Outlined.MenuBook, TabIcons.For("library"));
    }
}
