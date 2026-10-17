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
        Assert.Contains("/* ---- Tasks ", text, StringComparison.Ordinal);
    }

    /// <summary>The Board's scroller lane class is declared.</summary>
    [Fact]
    public void AppCss_DeclaresTaskBoardLane()
    {
        string text = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.css"));

        // contains-ok: source-fact test, no CSS parser - app.css's own text is what's pinned.
        Assert.Contains(".task-board-lane", text, StringComparison.Ordinal);
    }

    /// <summary>OP-3 (UX-007): the Board scrolls both ways inside its pane, so its scrollbar sits at the pane's bottom edge, and its column headers stick to the top.</summary>
    [Fact]
    public void AppCss_BoardScrollsBothWays_WithStickyColumnHeaders()
    {
        string text = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.css"));

        string board = RuleBody(text, ".task-board");
        string header = RuleBody(text, ".task-board-columns");
        // contains-ok: source-fact test, no CSS parser - app.css's own declarations are what's pinned.
        Assert.Contains("overflow: auto", board, StringComparison.Ordinal);
        Assert.Contains("position: sticky", header, StringComparison.Ordinal);
        Assert.Contains("top: 0", header, StringComparison.Ordinal);
    }

    /// <summary>The declarations of the first rule whose selector list ends with <paramref name="selector"/>.</summary>
    /// <param name="css">The stylesheet text.</param>
    /// <param name="selector">The selector, for example <c>.task-board</c>.</param>
    private static string RuleBody(string css, string selector)
    {
        System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(
            css,
            @"(?:^|\n)(?:[^{}\n]*,\s*)?" + System.Text.RegularExpressions.Regex.Escape(selector) + @"\s*(?:,[^{}]*)?\{(?<body>[^}]*)\}",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(2));
        Assert.True(match.Success, $"No rule for {selector}.");
        return match.Groups["body"].Value;
    }

    /// <summary>The per-state coloured left edge is gone: the Board column names the state, so the edge repeated it.</summary>
    [Fact]
    public void AppCss_DeclaresNoTaskColEdge()
    {
        string text = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.css"));

        Assert.DoesNotContain("task-col-edge", text, StringComparison.Ordinal);
    }

    /// <summary>The ghost bucket, its droppable state, the dragged card, the overdue marker and the two chat-reference classes are all declared.</summary>
    [Theory]
    [InlineData(".task-zone-can")]
    [InlineData(".task-ghost-bucket")]
    [InlineData(".task-card")]
    [InlineData(".task-card-dragging")]
    [InlineData(".task-due-overdue")]
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

    /// <summary>The Board scrolls both ways inside its pane once its columns are wider or its lanes taller (finding F10, OP-3).</summary>
    [Fact]
    public void AppCss_DeclaresTaskBoard_WithOverflowBothWays()
    {
        string rule = RuleFor(".task-board");

        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("overflow: auto", rule, StringComparison.Ordinal);
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
        Assert.Contains("grid-auto-columns: minmax(260px, 300px)", rule, StringComparison.Ordinal);
    }

    /// <summary>The column header lays out its label, count and menu in a row (finding F10).</summary>
    [Fact]
    public void AppCss_DeclaresTaskBoardColumnHeader_AsFlex()
    {
        string rule = RuleFor(".task-board-column-header");

        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("display: flex", rule, StringComparison.Ordinal);
    }

    /// <summary>The column count is 12px secondary text after the 13px/600 label (S4).</summary>
    [Fact]
    public void AppCss_DeclaresTaskBoardColumnCount_WithPadding()
    {
        string rule = RuleFor(".task-board-column-count");

        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("font-size: var(--mud-typography-caption-size)", rule, StringComparison.Ordinal);
        Assert.Contains("color: var(--mud-palette-text-secondary)", rule, StringComparison.Ordinal);
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

    /// <summary>The List's header cells are 12px/600 secondary, its rows 36px (32px Dense) and its fixed columns 110, 120 and 40px (S5).</summary>
    [Fact]
    public void AppCss_ListHeaderRowsAndColumns_FollowTheSpec()
    {
        string css = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.css"));

        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains(".mud-table.task-list-grid .mud-table-head .mud-table-row .mud-table-cell {\n    font-size: var(--mud-typography-caption-size);\n    font-weight: 600;\n    color: var(--mud-palette-text-secondary);", css.Replace("\r\n", "\n"), StringComparison.Ordinal);
        Assert.Contains("height: 36px;", RuleFor(".mud-table.task-list-grid .mud-table-body .mud-table-row .mud-table-cell"), StringComparison.Ordinal);
        Assert.Contains("height: 32px;", RuleFor(".mud-table.task-list-grid.mud-table-dense .mud-table-body .mud-table-row .mud-table-cell"), StringComparison.Ordinal);
        Assert.Contains("width: 110px;", RuleFor(".task-list-grid thead th.task-list-id-cell"), StringComparison.Ordinal);
        Assert.Contains("width: 120px;", RuleFor(".task-list-grid thead th.task-list-priority-cell"), StringComparison.Ordinal);
        Assert.Contains("width: 40px;", RuleFor(".task-list-grid thead th:last-child"), StringComparison.Ordinal);
    }

    /// <summary>The card is 12px padded with a Divider border and 8px between cards; its title clamps at three lines and its id is 12px secondary (S4).</summary>
    [Fact]
    public void AppCss_BoardCard_FollowsTheSpec()
    {
        string card = RuleFor(".task-card");
        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("padding: 12px;", card, StringComparison.Ordinal);
        Assert.Contains("border: 1px solid var(--ink-edge);", card, StringComparison.Ordinal);
        Assert.Contains("gap: 8px;", RuleFor(".task-zone"), StringComparison.Ordinal);
        Assert.Contains("line-clamp: 3;", RuleFor(".task-card-title"), StringComparison.Ordinal);
        Assert.Contains("font-size: var(--mud-typography-caption-size);", RuleFor(".task-card-id"), StringComparison.Ordinal);
        Assert.Contains("width: 8px;", RuleFor(".task-state-dot"), StringComparison.Ordinal);
    }

    /// <summary>The drawer's label column is 120px, its footer meta 12px secondary and its title clamps at three lines (S6).</summary>
    [Fact]
    public void AppCss_TaskDrawer_FollowsTheSpec()
    {
        string css = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.css"));

        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("grid-template-columns: 120px minmax(0, 1fr);", css, StringComparison.Ordinal);
        Assert.Contains("font-size: var(--mud-typography-caption-size);", RuleFor(".task-detail-footer-meta"), StringComparison.Ordinal);
        Assert.Contains("line-clamp: 3;", RuleFor(".resizable-drawer-title"), StringComparison.Ordinal);
        Assert.Contains("margin: 0;", RuleFor(".task-detail-group > .group-label"), StringComparison.Ordinal);
    }

    /// <summary>A Board lane is a Surface panel with a Divider hairline and the card radius, not a Background-gray tint that is heavy in Light and invisible in Dark.</summary>
    [Fact]
    public void AppCss_BoardLane_IsASurfaceWithADividerBorder()
    {
        string lane = RuleFor(".task-board-column");

        Assert.Contains("background-color: var(--mud-palette-surface);", lane, StringComparison.Ordinal);
        Assert.Contains("border: 1px solid var(--ink-edge);", lane, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.Contains("border-radius: var(--mud-default-borderradius);", lane, StringComparison.Ordinal);
        Assert.DoesNotContain("background-gray", lane, StringComparison.Ordinal);
    }

    /// <summary>The Backlog column's dot is a hollow dashed ring, so it differs from To Do now that both are Default.</summary>
    [Fact]
    public void AppCss_BacklogStateDot_IsHollowAndDashed()
    {
        string dot = RuleFor(".task-state-dot-backlog");

        Assert.Contains("background: transparent;", dot, StringComparison.Ordinal);
        Assert.Contains("border: 1px dashed var(--mud-palette-text-secondary);", dot, StringComparison.Ordinal);
    }

    /// <summary>A card's due date is 12px like the id, and the overdue icon takes the date's size (1em), never the subtitle2 size.</summary>
    [Fact]
    public void AppCss_CardDueDate_IsCaptionSized_AndTheOverdueIconFollowsIt()
    {
        Assert.Contains("font-size: var(--mud-typography-caption-size);", RuleFor(".task-card-field-due-date"), StringComparison.Ordinal);
        string icon = RuleFor(".task-due-overdue .task-due-icon");
        Assert.Contains("width: 1em;", icon, StringComparison.Ordinal);
        Assert.Contains("height: 1em;", icon, StringComparison.Ordinal);
        Assert.Contains("font-size: inherit;", icon, StringComparison.Ordinal);
        Assert.DoesNotContain("subtitle2", icon, StringComparison.Ordinal);
        string overdue = RuleFor(".task-due-overdue");
        Assert.Contains("font-weight: 600;", overdue, StringComparison.Ordinal);
        Assert.Contains("var(--mud-palette-error)", overdue, StringComparison.Ordinal);
    }
}
