using Bunit;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.Tests.Css;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Pins the round-3 shared components (audit A6, A7, B10): the compact search field, the Cancel-then-verb inline
/// confirm and the stylesheet rules they rest on.
/// </summary>
public sealed class R3SharedComponentsTests
{
    /// <summary>The header row is 32px high, and the intro is body1 in Quiet ink.</summary>
    [Fact]
    public void AppCss_PageHeaderRowIs32px_AndTheIntroIsBody1()
    {
        string css = CssText.AppCss();

        Assert.Contains("min-height: 32px;", CssText.RuleBody(css, ".page-header-row"), StringComparison.Ordinal); // contains-ok: stylesheet source text
        string intro = CssText.RuleBody(css, ".page-lead");
        Assert.Contains("var(--mud-typography-body1-size)", intro, StringComparison.Ordinal); // contains-ok: stylesheet source text
        Assert.Contains("var(--mud-palette-text-secondary)", intro, StringComparison.Ordinal); // contains-ok: stylesheet source text
    }

    /// <summary>A picker row is 32px at 13px with an 18px icon in a 24px slot.</summary>
    [Fact]
    public void AppCss_PickerRowIs32px_At13px_WithAnIconSlot()
    {
        string css = CssText.AppCss();

        string row = CssText.RuleBody(css, ".mud-list-item.picker-row");
        Assert.Contains("min-height: 32px;", row, StringComparison.Ordinal); // contains-ok: stylesheet source text
        Assert.Contains("var(--mud-typography-body2-size)", row, StringComparison.Ordinal); // contains-ok: stylesheet source text
        Assert.Contains("width: 24px;", CssText.RuleBody(css, ".picker-row .mud-list-item-icon"), StringComparison.Ordinal); // contains-ok: stylesheet source text
        Assert.Contains("font-size: var(--icon-size-row);", CssText.RuleBody(css, ".picker-row .mud-list-item-icon .mud-icon-root"), StringComparison.Ordinal); // contains-ok: stylesheet source text
    }

    /// <summary>A chip of the cloud is the Filter chip and has no rule of its own: the cloud's selector list and its set rule are gone, so the one Filter block reaches it.</summary>
    [Fact]
    public void AppCss_ChipCloudChipHasNoRuleOfItsOwn()
    {
        string css = CssText.AppCss();

        Assert.DoesNotContain(".chip-cloud-chip,", css, StringComparison.Ordinal); // contains-ok: stylesheet source text
        Assert.DoesNotContain(".chip-cloud-chip.is-set", css, StringComparison.Ordinal); // contains-ok: stylesheet source text
        Assert.DoesNotContain(".chip-cloud-all", css, StringComparison.Ordinal); // contains-ok: stylesheet source text
        string filter = CssText.RuleBody(css, ".mud-chip.filter-chip");
        Assert.Contains("height: 28px;", filter, StringComparison.Ordinal); // contains-ok: stylesheet source text
        Assert.Contains("border: 1px dashed var(--ink-field);", filter, StringComparison.Ordinal); // contains-ok: stylesheet source text
        Assert.Contains("background-color: transparent;", filter, StringComparison.Ordinal); // contains-ok: stylesheet source text
        Assert.DoesNotContain("is-quiet", css, StringComparison.Ordinal); // contains-ok: stylesheet source text
    }

    /// <summary>A set chip of the cloud is the Filter chip in its set state (<c>is-set</c>); an unset chip is the same chip without it.</summary>
    [Fact]
    public async Task ChipCloud_SetChip_WearsTheSetClasses()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<ChipCloud> cut = ctx.Render<ChipCloud>(parameters => parameters
            .Add(p => p.Title, "Tags")
            .Add(p => p.Pool, ["a", "b"])
            .Add(p => p.Selected, ["a"]));

        Assert.Equal([true, false], cut.FindAll(".mud-chip.filter-chip.chip-cloud-chip").Select(static chip => chip.ClassList.Contains("is-set")));
    }

    /// <summary>A Compact SearchField wears the global <c>toolbar-compact</c> class, with the caller's own class beside it; otherwise it does not.</summary>
    [Fact]
    public async Task SearchField_Compact_WearsToolbarCompact()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<SearchField> compact = ctx.Render<SearchField>(parameters => parameters
            .Add(p => p.Compact, true).Add(p => p.Class, "mine"));
        IRenderedComponent<SearchField> regular = ctx.Render<SearchField>(parameters => parameters.Add(p => p.Class, "mine"));

        Assert.Contains("toolbar-compact", compact.Find(".mud-input-control").ClassList);
        Assert.Contains("mine", compact.Find(".mud-input-control").ClassList);
        Assert.DoesNotContain("toolbar-compact", regular.Find(".mud-input-control").ClassList);
    }

    /// <summary>The inline confirm puts Cancel before the verb, and the verb button repeats the action ("Delete" by default), never "Confirm".</summary>
    [Fact]
    public async Task InlineConfirm_IsCancelThenTheVerb()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<InlineConfirm> cut = ctx.Render<InlineConfirm>(parameters => parameters.Add(p => p.Prompt, "Delete \"Ada\"?"));

        var buttons = cut.FindAll("button");
        Assert.Equal(["Cancel", "Delete"], buttons.Select(static b => b.TextContent.Trim()));
        Assert.Contains("inline-confirm-cancel", buttons[0].ClassList);
        Assert.Contains("inline-confirm-confirm", buttons[1].ClassList);
        Assert.Contains("mud-button-filled-error", buttons[1].ClassList);
        Assert.DoesNotContain("Confirm", cut.Markup.Replace("inline-confirm-confirm", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
    }

    /// <summary>The narrow form, for the sidebar, adds <c>inline-confirm-narrow</c> so the prompt and buttons stay on one line.</summary>
    [Fact]
    public async Task InlineConfirm_Narrow_AddsTheNarrowClass()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<InlineConfirm> narrow = ctx.Render<InlineConfirm>(parameters => parameters
            .Add(p => p.Prompt, "Delete?").Add(p => p.Narrow, true));
        IRenderedComponent<InlineConfirm> regular = ctx.Render<InlineConfirm>(parameters => parameters.Add(p => p.Prompt, "Delete?"));

        Assert.Contains("inline-confirm-narrow", narrow.Find("div[role=group]").ClassList);
        Assert.DoesNotContain("inline-confirm-narrow", regular.Find("div[role=group]").ClassList);
    }
}
