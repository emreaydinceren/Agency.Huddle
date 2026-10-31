using Bunit;
using Agency.Huddle.App.Components.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

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

    /// <summary>Escape on the drawer body asks the owner to close the drawer (theming plan 7.5), so every drawer closes from the keyboard through the owner's own close path.</summary>
    [Fact]
    public async Task Body_Escape_RaisesOpenChangedFalse()
    {
        await using MudBunitContext ctx = new();
        List<bool> raised = [];
        var cut = RenderDrawer(ctx, onOpenChanged: raised.Add);

        await cut.Find(".resizable-drawer-body").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        Assert.Equal([false], raised);
    }

    /// <summary>Any other key on the drawer body leaves the drawer open.</summary>
    [Fact]
    public async Task Body_OtherKey_DoesNotRaiseOpenChanged()
    {
        await using MudBunitContext ctx = new();
        List<bool> raised = [];
        var cut = RenderDrawer(ctx, onOpenChanged: raised.Add);

        await cut.Find(".resizable-drawer-body").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        Assert.Empty(raised);
    }

    /// <summary>The body is focusable by script but not by Tab, so Escape reaches it without adding a tab stop.</summary>
    [Fact]
    public async Task Body_IsProgrammaticallyFocusable()
    {
        await using MudBunitContext ctx = new();

        var cut = RenderDrawer(ctx);

        Assert.Equal("-1", cut.Find(".resizable-drawer-body").GetAttribute("tabindex"));
    }

    /// <summary>Opening the drawer focuses its body, so Escape works without a click first.</summary>
    [Fact]
    public async Task Open_FocusesTheBody()
    {
        await using MudBunitContext ctx = new();
        RenderFragment content = builder => builder.AddMarkupContent(0, "<span></span>");

        _ = ctx.Render<ResizableEndDrawer>(parameters => parameters
            .Add(p => p.StorageKey, Key).Add(p => p.Title, "T").Add(p => p.Open, true).Add(p => p.ChildContent, content));

        var focus = ctx.JSInterop.VerifyFocusAsyncInvoke();
        Assert.Equal("Blazor._internal.domWrapper.focus", focus.Identifier);
    }

    /// <summary>
    /// A focus call that the browser refuses because the body has already left the page (Expand closing the drawer
    /// and navigating as it opens its dialog) is harmless: the render completes and the drawer still works, instead
    /// of the exception terminating the circuit.
    /// </summary>
    [Fact]
    public async Task Open_FocusRefusedByTheBrowser_DoesNotFailTheRender()
    {
        await using MudBunitContext ctx = new();
        ctx.JSInterop.SetupVoid("Blazor._internal.domWrapper.focus", _ => true)
            .SetException(new Microsoft.JSInterop.JSException("Unable to focus an invalid element."));
        List<bool> raised = [];

        var cut = RenderDrawer(ctx, onOpenChanged: raised.Add);
        await cut.Find(".resizable-drawer-body").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        Assert.Equal([false], raised);
    }

    /// <summary>
    /// Opening the drawer and changing its content each scroll MudBlazor's drawer content back to the top (theming plan
    /// 7.7: New task opened at the scroll position of the Task before it); a re-render with the same content does not.
    /// </summary>
    [Fact]
    public async Task OpenAndContentKeyChange_ScrollTheDrawerToTop_SameKeyDoesNot()
    {
        await using MudBunitContext ctx = new();
        RenderFragment content = builder => builder.AddMarkupContent(0, "<span></span>");
        int ScrollCalls() => ctx.JSInterop.Invocations.Count(i => string.Equals(i.Identifier, "huddleResize.scrollDrawerToTop", StringComparison.Ordinal));
        var cut = ctx.Render<ResizableEndDrawer>(parameters => parameters
            .Add(p => p.StorageKey, Key).Add(p => p.Title, "T").Add(p => p.Open, true).Add(p => p.ContentKey, "PLAT-0002").Add(p => p.ChildContent, content));
        Assert.Equal(1, ScrollCalls());

        cut.Render(parameters => parameters.Add(p => p.ContentKey, "new"));
        Assert.Equal(2, ScrollCalls());

        cut.Render(parameters => parameters.Add(p => p.Title, "T2"));
        Assert.Equal(2, ScrollCalls());
    }

    /// <summary>A drawer that is closed does not steal focus, and it takes focus on the render where it opens.</summary>
    [Fact]
    public async Task Closed_DoesNotFocus_UntilItOpens()
    {
        await using MudBunitContext ctx = new();
        RenderFragment content = builder => builder.AddMarkupContent(0, "<span></span>");
        var cut = ctx.Render<ResizableEndDrawer>(parameters => parameters
            .Add(p => p.StorageKey, Key).Add(p => p.Title, "T").Add(p => p.Open, false).Add(p => p.ChildContent, content));
        int FocusCalls() => ctx.JSInterop.Invocations.Count(i => string.Equals(i.Identifier, "Blazor._internal.domWrapper.focus", StringComparison.Ordinal));
        Assert.Equal(0, FocusCalls());

        cut.Render(parameters => parameters.Add(p => p.Open, true));

        Assert.Equal(1, FocusCalls());
    }

    /// <summary>Changing <c>ContentKey</c> replaces the body subtree (a fresh scroll position and focus for the next Task); keeping it keeps the same subtree (theming plan 7.7).</summary>
    [Fact]
    public async Task ContentKey_Change_ReplacesTheBody_SameKeyKeepsIt()
    {
        await using MudBunitContext ctx = new();
        RenderFragment content = builder =>
        {
            builder.OpenComponent<BodyProbe>(0);
            builder.CloseComponent();
        };
        var cut = ctx.Render<ResizableEndDrawer>(parameters => parameters
            .Add(p => p.StorageKey, Key).Add(p => p.Title, "T").Add(p => p.Open, true).Add(p => p.ContentKey, "a").Add(p => p.ChildContent, content));
        BodyProbe first = cut.FindComponent<BodyProbe>().Instance;

        cut.Render(parameters => parameters.Add(p => p.ContentKey, "a"));
        Assert.Same(first, cut.FindComponent<BodyProbe>().Instance);

        cut.Render(parameters => parameters.Add(p => p.ContentKey, "b"));
        Assert.NotSame(first, cut.FindComponent<BodyProbe>().Instance);
    }

    /// <summary>A stand-in for the drawer's content whose instance identity shows whether the body was re-created.</summary>
    private sealed class BodyProbe : ComponentBase
    {
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder) =>
            builder.AddContent(0, "probe");
    }
}
