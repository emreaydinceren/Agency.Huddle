using Agency.Huddle.Tests.Css;

namespace Agency.Huddle.Tests.Ui.Library;

/// <summary>
/// Source-text guards for the Library's sizes and the shared empty state (change list S7, C7, C4). A browser is not
/// rendered here, so each value is pinned in the stylesheet or markup that carries it; the running app was measured
/// separately.
/// </summary>
public sealed class LibraryReadingStyleSourceTests
{
    private const string Measure = "max-width: calc(calc(var(--mud-typography-body1-size) * 8 / 7 * 46.29) + 32px);";

    /// <summary>Tree rows are 32px tall (the row height of the sidebar and the menus), their icons 16px and the warning glyph on a folder 14px.</summary>
    [Fact]
    public void AppCss_TreeRows_AreThirtyTwoPixelsWithSixteenPixelIcons()
    {
        string css = CssText.AppCss();

        Assert.Contains("min-height: 32px;", CssText.RuleBody(css, ".library-tree .mud-treeview-item-content", RuleMatch.Last), StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("height: 16px;", CssText.RuleBody(css, ".library-tree .mud-treeview-item-icon .mud-icon-root", RuleMatch.Last), StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("height: 14px;", CssText.RuleBody(css, ".library-tree-orphan .mud-icon-root", RuleMatch.Last), StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
    }

    /// <summary>The rendered note takes the reader's measure and is centred in the pane.</summary>
    [Fact]
    public void AppCss_RenderedNote_TakesTheReaderMeasure()
    {
        string css = CssText.AppCss();
        string body = CssText.RuleBody(css, ".library-rendered", RuleMatch.Last);

        Assert.Contains(Measure, body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("margin-inline: auto;", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
    }

    /// <summary>The Read, Edit and Split labels are 13px.</summary>
    [Fact]
    public void AppCss_ModeToggle_LabelsAreThirteenPixels()
    {
        string body = CssText.RuleBody(CssText.AppCss(), ".library-document-mode-toggle .mud-toggle-item,\n.library-document-mode-toggle .mud-toggle-item .mud-typography", RuleMatch.Last);

        Assert.Contains("font-size: var(--mud-typography-body2-size);", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
    }

    /// <summary>A full-pane empty state rests a third of the way down: two flex spacers split the free height 1 to 2.</summary>
    [Fact]
    public void AppCss_FullPaneEmptyState_SitsAThirdOfTheWayDown()
    {
        string css = CssText.AppCss();

        Assert.Contains("flex: 1 1 0;", CssText.RuleBody(css, ".empty-state-centered::before", RuleMatch.Last), StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("flex: 2 1 0;", CssText.RuleBody(css, ".empty-state-centered::after", RuleMatch.Last), StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("height: 32px;", CssText.RuleBody(css, ".empty-state .mud-icon-root.empty-state-icon", RuleMatch.Last), StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
    }

    /// <summary>The Library's toolbar and its empty state offer "New note" in the same style: neither is outlined.</summary>
    [Fact]
    public void Explorer_NewNoteButtons_ShareOneStyle()
    {
        string markup = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Library", "LibraryExplorer.razor"));
        string[] lines = markup.Split('\n').Where(l => l.Contains("New note</MudButton>", StringComparison.Ordinal)).ToArray(); // contains-ok: Razor source text, not rendered markup

        Assert.Equal(2, lines.Length);
        Assert.All(lines, l => Assert.DoesNotContain("Variant.Outlined", l, StringComparison.Ordinal)); // contains-ok: Razor source text, not rendered markup
    }

}
