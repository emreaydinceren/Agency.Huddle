namespace Agency.Huddle.Tests.Ui.Library;

/// <summary>
/// Round 3 Library guards (audit B2, A5, B10): the shared header band, the tree's selected shape, and the tree
/// menu's idiom. A browser is not rendered here, so each value is pinned in the stylesheet or markup that carries
/// it; the running app is measured separately.
/// </summary>
public sealed class R3LibraryTests
{
    /// <summary>The tree header and the document toolbar are one band: both take the same height, and each ends in a Chrome line.</summary>
    [Fact]
    public void AppCss_TreeHeaderAndToolbar_ShareOneBandHeightAndChromeLine()
    {
        string css = ReadAppCss();
        string header = RuleBody(css, ".library-explorer-tree-header");
        string toolbar = RuleBody(css, ".library-document-toolbar.mud-toolbar");

        Assert.Contains("min-height: 61px;", header, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("min-height: 61px;", toolbar, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("box-sizing: border-box;", header, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("box-sizing: border-box;", toolbar, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("border-bottom: 1px solid var(--mud-palette-lines-default);", header, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("border-bottom: 1px solid var(--mud-palette-lines-default);", toolbar, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
    }

    /// <summary>New note sits inside the band, in the same row as the search field, which is the compact 28px field.</summary>
    [Fact]
    public void Explorer_NewNote_IsInTheSameRowAsTheCompactSearch()
    {
        string markup = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Library", "LibraryExplorer.razor"));
        string header = RuleBody(ReadAppCss(), ".library-explorer-tree-header");

        Assert.Contains("Compact=\"true\"", markup, StringComparison.Ordinal); // contains-ok: Razor source text, not rendered markup
        Assert.Contains("display: flex;", header, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.DoesNotContain("flex-direction: column;", header, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
    }

    /// <summary>A tree row takes the shared selected shape: inset 4px, rounded at 0.75x the base radius, with the 2px bar kept inside.</summary>
    [Fact]
    public void AppCss_TreeRow_TakesTheSelectedShapeAndKeepsItsBar()
    {
        string css = ReadAppCss();
        string row = RuleBody(css, ".library-tree .mud-treeview-item-content");
        string selected = RuleBody(css, ".library-tree.mud-treeview .mud-treeview-item-content.mud-treeview-item-selected");

        Assert.Contains("margin-inline: 4px;", row, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("border-radius: calc(var(--mud-default-borderradius) * 0.75);", row, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("14%", selected, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("font-weight: 500;", selected, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("box-shadow: inset 2px 0 0 var(--mud-palette-primary);", selected, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
    }

    /// <summary>A menu item that opens a dialog closes its menu first, through the shared idiom, and the menu note is Quiet ink.</summary>
    [Fact]
    public void Tree_MenuItems_CloseTheMenuBeforeADialogOpens()
    {
        string markup = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Library", "LibraryTree.razor"));
        string css = ReadAppCss();

        Assert.Contains("MenuDialogs.AfterClosingAsync(menu,", markup, StringComparison.Ordinal); // contains-ok: Razor source text, not rendered markup
        Assert.Contains("this.RaiseFromMenuAsync(action.Kind, value)", markup, StringComparison.Ordinal); // contains-ok: Razor source text, not rendered markup
        Assert.Contains("color: var(--mud-palette-text-secondary);", RuleBody(css, ".library-tree-menu-note"), StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
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
