using Agency.Huddle.Tests.Css;

namespace Agency.Huddle.Tests.Ui.Skins;

/// <summary>
/// Phase 2, package C: the Tasks, Library and Task-drawer sections of <c>app.css</c> (from the "Tasks" header to the
/// "Task List at narrower widths" header) read the skin tokens instead of the literal recipes the tokens default to.
/// The values are pinned once, in the registry's default test; here only the wiring is pinned.
/// </summary>
public sealed class AppCssTokensCTests
{
    private const string RangeStart = "Tasks ---";
    private const string RangeEnd = "The Task List at narrower widths";

    /// <summary>Reads the stylesheet with LF endings, restricted to this package's range.</summary>
    /// <returns>The text between the two section headers.</returns>
    private static string Range() =>
        CssText.Section(CssText.AppCss(), RangeStart, RangeEnd);

    /// <summary>Each converted site now declares the token reference, not the recipe.</summary>
    /// <param name="selector">The rule's selector.</param>
    /// <param name="declaration">The declaration it must contain.</param>
    [Theory]
    [InlineData(".task-board-columns", "background: var(--hu-ground-page);")]
    [InlineData(".task-board-lane-heading.group-heading", "border-bottom: 1px solid var(--hu-line-region);")]
    [InlineData(".task-board-column", "background-color: var(--hu-ground-sunk);")]
    [InlineData(".task-card", "background-color: var(--hu-ground-raised);")]
    [InlineData(".task-card", "box-shadow: var(--hu-shadow-card);")]
    [InlineData(".task-card:hover", "box-shadow: var(--hu-board-card-hover-shadow);")]
    [InlineData(".task-card-open:focus-visible", "outline: 2px solid var(--hu-focus);")]
    [InlineData(".mud-table .mud-table-body tr.task-row-selected > td", "background-color: var(--hu-selected-task-background);")]
    [InlineData(".mud-table.mud-table-hover .mud-table-body tr.task-row-selected:hover > td", "background-color: var(--hu-selected-background-hover);")]
    [InlineData(".task-card.task-card-selected", "background-color: var(--hu-selected-task-background);")]
    [InlineData(".task-card.task-card-selected", "box-shadow: inset 3px 0 0 var(--hu-selected-task-bar);")]
    [InlineData(".library-editor .cm-editor", "background-color: var(--hu-ground-page);")]
    [InlineData(".mud-toolbar.toolbar-hairline", "border-bottom: 1px solid var(--hu-line-region);")]
    [InlineData(".library-document-toolbar.mud-toolbar", "border-bottom: 1px solid var(--hu-line-region);")]
    [InlineData(".library-document-split > .library-editor", "border-inline-end: 1px solid var(--hu-line-region);")]
    [InlineData(".library-explorer-split > .divider", "background-color: var(--hu-line-region);")]
    [InlineData(".library-explorer-tree-header", "border-bottom: 1px solid var(--hu-line-region);")]
    [InlineData(".library-explorer-tree-header", "background: var(--hu-ground-sidebar);")]
    [InlineData(".library-tree.mud-treeview .mud-treeview-item-content.mud-treeview-item-selected", "color: var(--hu-selected-foreground);")]
    [InlineData(".library-tree.mud-treeview .mud-treeview-item-content.mud-treeview-item-selected", "background-color: var(--hu-selected-background);")]
    [InlineData(".library-tree.mud-treeview .mud-treeview-item-content.mud-treeview-item-selected", "box-shadow: inset 2px 0 0 var(--hu-tree-current-bar);")]
    [InlineData(".library-tree.mud-treeview .mud-treeview-item-content.mud-treeview-item-selected:hover", "background-color: var(--hu-selected-background-hover);")]
    [InlineData(".tasks-count-need-you:not(.is-zero)", "color: color-mix(in srgb, var(--hu-needs) 70%, var(--mud-palette-text-primary));")]
    [InlineData(".task-filter-chip-set .task-filter-chip-button", "color: var(--hu-selected-foreground);")]
    [InlineData(".library-rendered .library-ref", "color: var(--hu-accent-ink);")]
    public void Rule_ReadsTheToken(string selector, string declaration)
    {
        Assert.Contains(declaration, CssText.RuleBody(Range(), selector), StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
    }

    /// <summary>The recipes the tokens default to no longer appear anywhere in the range.</summary>
    /// <param name="recipe">A literal recipe that a token now owns.</param>
    [Theory]
    [InlineData("color-mix(in srgb, var(--mud-palette-primary) 14%, var(--mud-palette-surface))")]
    [InlineData("color-mix(in srgb, var(--mud-palette-primary) 20%, var(--mud-palette-surface))")]
    [InlineData("color-mix(in srgb, var(--mud-palette-primary) 70%, var(--mud-palette-text-primary))")]
    [InlineData("color-mix(in srgb, var(--mud-palette-warning) 70%, var(--mud-palette-text-primary))")]
    [InlineData("inset 3px 0 0 var(--mud-palette-primary)")]
    [InlineData("inset 2px 0 0 var(--mud-palette-primary)")]
    [InlineData("outline: 2px solid var(--mud-palette-text-primary)")]
    [InlineData("border-bottom: 1px solid var(--mud-palette-lines-default)")]
    [InlineData("border-top: 1px solid var(--mud-palette-lines-default)")]
    [InlineData("border-inline-end: 1px solid var(--mud-palette-lines-default)")]
    public void Range_HoldsNoReplacedRecipe(string recipe)
    {
        Assert.DoesNotContain(recipe, Range(), StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
    }

    /// <summary>The Board lane is a Surface-grounded panel whose fill is the sunk ground; no plain Surface fill is left on a lane or card.</summary>
    [Fact]
    public void Range_LaneAndCardGroundsAreTokens()
    {
        Assert.DoesNotContain("background-color: var(--mud-palette-surface);", CssText.RuleBody(Range(), ".task-board-column"), StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.DoesNotContain("background-color: var(--mud-palette-surface);", CssText.RuleBody(Range(), ".task-card"), StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
    }
}
