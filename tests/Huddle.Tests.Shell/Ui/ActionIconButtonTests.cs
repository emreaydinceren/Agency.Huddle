using AngleSharp.Dom;
using Bunit;
using Agency.Huddle.App.Components.Shared;
using MudBlazor;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// The one icon button (ComponentCatalog, ActionIconButton): always Small and named by its label, paired with its tooltip when
/// given one, a toggle when <c>Pressed</c> is set, and nameless never.
/// </summary>
public sealed class ActionIconButtonTests
{
    /// <summary>The button is Small, carries its label as <c>aria-label</c>, the caller's hook and any other attribute.</summary>
    [Fact]
    public async Task Render_Plain_IsSmallAndNamed()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<ActionIconButton> cut = ctx.Render<ActionIconButton>(parameters => parameters
            .Add(static p => p.Icon, Icons.Material.Filled.Close)
            .Add(static p => p.Label, "Close")
            .Add(static p => p.Class, "library-document-close")
            .AddUnmatched("data-md-command", "bold"));

        IElement button = cut.Find("button");
        Assert.Equal("Close", button.GetAttribute("aria-label"));
        Assert.Equal("bold", button.GetAttribute("data-md-command"));
        Assert.Contains("mud-icon-button-size-small", button.ClassList);
        Assert.Contains("library-document-close", button.ClassList);
        Assert.Null(button.GetAttribute("aria-pressed"));
        Assert.Empty(cut.FindComponents<MudTooltip>());
    }

    /// <summary>A tooltip text wraps the button in one tooltip with the caller's placement.</summary>
    [Fact]
    public async Task Render_WithTooltip_PairsTheTooltip()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<ActionIconButton> cut = ctx.Render<ActionIconButton>(parameters => parameters
            .Add(static p => p.Icon, Icons.Material.Filled.PersonAddAlt)
            .Add(static p => p.Label, "Invite agent")
            .Add(static p => p.Tooltip, "Invite agent")
            .Add(static p => p.TooltipPlacement, Placement.Start));

        MudTooltip tooltip = Assert.Single(cut.FindComponents<MudTooltip>()).Instance;
        Assert.Equal("Invite agent", tooltip.Text);
        Assert.Equal(Placement.Start, tooltip.Placement);
    }

    /// <summary>A toggle writes <c>aria-pressed</c> and wears the accent while pressed.</summary>
    /// <param name="pressed">Whether the toggle is on.</param>
    /// <param name="aria">The <c>aria-pressed</c> value it must write.</param>
    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public async Task Render_Pressed_WritesAriaPressed(bool pressed, string aria)
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<ActionIconButton> cut = ctx.Render<ActionIconButton>(parameters => parameters
            .Add(static p => p.Icon, Icons.Material.Outlined.Notifications)
            .Add(static p => p.Label, "Notifications")
            .Add(static p => p.Pressed, pressed));

        Assert.Equal(aria, cut.Find("button").GetAttribute("aria-pressed"));
        Assert.Equal(pressed ? Color.Primary : Color.Inherit, cut.FindComponent<MudIconButton>().Instance.Color);
    }

    /// <summary>A button with no label throws on its first render, because an icon-only button has no other name.</summary>
    [Fact]
    public async Task Render_NoLabel_Throws()
    {
        await using MudBunitContext ctx = new();

        Assert.Throws<InvalidOperationException>(() => ctx.Render<ActionIconButton>(parameters => parameters
            .Add(static p => p.Icon, Icons.Material.Filled.Close)));
    }
}
