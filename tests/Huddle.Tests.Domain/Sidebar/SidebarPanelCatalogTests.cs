using Agency.Huddle.App;
using Agency.Huddle.App.Sidebar;
using Microsoft.Extensions.Options;
using Agency.Huddle.Tests.Factories;

namespace Agency.Huddle.Tests.Sidebar;

/// <summary>Pins the panel selection of Spec D-3: a pure function over registered panels and the bound options.</summary>
public sealed class SidebarPanelCatalogTests
{
    /// <summary>For a room, only the panels that apply to rooms are returned, in Order.</summary>
    [Fact]
    public void For_RoomSubject_ReturnsRoomPanelsInOrder()
    {
        SidebarPanel documentOnly = Panel("a", 30, static subject => subject is DocumentSubject);
        SidebarPanel second = Panel("c", 20, static subject => subject is RoomSubject);
        SidebarPanel first = Panel("b", 10, static subject => subject is RoomSubject);
        SidebarPanelCatalog catalog = new([documentOnly, second, first], TestOptions.Create());

        IReadOnlyList<SidebarPanel> panels = catalog.For(new RoomSubject("ab12"));

        Assert.Equal(expectedRoomPanels, panels.Select(static panel => panel.Id));
    }

    /// <summary>A panel whose feature switch is off is left out, even when it applies to the subject.</summary>
    [Fact]
    public void For_DisabledPanel_IsLeftOut()
    {
        SidebarPanel library = Panel("library", 10, static subject => subject is RoomSubject, static options => options.Library.Enabled);
        TeamOptions options = new();
        options.Library.Enabled = false;
        SidebarPanelCatalog catalog = new([library], Options.Create(options));

        IReadOnlyList<SidebarPanel> panels = catalog.For(new RoomSubject("ab12"));

        Assert.Empty(panels);
    }

    /// <summary>A panel with no feature switch is included when every switch is off.</summary>
    [Fact]
    public void For_NullIsEnabled_IsAlwaysIncluded()
    {
        SidebarPanel always = Panel("always", 10, static subject => subject is RoomSubject);
        TeamOptions options = new();
        options.Library.Enabled = false;
        options.Tasks.Enabled = false;
        SidebarPanelCatalog catalog = new([always], Options.Create(options));

        IReadOnlyList<SidebarPanel> panels = catalog.For(new RoomSubject("ab12"));

        Assert.Single(panels);
    }

    /// <summary>Panels with the same Order come back in the order they were registered.</summary>
    [Fact]
    public void For_EqualOrder_KeepsRegistrationOrder()
    {
        SidebarPanel first = Panel("first", 10, static subject => subject is RoomSubject);
        SidebarPanel second = Panel("second", 10, static subject => subject is RoomSubject);
        SidebarPanelCatalog catalog = new([first, second], TestOptions.Create());

        IReadOnlyList<SidebarPanel> panels = catalog.For(new RoomSubject("ab12"));

        Assert.Equal(expectedRegistrationOrder, panels.Select(static panel => panel.Id));
    }

    /// <summary>The room panel ids in Order, for the registration-order assertion.</summary>
    private static readonly string[] expectedRoomPanels = ["b", "c"];

    /// <summary>The ids of two panels that share an Order, in registration order.</summary>
    private static readonly string[] expectedRegistrationOrder = ["first", "second"];

    /// <summary>Builds a panel descriptor for the catalog tests; only the id, order, applicability and switch matter here.</summary>
    private static SidebarPanel Panel(string id, int order, Func<PageSubject, bool> appliesTo, Func<TeamOptions, bool>? isEnabled = null)
    {
        return new SidebarPanel(id, id, "icon", order, appliesTo, typeof(object), isEnabled);
    }
}
