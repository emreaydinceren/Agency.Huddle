using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Agency.Huddle.App.Components.Tasks;

namespace Agency.Huddle.Tests.Ui.Tasks;

/// <summary>Pins <see cref="TaskFilterChip"/>'s two forms: the default "Label: Value ⌄" chip with a menu, and the menu-less pill that only shows a choice made elsewhere and clears it.</summary>
public sealed class TaskFilterChipTests
{
    /// <summary>Without <c>NoMenu</c> the chip is a button with a caret that opens its menu, and it is not a group: the form every Tasks toolbar chip takes.</summary>
    [Fact]
    public async Task Render_Default_DrawsTheMenuAndTheCaret()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(builder => Chip(builder, noMenu: false));

        Assert.NotEmpty(cut.FindAll(".chip-menu"));
        Assert.NotEmpty(cut.FindAll(".filter-chip .chip-trigger .mud-icon-root"));
        Assert.Equal("Team type filter: Books", cut.Find(".chip-trigger").GetAttribute("aria-label"));
        Assert.Null(cut.Find(".filter-chip").GetAttribute("role"));
    }

    /// <summary>
    /// With <c>NoMenu</c> the same pill is drawn with no caret and no menu, its choices are not drawn, its text is hidden from assistive
    /// technology (the group around it names the filter), and its look is the set Filter chip's.
    /// </summary>
    [Fact]
    public async Task Render_NoMenu_DrawsThePillWithoutACaretOrAMenu()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(builder => Chip(builder, noMenu: true));

        IElement chip = cut.Find(".filter-chip");
        Assert.Contains("is-set", chip.ClassList);
        Assert.Equal("group", chip.GetAttribute("role"));
        Assert.Equal("Team type filter: Books", chip.GetAttribute("aria-label"));
        Assert.Empty(cut.FindAll(".chip-menu"));
        Assert.Empty(cut.FindAll(".chip-trigger"));
        Assert.Empty(cut.FindAll(".choice"));
        IElement label = cut.Find(".mud-chip-content > span[aria-hidden]");
        Assert.Equal("true", label.GetAttribute("aria-hidden"));
        Assert.Equal("Team type:", label.TextContent.Replace("Books", string.Empty, StringComparison.Ordinal).Trim());
        Assert.Equal("Books", cut.Find(".chip-value").TextContent.Trim());
    }

    /// <summary>The menu-less pill keeps its clear button, which raises <c>OnClear</c>.</summary>
    [Fact]
    public async Task Click_NoMenuClearButton_RaisesOnClear()
    {
        await using MudBunitContext ctx = new();
        int cleared = 0;
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(builder => Chip(builder, noMenu: true, onClear: () => cleared++));

        await cut.InvokeAsync(() => cut.Find("[aria-label='Clear Team type filter']").ClickAsync());

        Assert.Equal(1, cleared);
    }

    /// <summary>Draws a set "Team type: Books" chip with one menu choice, in the form the test names.</summary>
    /// <param name="builder">The builder to draw into.</param>
    /// <param name="noMenu">Whether to draw the menu-less pill.</param>
    /// <param name="onClear">Called when the clear button is pressed.</param>
    private static void Chip(RenderTreeBuilder builder, bool noMenu, Action? onClear = null)
    {
        builder.OpenComponent<TaskFilterChip>(0);
        builder.AddAttribute(1, nameof(TaskFilterChip.Label), "Team type");
        builder.AddAttribute(2, nameof(TaskFilterChip.Value), "Books");
        builder.AddAttribute(3, nameof(TaskFilterChip.IsSet), true);
        builder.AddAttribute(4, nameof(TaskFilterChip.NoMenu), noMenu);
        builder.AddAttribute(5, nameof(TaskFilterChip.OnClear), EventCallback.Factory.Create(new object(), () => onClear?.Invoke()));
        builder.AddAttribute(6, nameof(TaskFilterChip.ChildContent), (RenderFragment)(choices => choices.AddMarkupContent(0, "<span class=\"choice\">Other</span>")));
        builder.CloseComponent();
    }
}
