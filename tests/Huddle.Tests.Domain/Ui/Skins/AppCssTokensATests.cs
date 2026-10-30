using Agency.Huddle.Tests.Css;

namespace Agency.Huddle.Tests.Ui.Skins;

/// <summary>
/// Phase 2, package A: the first range of <c>app.css</c> (the file start up to the Message layout section) and
/// <c>TabStrip.razor.css</c> read the skin tokens (<c>--hu-*</c>) instead of repeating the literal recipe each token's
/// registered default reproduces. The default itself is pinned once, in the registry tests; these tests pin the
/// site: the rule reads the token, and the replaced recipe no longer appears in the range.
/// </summary>
public sealed class AppCssTokensATests
{
    /// <summary>The comment that opens the range owned by the next package.</summary>
    private const string RangeEnd = "/* ---- Message layout: avatar gutter";

    /// <summary>A rule reads the token for the property it used to spell as a literal recipe.</summary>
    /// <param name="selector">The rule's selector, exactly as it starts a line in <c>app.css</c>.</param>
    /// <param name="declaration">The declaration the rule's body must now contain.</param>
    [Theory]
    [InlineData("html, body", "background: var(--hu-ground-page);")]
    [InlineData("button:focus-visible", "outline: 2px solid var(--hu-focus);")]
    [InlineData(".mud-drawer.mud-drawer", "background-color: var(--hu-ground-sidebar);")]
    [InlineData(".mud-drawer.mud-drawer", "border-inline-end: 1px solid var(--hu-line-region);")]
    [InlineData(".mud-drawer.mud-drawer.right-sidebar", "background-color: var(--hu-ground-page);")]
    [InlineData(".mud-drawer.right-sidebar", "border-inline-start: 1px solid var(--hu-line-region);")]
    [InlineData(".mud-drawer .mud-navmenu .mud-nav-link.active:not(.mud-nav-link-disabled)", "color: var(--hu-current-foreground);")]
    [InlineData(".mud-drawer .mud-navmenu .mud-nav-link.active:not(.mud-nav-link-disabled)", "background-color: var(--hu-current-background);")]
    [InlineData(".mud-drawer .mud-navmenu .mud-nav-link.active:not(.mud-nav-link-disabled)", "font-weight: var(--hu-current-weight);")]
    [InlineData(".mud-drawer .mud-navmenu .mud-nav-link.active:not(.mud-nav-link-disabled)", "box-shadow: var(--hu-current-shadow);")]
    [InlineData(".mud-drawer .mud-navmenu .mud-nav-link.active:not(.mud-nav-link-disabled):active:not(.mud-nav-link-disabled)", "background-color: var(--hu-current-background-hover);")]
    [InlineData(".mud-drawer .mud-navmenu .mud-nav-link.active:not(.mud-nav-link-disabled) .mud-nav-link-icon", "color: var(--hu-current-icon);")]
    [InlineData(".mud-button-outlined.mud-button-outlined-primary", "color: var(--hu-accent-ink);")]
    [InlineData(".mud-button-root.mud-button.mud-button-outlined", "border-color: var(--hu-line-control);")]
    [InlineData(".mud-tabs:not(.mud-tabs-vertical) > .mud-tabs-tabbar", "border-bottom: 1px solid var(--hu-line-region);")]
    [InlineData(".mud-link.mud-primary-text", "color: var(--hu-accent-ink) !important;")]
    [InlineData(".mud-list-item.mud-selected-item", "background-color: var(--hu-selected-background);")]
    [InlineData(".mud-list-item.mud-selected-item", "color: var(--hu-selected-foreground) !important;")]
    [InlineData(".mud-list-item.mud-selected-item:focus-visible", "background-color: var(--hu-selected-background-hover);")]
    [InlineData(".floating-panel", "box-shadow: var(--hu-shadow-panel);")]
    [InlineData(".ref-link", "color: var(--hu-accent-ink);")]
    [InlineData(".mention", "color: var(--hu-accent-ink);")]
    [InlineData(".mention.mention--me", "background: var(--hu-tone-warning-soft);")]
    [InlineData(".selected-shape", "background-color: var(--hu-selected-background);")]
    [InlineData(".selected-shape", "color: var(--hu-selected-foreground);")]
    [InlineData(".selected-shape:focus-visible", "background-color: var(--hu-selected-background-hover);")]
    [InlineData(".tab-group + .tab-group", "border-left: 1px solid var(--hu-line-region);")]
    [InlineData(".mud-appbar.top-bar", "background-color: var(--hu-ground-page);")]
    [InlineData(".mud-appbar.top-bar", "border-bottom: 1px solid var(--hu-line-region);")]
    [InlineData(".activity-bar", "background-color: var(--hu-ground-rail);")]
    [InlineData(".activity-bar-item.activity-bar-item-selected", "color: var(--hu-current-foreground);")]
    [InlineData(".activity-bar-item.activity-bar-item-selected", "background-color: var(--hu-current-background);")]
    [InlineData(".activity-bar-item.activity-bar-item-selected", "box-shadow: var(--hu-current-shadow);")]
    [InlineData(".activity-bar-item.activity-bar-item-selected:focus-visible", "background-color: var(--hu-current-background-hover);")]
    [InlineData(".sidebar-views", "border-inline-start: 1px solid var(--hu-line-region);")]
    [InlineData(".sidebar-row .mud-link.sidebar-row-text.is-current", "color: var(--hu-accent-ink);")]
    [InlineData(".invite-panel", "background: var(--hu-ground-raised);")]
    [InlineData(".invite-agent-option:hover","background: var(--hu-hover);")]
    [InlineData(".mud-input.mud-input-outlined", "background-color: var(--hu-field-background);")]
    [InlineData(".mud-input.mud-input-outlined:focus-within .mud-shrink > .mud-input-outlined-border", "border-color: var(--hu-field-focus-line);")]
    [InlineData(".mud-input.mud-input-outlined:focus-within .mud-shrink > .mud-input-outlined-border", "box-shadow: var(--hu-field-focus-shadow);")]
    [InlineData(".mud-overlay.mud-overlay-dialog .mud-overlay-scrim.mud-overlay-dark", "background-color: var(--hu-scrim);")]
    public void AppCss_Site_ReadsTheToken(string selector, string declaration)
    {
        string body = CssText.RuleBody(AppCssRange(), selector);

        Assert.Contains(declaration, body, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
    }

    /// <summary>The recipes the tokens replace are gone from the range, except the sites the architecture leaves literal.</summary>
    [Fact]
    public void AppCss_ReplacedRecipes_AreGoneFromTheRange()
    {
        string range = AppCssRange();

        Assert.DoesNotContain("var(--mud-palette-primary) 14%, var(--mud-palette-surface))", range, StringComparison.Ordinal);
        Assert.DoesNotContain("var(--mud-palette-primary) 70%, var(--mud-palette-text-primary))", range, StringComparison.Ordinal);
        Assert.DoesNotContain("var(--mud-palette-warning) 14%, var(--mud-palette-surface))", range, StringComparison.Ordinal);
        Assert.DoesNotContain("var(--mud-elevation-4)", range, StringComparison.Ordinal);
        Assert.DoesNotContain("var(--mud-palette-action-default-hover)", range, StringComparison.Ordinal);
        Assert.DoesNotContain("outline: 2px solid var(--mud-palette-text-primary)", range, StringComparison.Ordinal);

        // The avatar monogram's 40% fill (its Light and Dark rules, each as the fill and inside its ring) is an avatar fill, not a
        // selection (architecture C3); it stays literal.
        Assert.Equal(4, CssText.CountOccurrences(range, "var(--mud-palette-primary) 40%, var(--mud-palette-surface))"));

        // The tab group's inactive marker is an inset indicator, not a region line (architecture C4); it stays literal.
        Assert.Equal(1, CssText.CountOccurrences(range, "var(--mud-palette-lines-default)"));
    }

    /// <summary>The tab strip's tints, hairlines and hovers read the tokens.</summary>
    /// <param name="selector">The rule's selector, exactly as it starts a line in <c>TabStrip.razor.css</c>.</param>
    /// <param name="declaration">The declaration the rule's body must now contain.</param>
    [Theory]
    [InlineData(".tab-strip", "background-color: var(--hu-tabs-strip-background);")]
    [InlineData(".tab-strip", "box-shadow: inset 0 -1px 0 var(--hu-line-region);")]
    [InlineData(".tab-item", "border-right: 1px solid var(--hu-line-region);")]
    [InlineData(".tab-item:not(.active):hover", "background-color: var(--hu-tabs-item-hover);")]
    [InlineData(".tab-actions", "var(--hu-tabs-item-hover) 0.75rem);")]
    [InlineData(".tab-close:hover", "background-color: var(--hu-hover);")]
    [InlineData(".tab-action:not(.tab-action-move) + .tab-action-move::before", "border-inline-start: 1px solid var(--hu-line-region);")]
    [InlineData(".tab-actions::after", "border-inline-start: 1px solid var(--hu-line-region);")]
    [InlineData(".tab-action:hover", "background-color: var(--hu-hover);")]
    public void TabStripCss_Site_ReadsTheToken(string selector, string declaration)
    {
        string body = CssText.RuleBody(TabStripCss(), selector);

        Assert.Contains(declaration, body, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
    }

    /// <summary>The tab strip stylesheet no longer repeats the tint, hairline or hover recipes.</summary>
    [Fact]
    public void TabStripCss_ReplacedRecipes_AreGone()
    {
        string css = TabStripCss();

        Assert.DoesNotContain("var(--mud-palette-lines-default)", css, StringComparison.Ordinal);
        Assert.DoesNotContain("var(--mud-palette-action-default-hover)", css, StringComparison.Ordinal);
        Assert.DoesNotContain("var(--mud-palette-text-primary) 4%", css, StringComparison.Ordinal);
        Assert.DoesNotContain("var(--mud-palette-text-primary) 7%", css, StringComparison.Ordinal);
    }

    /// <summary>The text of <c>app.css</c> from the file start to the line before the Message layout section.</summary>
    private static string AppCssRange()
    {
        string css = CssText.AppCss(CssNewlines.AsIs);
        int end = css.IndexOf(RangeEnd, StringComparison.Ordinal);
        Assert.True(end > 0, "The Message layout section header is missing.");
        return css[..end];
    }

    /// <summary>The text of <c>TabStrip.razor.css</c>.</summary>
    private static string TabStripCss() =>
        File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Layout", "TabStrip.razor.css"));

}
