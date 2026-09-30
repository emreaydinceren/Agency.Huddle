namespace Agency.Huddle.Tests.Ui.Tasks;

/// <summary>
/// Pins the Spec §13.10 <c>/* Tasks */</c> block in <c>app.css</c> as a source fact: every class the
/// Board and List need that a MudBlazor component parameter cannot supply.
/// </summary>
public sealed class AppCssTasksTests
{
    /// <summary><c>app.css</c> carries a <c>/* Tasks */</c> block.</summary>
    [Fact]
    public void AppCss_HasTasksBlock()
    {
        string text = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.css"));

        // contains-ok: source-fact test, no CSS parser - app.css's own text is what's pinned.
        Assert.Contains("/* Tasks */", text, StringComparison.Ordinal);
    }

    /// <summary>The Board's scroller lane class is declared.</summary>
    [Fact]
    public void AppCss_DeclaresTaskBoardLane()
    {
        string text = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.css"));

        // contains-ok: source-fact test, no CSS parser - app.css's own text is what's pinned.
        Assert.Contains(".task-board-lane", text, StringComparison.Ordinal);
    }

    /// <summary>One column-edge class per state colour family: Default, Info, Warning, Secondary, Success, Error.</summary>
    [Theory]
    [InlineData(".task-col-edge-default")]
    [InlineData(".task-col-edge-info")]
    [InlineData(".task-col-edge-warning")]
    [InlineData(".task-col-edge-secondary")]
    [InlineData(".task-col-edge-success")]
    [InlineData(".task-col-edge-error")]
    public void AppCss_DeclaresTaskColEdge_ForEveryStateFamily(string className)
    {
        string text = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.css"));

        // contains-ok: source-fact test, no CSS parser - app.css's own text is what's pinned.
        Assert.Contains(className, text, StringComparison.Ordinal);
    }

    /// <summary>The ghost bucket, its droppable state, the dragged card, the overdue marker and the two chat-reference classes are all declared.</summary>
    [Theory]
    [InlineData(".task-zone-can")]
    [InlineData(".task-ghost-bucket")]
    [InlineData(".task-card")]
    [InlineData(".task-card-dragging")]
    [InlineData(".task-overdue")]
    [InlineData(".task-ref")]
    [InlineData(".task-ref-closed")]
    public void AppCss_DeclaresRemainingTaskClasses(string className)
    {
        string text = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.css"));

        // contains-ok: source-fact test, no CSS parser - app.css's own text is what's pinned.
        Assert.Contains(className, text, StringComparison.Ordinal);
    }

    /// <summary>The id text carries <c>user-select: all</c>, so a single click selects it for Ctrl+C if the copy button fails (Spec §13.13.1, corrections-B6 "D15.2" item 6).</summary>
    [Fact]
    public void AppCss_DeclaresTaskId_WithUserSelectAll()
    {
        string text = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.css"));

        int classIndex = text.IndexOf(".task-id", StringComparison.Ordinal);
        Assert.True(classIndex >= 0, "Expected a .task-id rule in app.css.");
        int braceEnd = text.IndexOf('}', classIndex);
        Assert.True(braceEnd > classIndex, "Expected .task-id's rule to be closed.");
        string rule = text[classIndex..(braceEnd + 1)];
        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("user-select: all", rule, StringComparison.Ordinal);
    }

    /// <summary>The Board scrolls horizontally once its columns are wider than the page (finding F10).</summary>
    [Fact]
    public void AppCss_DeclaresTaskBoard_WithOverflowX()
    {
        string rule = RuleFor(".task-board");

        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("overflow-x: auto", rule, StringComparison.Ordinal);
    }

    /// <summary>
    /// The header row and every lane's row of columns share one grid declaration, so a header
    /// cell lines up above the cards it describes (finding F10).
    /// </summary>
    [Fact]
    public void AppCss_DeclaresTaskBoardColumns_AsGrid()
    {
        string rule = RuleFor(".task-board-lane-columns");

        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("display: grid", rule, StringComparison.Ordinal);
        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("grid-auto-columns: 310px", rule, StringComparison.Ordinal);
    }

    /// <summary>The column header lays out its label, count and menu in a row (finding F10).</summary>
    [Fact]
    public void AppCss_DeclaresTaskBoardColumnHeader_AsFlex()
    {
        string rule = RuleFor(".task-board-column-header");

        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("display: flex", rule, StringComparison.Ordinal);
    }

    /// <summary>The count chip is spaced away from the label it used to run into (finding F10).</summary>
    [Fact]
    public void AppCss_DeclaresTaskBoardColumnCount_WithPadding()
    {
        string rule = RuleFor(".task-board-column-count");

        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("padding:", rule, StringComparison.Ordinal);
    }

    /// <summary>A lane is a vertical band: its label spans the full width above its own row of columns (finding F10).</summary>
    [Fact]
    public void AppCss_DeclaresTaskBoardLane_AsFlexColumn()
    {
        string rule = RuleFor(".task-board-lane");

        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("display: flex", rule, StringComparison.Ordinal);
        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("flex-direction: column", rule, StringComparison.Ordinal);
    }

    /// <summary>Every zone stacks its cards vertically with a gap and keeps a min-height so an empty column is still a drop target (finding F10).</summary>
    [Fact]
    public void AppCss_DeclaresTaskZone_AsFlexColumnWithMinHeight()
    {
        string rule = RuleFor(".task-zone");

        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("display: flex", rule, StringComparison.Ordinal);
        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("flex-direction: column", rule, StringComparison.Ordinal);
        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("min-height:", rule, StringComparison.Ordinal);
    }

    /// <summary>The drag caption sits on its own row under the header, not crowding the menu button (finding F10).</summary>
    [Fact]
    public void AppCss_DeclaresTaskBoardDragCaption_FullWidth()
    {
        string rule = RuleFor(".task-board-drag-caption");

        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("flex: 1 1 100%", rule, StringComparison.Ordinal);
    }

    /// <summary>A ghost bucket lays its icon and state name out in a row (finding F10).</summary>
    [Fact]
    public void AppCss_DeclaresTaskGhostBucket_AsFlexRow()
    {
        string rule = RuleFor(".task-ghost-bucket");

        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("display: flex", rule, StringComparison.Ordinal);
    }

    /// <summary>
    /// The exact rule text opened by <paramref name="selector"/> in <c>app.css</c>, from its
    /// declaration to its closing brace. Anchors on <c>"{selector} {{"</c> (the selector
    /// immediately followed by a space and the opening brace) rather than a bare substring
    /// search, so e.g. <c>.task-ghost-bucket</c> finds its own rule and not the unrelated
    /// <c>.task-ghost-bucket-label</c> rule, and a class name spelled out in a comment's prose -
    /// never followed there by " {" - is never mistaken for the rule itself.
    /// </summary>
    /// <param name="selector">The selector text to find, verbatim as written in <c>app.css</c>.</param>
    private static string RuleFor(string selector)
    {
        string text = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.css"));

        string anchor = $"{selector} {{";
        int selectorIndex = text.IndexOf(anchor, StringComparison.Ordinal);
        Assert.True(selectorIndex >= 0, $"Expected a '{selector}' rule in app.css.");
        int braceStart = selectorIndex + anchor.Length - 1;
        int braceEnd = text.IndexOf('}', braceStart);
        Assert.True(braceEnd > braceStart, $"Expected '{selector}''s rule to be closed.");
        return text[selectorIndex..(braceEnd + 1)];
    }

    /// <summary>Manual test TASKS-07 finding F17: the Panel's stacked conflict row is scoped to <c>.task-detail-conflict-stacked</c> and laid out as a column, so it never reaches the Expanded layout's table row.</summary>
    [Fact]
    public void AppCss_DeclaresConflictStackedRow_ScopedAndColumnLaidOut()
    {
        string text = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.css"));

        int classIndex = text.IndexOf(".task-detail-conflict-stacked .task-detail-conflict-row", StringComparison.Ordinal);
        Assert.True(classIndex >= 0, "Expected a scoped .task-detail-conflict-stacked .task-detail-conflict-row rule in app.css.");
        int braceEnd = text.IndexOf('}', classIndex);
        Assert.True(braceEnd > classIndex, "Expected the rule to be closed.");
        string rule = text[classIndex..(braceEnd + 1)];
        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("flex-direction: column", rule, StringComparison.Ordinal);
    }
}
