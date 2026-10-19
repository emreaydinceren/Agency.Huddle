using Bunit;
using Microsoft.AspNetCore.Components;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.Tests.Css;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Pins the round-3 shared components (audit A6, A7, B4k, B10, B11): the sticky drawer footer, the drawer beside the
/// Right sidebar, the compact search field, the Cancel-then-verb inline confirm and the stylesheet rules they rest on.
/// </summary>
public sealed class R3SharedComponentsTests
{
    /// <summary>The drawer's Footer shows while ShowFooter is true, under the content, and carries the caller's actions.</summary>
    [Fact]
    public async Task Drawer_Footer_ShowsWhileThereAreChanges()
    {
        await using MudBunitContext ctx = new();
        RenderFragment footer = builder => builder.AddMarkupContent(0, "<button class=\"footer-save\">Save</button>");

        IRenderedComponent<ResizableEndDrawer> cut = ctx.Render<ResizableEndDrawer>(parameters => parameters
            .Add(p => p.StorageKey, "k").Add(p => p.Title, "Task").Add(p => p.Open, true)
            .Add(p => p.Footer, footer).Add(p => p.ShowFooter, true));

        Assert.NotNull(cut.Find(".resizable-drawer-footer button.footer-save"));
    }

    /// <summary>The Footer is not drawn while ShowFooter is false, nor when none was given.</summary>
    [Fact]
    public async Task Drawer_Footer_IsHiddenWithoutChangesOrWithoutAFooter()
    {
        await using MudBunitContext ctx = new();
        RenderFragment footer = builder => builder.AddMarkupContent(0, "<button>Save</button>");

        IRenderedComponent<ResizableEndDrawer> hidden = ctx.Render<ResizableEndDrawer>(parameters => parameters
            .Add(p => p.StorageKey, "k").Add(p => p.Title, "Task").Add(p => p.Open, true)
            .Add(p => p.Footer, footer).Add(p => p.ShowFooter, false));
        IRenderedComponent<ResizableEndDrawer> none = ctx.Render<ResizableEndDrawer>(parameters => parameters
            .Add(p => p.StorageKey, "k").Add(p => p.Title, "Task").Add(p => p.Open, true));

        Assert.Empty(hidden.FindAll(".resizable-drawer-footer"));
        Assert.Empty(none.FindAll(".resizable-drawer-footer"));
    }

    /// <summary>The drawer starts under the Top bar (clip mode Always), the first half of leaving the Right sidebar's toggle uncovered.</summary>
    [Fact]
    public async Task Drawer_IsClippedUnderTheTopBar()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<ResizableEndDrawer> cut = ctx.Render<ResizableEndDrawer>(parameters => parameters
            .Add(p => p.StorageKey, "k").Add(p => p.Title, "Task").Add(p => p.Open, true));

        Assert.Contains("mud-drawer-clipped-always", cut.Find(".resizable-drawer").ClassList);
    }

    /// <summary>The drawer and its scrim keep the Right sidebar's 280px clear while the sidebar is open, and the scrim starts under the Top bar.</summary>
    [Fact]
    public void AppCss_DrawerOpensBesideTheRightSidebar()
    {
        string css = CssText.AppCss();

        const string sidebarOpen = ":root:has(.mud-drawer.right-sidebar.mud-drawer--open) ";
        Assert.Contains("right: 280px;", CssText.RuleBody(css, sidebarOpen + ".mud-drawer.resizable-drawer.mud-drawer-temporary.mud-drawer-pos-right.mud-drawer--open"), StringComparison.Ordinal); // contains-ok: stylesheet source text
        Assert.Contains("right: 280px;", CssText.RuleBody(css, sidebarOpen + ".mud-overlay.mud-overlay-drawer"), StringComparison.Ordinal); // contains-ok: stylesheet source text
        Assert.Contains("top: var(--mud-appbar-height);", CssText.RuleBody(css, ".mud-overlay.mud-overlay-drawer"), StringComparison.Ordinal); // contains-ok: stylesheet source text
    }

    /// <summary>The footer is sticky at the bottom with a Chrome line on top, right-aligned.</summary>
    [Fact]
    public void AppCss_DrawerFooterIsStickyWithAChromeLine()
    {
        string footer = CssText.RuleBody(CssText.AppCss(), ".resizable-drawer-footer");

        Assert.Contains("position: sticky;", footer, StringComparison.Ordinal); // contains-ok: stylesheet source text
        Assert.Contains("bottom: 0;", footer, StringComparison.Ordinal); // contains-ok: stylesheet source text
        Assert.Contains("justify-content: flex-end;", footer, StringComparison.Ordinal); // contains-ok: stylesheet source text
        Assert.Contains("border-top: 1px solid var(--hu-line-region);", footer, StringComparison.Ordinal); // contains-ok: stylesheet source text
    }

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

    /// <summary>A chip of the cloud is the Filter chip: 28px, a dashed Field line and no fill unset, and the Accent border and tint set.</summary>
    [Fact]
    public void AppCss_ChipCloudChipIsTheFilterChip()
    {
        string css = CssText.AppCss();

        string chip = CssText.RuleBody(css, ".chip-cloud-chip,\n.chip-cloud-all");
        Assert.Contains("height: 28px;", chip, StringComparison.Ordinal); // contains-ok: stylesheet source text
        Assert.Contains("border: 1px dashed var(--ink-field);", chip, StringComparison.Ordinal); // contains-ok: stylesheet source text
        Assert.Contains("background-color: transparent;", chip, StringComparison.Ordinal); // contains-ok: stylesheet source text
        string set = CssText.RuleBody(css, ".chip-cloud-chip.is-set");
        Assert.Contains("border: 1px solid var(--mud-palette-primary);", set, StringComparison.Ordinal); // contains-ok: stylesheet source text
        Assert.Contains("background-color: var(--hu-selected-background);", set, StringComparison.Ordinal); // contains-ok: stylesheet source text
        Assert.DoesNotContain("is-quiet", css, StringComparison.Ordinal); // contains-ok: stylesheet source text
    }

    /// <summary>A set chip of the cloud carries exactly the classes the stylesheet's Filter-chip set state keys on; an unset chip carries only the base class.</summary>
    [Fact]
    public async Task ChipCloud_SetChip_WearsTheSetClasses()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<ChipCloud> cut = ctx.Render<ChipCloud>(parameters => parameters
            .Add(p => p.Title, "Tags")
            .Add(p => p.Pool, ["a", "b"])
            .Add(p => p.Selected, ["a"]));

        Assert.Equal(["chip-cloud-chip chip-cloud-chip-selected is-set", "chip-cloud-chip"], cut.FindAll("button.chip-cloud-chip").Select(static b => b.ClassName));
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
