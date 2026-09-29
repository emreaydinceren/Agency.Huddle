using Bunit;
using Agency.Huddle.App.Components.Settings;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Renders <see cref="ResetAllControl"/> on its own from its parameters, using
/// <see cref="MudBunitContext"/> rather than the <c>HtmlRenderer</c> this suite used before the
/// MudBlazor migration: <c>HtmlRenderer</c> could not dispatch a click, so the plain-button and
/// confirm-step renderings were exercised only through the <see cref="ResetAllControl.Confirming"/>
/// and <see cref="ResetAllControl.AnyModified"/> parameters. bUnit can additionally click, so this
/// suite now exercises the actual inline-confirm flow one click at a time rather than only its two
/// snapshot states. Every test disposes the context with <c>await using</c> rather than <c>using</c> -
/// see the note on <see cref="PromptsPanelTests"/> for why a synchronous <c>Dispose</c> is unsafe once a
/// MudBlazor component has rendered.
/// </summary>
public sealed class ResetAllControlTests
{
    /// <summary>With nothing modified and not confirming, the plain button renders disabled.</summary>
    [Fact]
    public async Task NothingModified_NotConfirming_ButtonIsDisabled()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<ResetAllControl>(parameters => parameters
            .Add(p => p.AnyModified, false)
            .Add(p => p.Confirming, false));

        Assert.Contains("Reset all to defaults", cut.Markup, StringComparison.Ordinal);
        Assert.True(cut.Find("button").HasAttribute("disabled"));
        Assert.DoesNotContain("Yes, reset everything", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>With something modified and not confirming, the plain button renders enabled.</summary>
    [Fact]
    public async Task SomethingModified_NotConfirming_ButtonIsEnabled()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<ResetAllControl>(parameters => parameters
            .Add(p => p.AnyModified, true)
            .Add(p => p.Confirming, false));

        Assert.Contains("Reset all to defaults", cut.Markup, StringComparison.Ordinal);
        Assert.False(cut.Find("button").HasAttribute("disabled"));
    }

    /// <summary>
    /// Clicking the enabled plain button raises <see cref="ResetAllControl.OnBeginReset"/> - the
    /// step that used to be untestable under <c>HtmlRenderer</c>, which could only render the
    /// <see cref="ResetAllControl.Confirming"/> state directly rather than reach it by clicking.
    /// </summary>
    [Fact]
    public async Task SomethingModified_ClickingThePlainButton_RaisesOnBeginReset()
    {
        await using MudBunitContext ctx = new();
        var beginResetRaised = false;

        var cut = ctx.Render<ResetAllControl>(parameters => parameters
            .Add(p => p.AnyModified, true)
            .Add(p => p.Confirming, false)
            .Add(p => p.OnBeginReset, () => beginResetRaised = true));

        await cut.InvokeAsync(() => cut.Find("button").ClickAsync());

        Assert.True(beginResetRaised);
    }

    /// <summary>While confirming, the plain button is replaced by Confirm/Cancel, never shown alongside it.</summary>
    [Fact]
    public async Task Confirming_ShowsConfirmAndCancelInsteadOfThePlainButton()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<ResetAllControl>(parameters => parameters
            .Add(p => p.AnyModified, true)
            .Add(p => p.Confirming, true));

        Assert.Contains("Yes, reset everything", cut.Markup, StringComparison.Ordinal);
        Assert.Contains(">Cancel<", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Reset all to defaults", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>Clicking "Yes, reset everything" while confirming raises <see cref="ResetAllControl.OnConfirm"/>, and clicking Cancel raises <see cref="ResetAllControl.OnCancel"/> instead.</summary>
    [Fact]
    public async Task Confirming_ClickingYesRaisesOnConfirm_AndClickingCancelRaisesOnCancel()
    {
        await using MudBunitContext ctx = new();
        var confirmRaised = false;
        var cancelRaised = false;

        var cut = ctx.Render<ResetAllControl>(parameters => parameters
            .Add(p => p.AnyModified, true)
            .Add(p => p.Confirming, true)
            .Add(p => p.OnConfirm, () => confirmRaised = true)
            .Add(p => p.OnCancel, () => cancelRaised = true));

        var buttons = cut.FindAll("button");
        Assert.Equal(2, buttons.Count);

        await cut.InvokeAsync(() => buttons[0].ClickAsync());
        Assert.True(confirmRaised);
        Assert.False(cancelRaised);

        await cut.InvokeAsync(() => buttons[1].ClickAsync());
        Assert.True(cancelRaised);
    }
}
