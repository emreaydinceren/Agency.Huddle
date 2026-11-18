using Bunit;
using Agency.Huddle.App.Components.Shared;
using MudBlazor;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// The one switch (ComponentCatalog, SettingSwitch): always Small and Primary (Q-A7-1), one caption line that is the disabled reason
/// while disabled and the helper otherwise, and a flip raised to the caller at once.
/// </summary>
public sealed class SettingSwitchTests
{
    /// <summary>Every switch is Small and Primary with the caller's label and hook class, whatever the caller passes.</summary>
    [Fact]
    public async Task Render_IsSmallAndPrimary_WithLabelAndHookClass()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<SettingSwitch> cut = ctx.Render<SettingSwitch>(p => p
            .Add(s => s.Label, "Show hidden")
            .Add(s => s.Class, "pack-bar-show-hidden"));

        MudSwitch<bool> inner = cut.FindComponent<MudSwitch<bool>>().Instance;
        Assert.Equal(Size.Small, inner.Size);
        Assert.Equal(Color.Primary, inner.Color);
        Assert.Equal("Show hidden", inner.Label);
        Assert.Equal("pack-bar-show-hidden", inner.Class);
        Assert.Empty(cut.FindAll(".setting-switch-helper, .setting-switch-reason"));
    }

    /// <summary>An enabled switch shows its helper as a plain caption; a disabled one shows its reason instead, as a status line.</summary>
    /// <param name="disabled">Whether the switch is disabled.</param>
    /// <param name="text">The one line expected under it.</param>
    /// <param name="role">The line's role, or null for none.</param>
    [Theory]
    [InlineData(false, "What it does.", null)]
    [InlineData(true, "Why it is off.", "status")]
    public async Task Line_IsTheReasonWhileDisabled_OtherwiseTheHelper(bool disabled, string text, string? role)
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<SettingSwitch> cut = ctx.Render<SettingSwitch>(p => p
            .Add(s => s.Label, "Browser")
            .Add(s => s.Helper, "What it does.")
            .Add(s => s.Disabled, disabled)
            .Add(s => s.DisabledReason, "Why it is off.")
            .Add(s => s.LineHookClass, "agent-browser-mode-note"));

        AngleSharp.Dom.IElement line = Assert.Single(cut.FindAll(".agent-browser-mode-note"));
        Assert.Equal(text, line.TextContent.Trim());
        Assert.Equal(role, line.GetAttribute("role"));
        string kind = disabled ? "setting-switch-reason" : "setting-switch-helper";
        Assert.Equal(
            ["mud-typography", "mud-typography-caption", "d-block", "mud-text-secondary", kind, "agent-browser-mode-note"],
            line.ClassList);
    }

    /// <summary>Flipping the switch raises the new value to the caller at once.</summary>
    [Fact]
    public async Task Flip_RaisesValueChanged()
    {
        await using MudBunitContext ctx = new();
        bool? raised = null;

        IRenderedComponent<SettingSwitch> cut = ctx.Render<SettingSwitch>(p => p
            .Add(s => s.Label, "Hide")
            .Add(s => s.ValueChanged, (bool value) => raised = value));
        await cut.InvokeAsync(() => cut.Find("input").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = true }));

        Assert.True(raised);
    }
}
