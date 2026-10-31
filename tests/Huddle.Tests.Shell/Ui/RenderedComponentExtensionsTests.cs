using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Agency.Huddle.App.Tabs;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Pins the shared bUnit queries and hosts of <c>Common/Ui</c>: <see cref="RenderedComponentExtensions"/>,
/// <see cref="DialogExtensions"/>, <see cref="UiTestHost"/> and <c>RenderWithPopovers&lt;T&gt;</c>, against
/// one small component that holds the pieces they query.
/// </summary>
public sealed class RenderedComponentExtensionsTests
{
    /// <summary>
    /// A test-only component holding two buttons with the same label, a button that has only an aria-label,
    /// a button with a unique label, and a <see cref="MudMenu"/> with one item that bumps a counter.
    /// </summary>
    private sealed class Probe : ComponentBase
    {
        private int renamed;

        /// <inheritdoc />
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "button");
            builder.AddAttribute(1, "class", "first");
            builder.AddContent(2, " Go ");
            builder.CloseElement();

            builder.OpenElement(3, "button");
            builder.AddAttribute(4, "class", "second");
            builder.AddContent(5, "Go");
            builder.CloseElement();

            builder.OpenElement(6, "button");
            builder.AddAttribute(7, "aria-label", "Close");
            builder.CloseElement();

            builder.OpenElement(8, "button");
            builder.AddContent(9, "Solo");
            builder.CloseElement();

            builder.OpenElement(10, "span");
            builder.AddAttribute(11, "class", "renamed");
            builder.AddContent(12, this.renamed);
            builder.CloseElement();

            builder.OpenComponent<MudMenu>(13);
            builder.AddAttribute(14, nameof(MudMenu.Label), "Actions");
            builder.AddAttribute(15, nameof(MudMenu.ChildContent), (RenderFragment)(menu =>
            {
                menu.OpenComponent<MudMenuItem>(16);
                menu.AddAttribute(17, nameof(MudMenuItem.OnClick), EventCallback.Factory.Create<MouseEventArgs>(this, () => this.renamed++));
                menu.AddAttribute(18, nameof(MudMenuItem.ChildContent), (RenderFragment)(item => item.AddContent(19, "Rename")));
                menu.CloseComponent();
            }));
            builder.CloseComponent();
        }
    }

    /// <summary>A dialog body holding a <see cref="MudDialog"/> with a recognisable paragraph.</summary>
    private sealed class ProbeDialog : ComponentBase
    {
        /// <inheritdoc />
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<MudDialog>(0);
            builder.AddAttribute(1, nameof(MudDialog.DialogContent), (RenderFragment)(body =>
            {
                body.OpenElement(2, "p");
                body.AddAttribute(3, "class", "probe-body");
                body.AddContent(4, "Hello from the dialog");
                body.CloseElement();
            }));
            builder.CloseComponent();
        }
    }

    /// <summary>Two buttons share the label, so the exactly-one lookup throws rather than picking one.</summary>
    [Fact]
    public async Task FindButton_TwoMatches_Throws()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers<Probe>();

        Assert.Throws<InvalidOperationException>(() => cut.FindButton("Go"));
    }

    /// <summary>A label held by one button is found, with the button's own padding trimmed away.</summary>
    [Fact]
    public async Task FindButton_OneMatch_ReturnsIt()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers<Probe>();

        Assert.Equal("Solo", cut.FindButton("Solo").TextContent);
    }

    /// <summary>The first-match lookup does not throw on two matches and returns the first in document order.</summary>
    [Fact]
    public async Task FindFirstButton_TwoMatches_ReturnsTheFirst()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers<Probe>();

        Assert.Equal("first", cut.FindFirstButton("Go").ClassName);
    }

    /// <summary>A button with no text is found by its aria-label only when the caller asks for it.</summary>
    [Fact]
    public async Task FindButton_OrAriaLabel_MatchesOnlyWhenAsked()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers<Probe>();

        Assert.Equal("Close", cut.FindButton("Close", orAriaLabel: true).GetAttribute("aria-label"));
        Assert.Throws<InvalidOperationException>(() => cut.FindButton("Close"));
    }

    /// <summary>The text of the first element matching a selector comes back trimmed.</summary>
    [Fact]
    public async Task TextOf_Selector_ReturnsTrimmedText()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers<Probe>();

        Assert.Equal("Go", cut.TextOf("button.first"));
    }

    /// <summary>A closed menu has no items; opening it paints its items through the popover provider.</summary>
    [Fact]
    public async Task MenuItems_AfterOpening_ListsTheItems()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers<Probe>();
        Assert.Empty(cut.MenuItems());

        await cut.InvokeAsync(() => cut.FindButton("Actions").ClickAsync());

        Assert.Equal("Rename", Assert.Single(cut.MenuItems()).TextContent.Trim());
    }

    /// <summary>Clicking a menu item by its text runs the handler to completion before the call returns.</summary>
    [Fact]
    public async Task ClickMenuItemAsync_RunsTheHandler()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers<Probe>();
        await cut.InvokeAsync(() => cut.FindButton("Actions").ClickAsync());

        await cut.ClickMenuItemAsync("Rename");

        Assert.Equal("1", cut.TextOf("span.renamed"));
    }

    /// <summary>A menu item that is not there fails loudly instead of clicking nothing.</summary>
    [Fact]
    public async Task ClickMenuItemAsync_UnknownItem_Throws()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers<Probe>();
        await cut.InvokeAsync(() => cut.FindButton("Actions").ClickAsync());

        await Assert.ThrowsAsync<InvalidOperationException>(() => cut.ClickMenuItemAsync("Delete"));
    }

    /// <summary>A dialog shown through the helper is on screen, with its body, when the call returns.</summary>
    [Fact]
    public async Task ShowDialogAsync_ShowsTheDialogAndWaits()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers<Probe>();

        IDialogReference reference = await ctx.ShowDialogAsync(cut, "Probe", new DialogParameters<ProbeDialog>(), waitSelector: ".probe-body");

        Assert.NotNull(reference);
        Assert.Equal("Hello from the dialog", cut.TextOf(".probe-body"));
    }

    /// <summary>The host adds a <see cref="TabService"/> once, and a second <c>WithTabs</c> keeps the first.</summary>
    [Fact]
    public async Task UiTestHost_WithTabs_ResolvesOneTabService()
    {
        await using MudBunitContext ctx = UiTestHost.Create().WithTabs().WithTabs();

        TabService first = ctx.Services.GetRequiredService<TabService>();

        Assert.Same(first, ctx.Services.GetRequiredService<TabService>());
        Assert.Single(ctx.Services.GetServices<TabService>());
    }
}
