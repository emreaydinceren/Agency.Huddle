using Bunit;
using Agency.Huddle.App.Components.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Renders <see cref="InlineConfirm"/> on its own: the prompt, both callbacks, the busy state, the
/// focus move on first render and Escape to cancel.
/// </summary>
public sealed class InlineConfirmTests
{
    /// <summary>The Confirm button.</summary>
    private const string ConfirmButton = "button.inline-confirm-confirm";

    /// <summary>The Cancel button.</summary>
    private const string CancelButton = "button.inline-confirm-cancel";

    /// <summary>Renders the component with a prompt, a caller class and the given state and callbacks.</summary>
    private static IRenderedComponent<InlineConfirm> RenderIt(
        MudBunitContext ctx,
        bool busy = false,
        Action? onConfirm = null,
        Action? onCancel = null) =>
        ctx.Render<InlineConfirm>(parameters => parameters
            .Add(p => p.Prompt, "Delete \"Ada\"?")
            .Add(p => p.Busy, busy)
            .Add(p => p.Class, "caller-class")
            .Add(p => p.OnConfirm, EventCallback.Factory.Create(ctx, () => onConfirm?.Invoke()))
            .Add(p => p.OnCancel, EventCallback.Factory.Create(ctx, () => onCancel?.Invoke())));

    /// <summary>The prompt is shown, names the group for assistive technology, and the caller's class lands on the group.</summary>
    [Fact]
    public async Task Renders_PromptAsTextAndGroupLabel_WithCallerClass()
    {
        await using MudBunitContext ctx = new();

        var cut = RenderIt(ctx);

        var group = cut.Find("div[role=group]");
        Assert.Equal("Delete \"Ada\"?", group.GetAttribute("aria-label"));
        Assert.True(group.ClassList.Contains("caller-class"));
        Assert.Equal("Delete \"Ada\"?", cut.Find(".inline-confirm-prompt").TextContent.Trim());
    }

    /// <summary>Clicking Confirm invokes <see cref="InlineConfirm.OnConfirm"/> and not <see cref="InlineConfirm.OnCancel"/>.</summary>
    [Fact]
    public async Task ClickConfirm_InvokesOnConfirmOnly()
    {
        await using MudBunitContext ctx = new();
        int confirmed = 0;
        int cancelled = 0;
        var cut = RenderIt(ctx, onConfirm: () => confirmed++, onCancel: () => cancelled++);

        await cut.InvokeAsync(() => cut.Find(ConfirmButton).ClickAsync());

        Assert.Equal(1, confirmed);
        Assert.Equal(0, cancelled);
    }

    /// <summary>Clicking Cancel invokes <see cref="InlineConfirm.OnCancel"/> and not <see cref="InlineConfirm.OnConfirm"/>.</summary>
    [Fact]
    public async Task ClickCancel_InvokesOnCancelOnly()
    {
        await using MudBunitContext ctx = new();
        int confirmed = 0;
        int cancelled = 0;
        var cut = RenderIt(ctx, onConfirm: () => confirmed++, onCancel: () => cancelled++);

        await cut.InvokeAsync(() => cut.Find(CancelButton).ClickAsync());

        Assert.Equal(0, confirmed);
        Assert.Equal(1, cancelled);
    }

    /// <summary>While Busy both buttons are disabled and a spinner shows; when idle neither is true.</summary>
    [Fact]
    public async Task Busy_DisablesBothButtons_AndShowsSpinner()
    {
        await using MudBunitContext ctx = new();

        var idle = RenderIt(ctx);
        Assert.False(idle.Find(ConfirmButton).HasAttribute("disabled"));
        Assert.False(idle.Find(CancelButton).HasAttribute("disabled"));
        Assert.Empty(idle.FindAll(".mud-progress-circular"));

        var busy = RenderIt(ctx, busy: true);
        Assert.True(busy.Find(ConfirmButton).HasAttribute("disabled"));
        Assert.True(busy.Find(CancelButton).HasAttribute("disabled"));
        Assert.Single(busy.FindAll(".mud-progress-circular"));
    }

    /// <summary>Escape raises <see cref="InlineConfirm.OnCancel"/>; any other key does not.</summary>
    [Fact]
    public async Task EscapeKey_InvokesOnCancel_OtherKeysDoNot()
    {
        await using MudBunitContext ctx = new();
        int cancelled = 0;
        var cut = RenderIt(ctx, onCancel: () => cancelled++);

        await cut.InvokeAsync(() => cut.Find("div[role=group]").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" }));
        Assert.Equal(0, cancelled);

        await cut.InvokeAsync(() => cut.Find("div[role=group]").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" }));
        Assert.Equal(1, cancelled);
    }

    /// <summary>Escape does nothing while Busy, because Cancel is disabled then too.</summary>
    [Fact]
    public async Task EscapeKey_WhileBusy_DoesNotCancel()
    {
        await using MudBunitContext ctx = new();
        int cancelled = 0;
        var cut = RenderIt(ctx, busy: true, onCancel: () => cancelled++);

        await cut.InvokeAsync(() => cut.Find("div[role=group]").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" }));

        Assert.Equal(0, cancelled);
    }

    /// <summary>Focus is requested for the Confirm button when the row first renders.</summary>
    [Fact]
    public async Task FirstRender_RequestsFocusOnConfirm()
    {
        await using MudBunitContext ctx = new();

        _ = RenderIt(ctx);

        var focus = ctx.JSInterop.VerifyFocusAsyncInvoke();
        Assert.Equal("Blazor._internal.domWrapper.focus", focus.Identifier);
    }
}
