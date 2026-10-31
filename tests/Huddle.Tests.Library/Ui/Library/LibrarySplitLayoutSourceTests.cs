using Agency.Huddle.Tests.Css;

namespace Agency.Huddle.Tests.Ui.Library;

/// <summary>
/// Source-text guards for the Library page's split filling its pane (UX-006, plan U18 T18.2). A browser is not
/// rendered here, so each property is pinned in the stylesheet or the markup that carries it, as
/// <c>RoomLayoutSourceTests</c> does. Percent heights are used on purpose: the page fills the pane it sits in,
/// not the window (plan section 3.5).
/// </summary>
public sealed class LibrarySplitLayoutSourceTests
{
    /// <summary>The Library page sets the document title "Library" without a <c>PageHeader</c> (the shell breadcrumb names it) and its root wears <c>library-page</c> so the split can take the pane's height (UX-001, UX-006).</summary>
    [Fact]
    public void LibraryPage_HasADocumentTitleAndAFillingRoot()
    {
        string markup = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Pages", "Library.razor"));

        Assert.Contains("<FocusedPageTitle Title=\"Library\" />", markup, StringComparison.Ordinal); // contains-ok: Razor source text, not rendered markup
        Assert.DoesNotContain("<PageHeader", markup, StringComparison.Ordinal); // contains-ok: Razor source text, not rendered markup
        Assert.Contains("library-page", markup, StringComparison.Ordinal); // contains-ok: Razor source text, not rendered markup
    }

    /// <summary>The page root is a full-height flex column that may shrink, so the explorer under the header gets the rest of the pane.</summary>
    [Fact]
    public void AppCss_LibraryPage_IsAFullHeightShrinkableColumn()
    {
        string body = CssText.RuleBody(CssText.AppCss(CssNewlines.AsIs), ".library-page");

        Assert.Contains("height: 100%;", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("display: flex;", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("flex-direction: column;", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("min-height: 0;", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
    }

    /// <summary>The explorer fills what the page leaves and may shrink below its content, on every page that hosts it, not only the Team page.</summary>
    [Fact]
    public void AppCss_LibraryExplorer_FillsAndMayShrink()
    {
        string body = CssText.RuleBody(CssText.AppCss(CssNewlines.AsIs), ".library-page .library-explorer");

        Assert.Contains("flex: 1 1 auto;", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("min-height: 0;", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("flex-direction: column;", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
    }

    /// <summary>The split is as tall as the explorer (<c>h-100</c> in percent) and may shrink, so the tree and the document scroll inside it instead of ending at 360px.</summary>
    [Fact]
    public void AppCss_LibrarySplit_IsFullHeightAndMayShrink()
    {
        string body = CssText.RuleBody(CssText.AppCss(CssNewlines.AsIs), ".library-explorer-split");

        Assert.Contains("height: 100%;", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("min-height: 0;", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
    }

    /// <summary>Both side-by-side panels are full height and scroll on their own, so the tree and the document are never two boxes of different heights.</summary>
    [Fact]
    public void AppCss_LibrarySplitPanels_AreFullHeightAndScroll()
    {
        string body = CssText.RuleBody(CssText.AppCss(CssNewlines.AsIs), ".library-explorer-split-side-by-side > .child-panel");

        Assert.Contains("height: 100%;", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("overflow: auto;", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
    }

    /// <summary>The divider is a visible hairline in the lines-default colour, not transparent.</summary>
    [Fact]
    public void AppCss_LibrarySplitDivider_IsAVisibleHairline()
    {
        string body = CssText.RuleBody(CssText.AppCss(CssNewlines.AsIs), ".library-explorer-split > .divider");

        Assert.Contains("background-color: var(--hu-line-region);", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
    }

}
