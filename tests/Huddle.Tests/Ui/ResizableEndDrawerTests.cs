using Bunit;
using Agency.Huddle.App.Components.Shared;
using Microsoft.AspNetCore.Components;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Renders <see cref="ResizableEndDrawer"/> on its own. The shared end drawer behind the Task detail panel and the View
/// editor has a header with its title and a close button, content in <c>pa-4</c> with a 16px gap, and one resize handle
/// that the keyboard can reach (design-system "Drawer anatomy"; UX-048, UX-R05, UX-V10).
/// </summary>
public sealed class ResizableEndDrawerTests
{
    private const string Key = "testDrawerWidth";

    private static IRenderedComponent<ResizableEndDrawer> RenderDrawer(
        MudBunitContext ctx,
        string title = "Edit view",
        Action<bool>? onOpenChanged = null)
    {
        RenderFragment content = builder => builder.AddMarkupContent(0, "<span class=\"drawer-probe\">probe</span>");
        return ctx.Render<ResizableEndDrawer>(parameters => parameters
            .Add(p => p.StorageKey, Key)
            .Add(p => p.Title, title)
            .Add(p => p.Open, true)
            .Add(p => p.OpenChanged, EventCallback.Factory.Create<bool>(ctx, open => onOpenChanged?.Invoke(open)))
            .Add(p => p.ChildContent, content));
    }

    /// <summary>The title renders in the header as a Typo.h6, with the text verbatim.</summary>
    [Fact]
    public async Task Header_ShowsTheTitleAsH6()
    {
        await using MudBunitContext ctx = new();

        var cut = RenderDrawer(ctx, "New view");

        var title = cut.Find(".mud-typography-h6");
        Assert.Equal("New view", title.TextContent);
    }

    /// <summary>The header carries a close button labelled Close.</summary>
    [Fact]
    public async Task Header_HasCloseButton()
    {
        await using MudBunitContext ctx = new();

        var cut = RenderDrawer(ctx);

        var close = cut.Find("button[aria-label='Close']");
        Assert.NotNull(close);
    }

    /// <summary>Pressing the close button asks the owner to close the drawer.</summary>
    [Fact]
    public async Task CloseButton_RaisesOpenChangedFalse()
    {
        await using MudBunitContext ctx = new();
        List<bool> raised = [];
        var cut = RenderDrawer(ctx, onOpenChanged: raised.Add);

        await cut.Find("button[aria-label='Close']").ClickAsync();

        Assert.Equal([false], raised);
    }

    /// <summary>The child content sits in a pa-4 stack with a 16px (gap-4) gap, as dialogs and the page do.</summary>
    [Fact]
    public async Task Content_HasPa4AndGap4()
    {
        await using MudBunitContext ctx = new();

        var cut = RenderDrawer(ctx);

        var stack = cut.Find(".drawer-probe").ParentElement;
        Assert.NotNull(stack);
        Assert.Contains("pa-4", stack.ClassList);
        Assert.Contains("gap-4", stack.ClassList);
    }

    /// <summary>The resize handle is a vertical separator that the Tab key reaches.</summary>
    [Fact]
    public async Task Handle_IsFocusableVerticalSeparator()
    {
        await using MudBunitContext ctx = new();

        var cut = RenderDrawer(ctx);

        var handle = cut.Find("[role='separator']");
        Assert.Equal("0", handle.GetAttribute("tabindex"));
        Assert.Equal("vertical", handle.GetAttribute("aria-orientation"));
    }

    /// <summary>The handle declares the 320 to 800 range and the 420 starting width as aria-value attributes.</summary>
    [Fact]
    public async Task Handle_DeclaresValueRange()
    {
        await using MudBunitContext ctx = new();

        var cut = RenderDrawer(ctx);

        var handle = cut.Find("[role='separator']");
        Assert.Equal("320", handle.GetAttribute("aria-valuemin"));
        Assert.Equal("800", handle.GetAttribute("aria-valuemax"));
        Assert.Equal("420", handle.GetAttribute("aria-valuenow"));
    }

    /// <summary>The first render attaches the drag and keyboard handlers with the storage key and the four width constants.</summary>
    [Fact]
    public async Task FirstRender_AttachesDrawerWithKeyAndWidths()
    {
        await using MudBunitContext ctx = new();

        RenderDrawer(ctx);

        var invocation = ctx.JSInterop.VerifyInvoke("huddleResize.attachDrawer");
        Assert.Equal(Key, invocation.Arguments[1]);
        Assert.Equal(320, Convert.ToInt32(invocation.Arguments[2], System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(800, Convert.ToInt32(invocation.Arguments[3], System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(420, Convert.ToInt32(invocation.Arguments[4], System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>An eyebrow renders as a caption line above the title; without one no eyebrow element is drawn (S6).</summary>
    [Fact]
    public async Task Header_Eyebrow_RendersAboveTheTitleOnlyWhenGiven()
    {
        await using MudBunitContext ctx = new();
        RenderFragment content = builder => builder.AddMarkupContent(0, "<span></span>");

        var with = ctx.Render<ResizableEndDrawer>(parameters => parameters
            .Add(p => p.StorageKey, Key).Add(p => p.Title, "Ship it").Add(p => p.Eyebrow, "PLAT-0013 · Platform").Add(p => p.Open, true).Add(p => p.ChildContent, content));

        Assert.Equal("PLAT-0013 · Platform", with.Find(".resizable-drawer-eyebrow").TextContent);
        Assert.Equal("Ship it", with.Find(".resizable-drawer-title").TextContent);
        Assert.Empty(RenderDrawer(ctx).FindAll(".resizable-drawer-eyebrow"));
    }
}
