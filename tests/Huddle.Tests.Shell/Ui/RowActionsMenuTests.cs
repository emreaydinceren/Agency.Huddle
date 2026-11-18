using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Agency.Huddle.App.Components;
using Agency.Huddle.App.Components.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// The one row menu (ComponentCatalog, RowActionsMenu): a Dense menu whose ⋮ activator is the round icon button named
/// "Actions for {Name}", revealed on hover or focus unless the caller says always (Q-A3-1, Q-A3-2), and whose items open from it.
/// </summary>
public sealed class RowActionsMenuTests
{
    /// <summary>The activator is the ⋮ icon button named for its row, carrying the reveal class and the caller's hook beside it.</summary>
    [Fact]
    public async Task Render_OnHover_NamesTheRowAndCarriesTheRevealClass()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<RowActionsMenu> cut = ctx.Render<RowActionsMenu>(parameters => parameters
            .Add(static p => p.Name, "Platform")
            .Add(static p => p.ActivatorClass, "teams-nav-scope-menu-button")
            .Add(static p => p.Class, "hover-reveal-menu"));

        IElement button = cut.Find(".hover-reveal-menu button");
        Assert.Equal("Actions for Platform", button.GetAttribute("aria-label"));
        Assert.Contains("hover-reveal-menu-button", button.ClassList);
        Assert.Contains("row-actions-button", button.ClassList);
        Assert.Contains("teams-nav-scope-menu-button", button.ClassList);
        Assert.Contains("mud-icon-button-size-small", button.ClassList);
        Assert.True(cut.FindComponent<MudMenu>().Instance.Dense);
    }

    /// <summary>An always-shown menu carries no reveal class, and a whole label replaces "Actions for {Name}".</summary>
    [Fact]
    public async Task Render_AlwaysWithLabel_IsNotRevealedAndUsesTheLabel()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<RowActionsMenu> cut = ctx.Render<RowActionsMenu>(parameters => parameters
            .Add(static p => p.Label, "Done column menu")
            .Add(static p => p.Reveal, RowActionsReveal.Always));

        IElement button = cut.Find("button");
        Assert.Equal("Done column menu", button.GetAttribute("aria-label"));
        Assert.DoesNotContain("hover-reveal-menu-button", button.ClassList);
    }

    /// <summary>Pressing the activator opens the menu, and an item raises its own handler.</summary>
    [Fact]
    public async Task Activator_Click_OpensTheItems()
    {
        await using MudBunitContext ctx = new();
        int pressed = 0;

        RenderFragment items = builder =>
        {
            builder.OpenComponent<MudMenuItem>(0);
            builder.AddAttribute(1, nameof(MudMenuItem.Class), "row-menu-rename");
            builder.AddAttribute(2, nameof(MudMenuItem.OnClick), EventCallback.Factory.Create<MouseEventArgs>(this, () => pressed++));
            builder.AddAttribute(3, nameof(MudMenuItem.ChildContent), (RenderFragment)(static b => b.AddContent(0, "Rename…")));
            builder.CloseComponent();
        };
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<RowActionsMenu>(0);
            builder.AddAttribute(1, nameof(RowActionsMenu.Name), "Lab");
            builder.AddAttribute(2, nameof(RowActionsMenu.ChildContent), items);
            builder.CloseComponent();
        });

        await cut.InvokeAsync(() => cut.Find("button").ClickAsync());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".row-menu-rename")));
        await cut.InvokeAsync(() => cut.Find(".row-menu-rename").ClickAsync());

        Assert.Equal(1, pressed);
    }

    /// <summary>A menu with neither a name nor a label throws on its first render, so no activator goes unnamed.</summary>
    [Fact]
    public async Task Render_Nameless_Throws()
    {
        await using MudBunitContext ctx = new();

        Assert.Throws<InvalidOperationException>(() => ctx.Render<RowActionsMenu>());
    }
}
