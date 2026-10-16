namespace Agency.Huddle.Tests.Ui.Library;

/// <summary>
/// Source-text guards for the Library's sizes and the shared empty state (change list S7, C7, C4). A browser is not
/// rendered here, so each value is pinned in the stylesheet or markup that carries it; the running app was measured
/// separately.
/// </summary>
public sealed class LibraryReadingStyleSourceTests
{
    private const string Measure = "max-width: calc(calc(var(--mud-typography-body1-size) * 8 / 7 * 35) + 32px);";

    /// <summary>Tree rows are 30px tall, their icons 16px and the warning glyph on a folder 14px.</summary>
    [Fact]
    public void AppCss_TreeRows_AreThirtyPixelsWithSixteenPixelIcons()
    {
        string css = ReadAppCss();

        Assert.Contains("min-height: 30px;", RuleBody(css, ".library-tree .mud-treeview-item-content"), StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("height: 16px;", RuleBody(css, ".library-tree .mud-treeview-item-icon .mud-icon-root"), StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("height: 14px;", RuleBody(css, ".library-tree-orphan .mud-icon-root"), StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
    }

    /// <summary>The backlinks panel takes the reader's measure, and its label is 13px at weight 500.</summary>
    [Fact]
    public void AppCss_BacklinksPanel_MatchesTheTextColumn()
    {
        string css = ReadAppCss();
        string label = RuleBody(css, ".library-backlinks .mud-expand-panel-header .mud-expand-panel-text");

        Assert.Contains(Measure, RuleBody(css, ".library-rendered"), StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains(Measure, RuleBody(css, ".library-backlinks"), StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("font-size: var(--mud-typography-body2-size);", label, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("font-weight: 500;", label, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
    }

    /// <summary>The Read, Edit and Split labels are 13px.</summary>
    [Fact]
    public void AppCss_ModeToggle_LabelsAreThirteenPixels()
    {
        string body = RuleBody(ReadAppCss(), ".library-document-mode-toggle .mud-toggle-item,\n.library-document-mode-toggle .mud-toggle-item .mud-typography");

        Assert.Contains("font-size: var(--mud-typography-body2-size);", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
    }

    /// <summary>A full-pane empty state rests a third of the way down: two flex spacers split the free height 1 to 2.</summary>
    [Fact]
    public void AppCss_FullPaneEmptyState_SitsAThirdOfTheWayDown()
    {
        string css = ReadAppCss();

        Assert.Contains("flex: 1 1 0;", RuleBody(css, ".empty-state-centered::before"), StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("flex: 2 1 0;", RuleBody(css, ".empty-state-centered::after"), StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("height: 32px;", RuleBody(css, ".empty-state .mud-icon-root.empty-state-icon"), StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
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

    private static string ReadAppCss() => File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.css"));

    private static string RuleBody(string css, string selector)
    {
        string normalised = css.Replace("\r\n", "\n", StringComparison.Ordinal);
        string opening = "\n" + selector + " {";
        int start = normalised.LastIndexOf(opening, StringComparison.Ordinal);
        Assert.True(start >= 0, $"app.css has no rule with the selector '{selector}'.");

        int end = normalised.IndexOf('}', start);
        return normalised[(start + opening.Length)..end];
    }
}
