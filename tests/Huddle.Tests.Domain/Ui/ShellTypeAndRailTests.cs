using Agency.Huddle.Tests.Css;
namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Pins the tab strip (C5), the tab rail (C6), the breadcrumb separator (S1) and the sidebar row and header
/// metrics (S2) of the UI change list. A browser is not rendered here, so the stylesheets are pinned as source
/// text; the same values were measured in the running app.
/// </summary>
public sealed class ShellTypeAndRailTests
{
    /// <summary>A Tab title is 13px at weight 400, and the active Tab is weight 500.</summary>
    [Fact]
    public void TabStrip_TitleIs13pxWeight400_ActiveIs500()
    {
        string css = ShellTypeAndRailTests.TabStripCss();

        Assert.Matches(@"\.tab-item\s*\{[^}]*font-size:\s*var\(--mud-typography-body2-size\);[^}]*font-weight:\s*400;[^}]*color:\s*var\(--mud-palette-text-primary\)", css);
        Assert.Matches(@"\.tab-item\.active\s*\{[^}]*font-weight:\s*500;", css);
    }

    /// <summary>A preview Tab takes the secondary text colour and an italic title, never a lighter weight; a kept Tab keeps the upright body style.</summary>
    [Fact]
    public void TabStrip_PreviewIsSecondaryColourAndItalic_NotWeight()
    {
        string css = ShellTypeAndRailTests.TabStripCss();

        Assert.Matches(@"\.tab-item\.preview\s*\{[^}]*color:\s*var\(--mud-palette-text-secondary\);", css);
        Assert.Matches(@"\.tab-item\.preview\s+\.tab-title\s*\{[^}]*font-style:\s*italic;", css);
        Assert.DoesNotMatch(@"\.tab-item\.preview[^{]*\{[^}]*font-weight:", css);
    }

    /// <summary>A long Tab title truncates with an ellipsis, and the whole Tab carries the full title as its tooltip.</summary>
    [Fact]
    public void TabStrip_LongTitleTruncatesWithTheFullTitleInATooltip()
    {
        string css = ShellTypeAndRailTests.TabStripCss();
        string markup = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Layout", "TabStrip.razor"));

        Assert.Matches(@"\.tab-title\s*\{[^}]*overflow:\s*hidden;[^}]*text-overflow:\s*ellipsis;", css);
        Assert.Contains("title=\"@tab.Title\"", markup, StringComparison.Ordinal);
    }

    /// <summary>The tab rail label is 13px, weight 500, secondary text; its icon is 18px and follows the label colour.</summary>
    [Fact]
    public void TabRail_LabelAndIconMetrics()
    {
        string css = CssText.AppCss(CssNewlines.AsIs);

        Assert.Matches(@"(?m)^\.mud-tab\s*\{[^}]*font-size:\s*var\(--mud-typography-body2-size\);[^}]*font-weight:\s*500;[^}]*color:\s*var\(--mud-palette-text-secondary\);", css);
        Assert.Matches(@"(?m)^\.mud-tab \.mud-icon-root\s*\{[^}]*font-size:\s*var\(--icon-size-row\);[^}]*color:\s*inherit;", css);
    }

    /// <summary>The active rail label is the accent text and its indicator is a 2px bar.</summary>
    [Fact]
    public void TabRail_ActiveLabelIsAccentText_IndicatorIs2px()
    {
        string css = CssText.AppCss(CssNewlines.AsIs);

        Assert.Matches(@"\.mud-tab\.mud-tab-active,[^{]*\{[^}]*color: var\(--hu-accent-ink\)", css);
        Assert.Matches(@"(?m)^\.mud-tab-slider\s*\{[^}]*height:\s*2px;", css);
    }

    /// <summary>The breadcrumb separator has 8px either side.</summary>
    [Fact]
    public void Breadcrumb_SeparatorHas8pxEitherSide()
    {
        Assert.Matches(@"\.page-breadcrumbs \.mud-breadcrumb-separator\s*\{[^}]*padding-inline:\s*8px;", CssText.AppCss(CssNewlines.AsIs));
    }

    /// <summary>A sidebar row is 32px tall with an 18px icon and an 8px gap; a section header is 12px, weight 600, 16px above and 4px below.</summary>
    [Fact]
    public void Sidebar_RowAndSectionHeaderMetrics()
    {
        string css = CssText.AppCss(CssNewlines.AsIs);

        Assert.Matches(@"--nav-row-height:\s*32px;[^}]*--icon-size-row:\s*18px;", css);
        Assert.Matches(@"\.mud-nav-group \.mud-collapse-container \.mud-nav-link \.mud-nav-link-icon\s*\{[^}]*font-size:\s*var\(--icon-size-row\);", css);
        Assert.Matches(@"--nav-gap:\s*8px;", css);
        Assert.Matches(@"\.mud-nav-group > \.mud-nav-link \.mud-nav-link-text\s*\{[^}]*font-size:\s*var\(--mud-typography-caption-size\);[^}]*font-weight:\s*600;[^}]*text-secondary", css);
        Assert.Matches(@"\.mud-nav-group > \.mud-nav-link\s*\{[^}]*margin-top:\s*16px;[^}]*margin-bottom:\s*4px;", css);
        Assert.DoesNotContain("nav-section-link", css, StringComparison.Ordinal);
    }

    private static string TabStripCss() =>
        File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Layout", "TabStrip.razor.css"));
}
