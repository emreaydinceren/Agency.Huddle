using Agency.Huddle.Tests.Css;

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
        string text = CssText.AppCss(CssNewlines.AsIs);

        // contains-ok: source-fact test, no CSS parser - app.css's own text is what's pinned.
        Assert.Contains("/* ---- Tasks ", text, StringComparison.Ordinal);
    }

    /// <summary>The Board's scroller lane class is declared.</summary>
    [Fact]
    public void AppCss_DeclaresTaskBoardLane()
    {
        string text = CssText.AppCss(CssNewlines.AsIs);

        // contains-ok: source-fact test, no CSS parser - app.css's own text is what's pinned.
        Assert.Contains(".task-board-lane", text, StringComparison.Ordinal);
    }

    /// <summary>OP-3 (UX-007): the Board scrolls both ways inside its pane, so its scrollbar sits at the pane's bottom edge, and its column headers stick to the top.</summary>
    [Fact]
    public void AppCss_BoardScrollsBothWays_WithStickyColumnHeaders()
    {
        string text = CssText.AppCss(CssNewlines.AsIs);

        string board = CssText.RuleBodyInList(text, ".task-board");
        string header = CssText.RuleBodyInList(text, ".task-board-columns");
        // contains-ok: source-fact test, no CSS parser - app.css's own declarations are what's pinned.
        Assert.Contains("overflow: auto", board, StringComparison.Ordinal);
        Assert.Contains("position: sticky", header, StringComparison.Ordinal);
        Assert.Contains("top: 0", header, StringComparison.Ordinal);
    }

    /// <summary>The per-state coloured left edge is gone: the Board column names the state, so the edge repeated it.</summary>
    [Fact]
    public void AppCss_DeclaresNoTaskColEdge()
    {
        string text = CssText.AppCss(CssNewlines.AsIs);

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
        string text = CssText.AppCss(CssNewlines.AsIs);

        // contains-ok: source-fact test, no CSS parser - app.css's own text is what's pinned.
        Assert.Contains(className, text, StringComparison.Ordinal);
    }

    /// <summary>The id text carries <c>user-select: all</c>, so a single click selects it for Ctrl+C if the copy button fails (Spec §13.13.1, corrections-B6 "D15.2" item 6).</summary>
    [Fact]
    public void AppCss_DeclaresTaskId_WithUserSelectAll()
    {
        string text = CssText.AppCss(CssNewlines.AsIs);

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
        string rule = CssText.RuleBody(CssText.AppCss(CssNewlines.AsIs), ".task-board");

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
        string rule = CssText.RuleBody(CssText.AppCss(CssNewlines.AsIs), ".task-board-lane-columns");

        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("display: grid", rule, StringComparison.Ordinal);
        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("grid-auto-columns: minmax(260px, 300px)", rule, StringComparison.Ordinal);
    }

    /// <summary>The column header lays out its label, count and menu in a row (finding F10).</summary>
    [Fact]
    public void AppCss_DeclaresTaskBoardColumnHeader_AsFlex()
    {
        string rule = CssText.RuleBody(CssText.AppCss(CssNewlines.AsIs), ".task-board-column-header");

        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("display: flex", rule, StringComparison.Ordinal);
    }

    /// <summary>The column count is 12px secondary text after the 13px/600 label (S4).</summary>
    [Fact]
    public void AppCss_DeclaresTaskBoardColumnCount_WithPadding()
    {
        string rule = CssText.RuleBody(CssText.AppCss(CssNewlines.AsIs), ".task-board-column-count");

        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("font-size: var(--mud-typography-caption-size)", rule, StringComparison.Ordinal);
        Assert.Contains("color: var(--mud-palette-text-secondary)", rule, StringComparison.Ordinal);
    }

    /// <summary>A lane is a vertical band: its label spans the full width above its own row of columns (finding F10).</summary>
    [Fact]
    public void AppCss_DeclaresTaskBoardLane_AsFlexColumn()
    {
        string rule = CssText.RuleBody(CssText.AppCss(CssNewlines.AsIs), ".task-board-lane");

        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("display: flex", rule, StringComparison.Ordinal);
        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("flex-direction: column", rule, StringComparison.Ordinal);
    }

    /// <summary>Every zone stacks its cards vertically with a gap and keeps a min-height so an empty column is still a drop target (finding F10).</summary>
    [Fact]
    public void AppCss_DeclaresTaskZone_AsFlexColumnWithMinHeight()
    {
        string rule = CssText.RuleBody(CssText.AppCss(CssNewlines.AsIs), ".task-zone");

        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("display: flex", rule, StringComparison.Ordinal);
        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("flex-direction: column", rule, StringComparison.Ordinal);
        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("min-height:", rule, StringComparison.Ordinal);
    }

    /// <summary>A zone's drag hint ("Notifies Nova", smart wakes 5.5) reads in the secondary text colour, a design token and not a literal, and the retired Board-wide caption rule is gone.</summary>
    [Fact]
    public void AppCss_DeclaresTaskZoneWakeHint_InSecondaryText_AndNoDragCaptionRule()
    {
        string css = CssText.AppCss(CssNewlines.AsIs);
        string rule = CssText.RuleBody(css, ".task-zone-wake-hint");

        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("color: var(--mud-palette-text-secondary)", rule, StringComparison.Ordinal);
        // contains-ok: source-fact test, no CSS parser - the absence of the retired selector is what's pinned.
        Assert.DoesNotContain(".task-board-drag-caption", css, StringComparison.Ordinal);
    }

    /// <summary>A ghost bucket lays its icon and state name out in a row (finding F10).</summary>
    [Fact]
    public void AppCss_DeclaresTaskGhostBucket_AsFlexRow()
    {
        string rule = CssText.RuleBody(CssText.AppCss(CssNewlines.AsIs), ".task-ghost-bucket");

        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("display: flex", rule, StringComparison.Ordinal);
    }

    /// <summary>The List's header cells are 12px/600 secondary, its header and body rows 32px (one row height, no Dense mode) and its fixed columns 110, 120 and 40px (S5).</summary>
    [Fact]
    public void AppCss_ListHeaderRowsAndColumns_FollowTheSpec()
    {
        string css = CssText.AppCss(CssNewlines.AsIs);

        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains(".mud-table.task-list-grid .mud-table-head .mud-table-row .mud-table-cell {\n    font-size: var(--mud-typography-caption-size);\n    font-weight: 600;\n    color: var(--mud-palette-text-secondary);", css.Replace("\r\n", "\n"), StringComparison.Ordinal);
        Assert.Contains("height: 32px;", CssText.RuleBody(CssText.AppCss(CssNewlines.AsIs), ".mud-table.task-list-grid .mud-table-body .mud-table-row .mud-table-cell"), StringComparison.Ordinal);
        Assert.Contains("height: 32px;", CssText.RuleBody(CssText.AppCss(CssNewlines.AsIs), ".mud-table.task-list-grid .mud-table-head .mud-table-row .mud-table-cell"), StringComparison.Ordinal);
        Assert.DoesNotContain(".mud-table-dense .mud-table-body", CssText.AppCss(CssNewlines.AsIs), StringComparison.Ordinal);
        Assert.Contains("width: 110px;", CssText.RuleBody(CssText.AppCss(CssNewlines.AsIs), ".task-list-grid thead th.task-list-id-cell"), StringComparison.Ordinal);
        Assert.Contains("width: 120px;", CssText.RuleBody(CssText.AppCss(CssNewlines.AsIs), ".task-list-grid thead th.task-list-priority-cell"), StringComparison.Ordinal);
        Assert.Contains("width: 40px;", CssText.RuleBody(CssText.AppCss(CssNewlines.AsIs), ".task-list-grid thead th:last-child"), StringComparison.Ordinal);
    }

    /// <summary>The card is 12px padded with a Divider border and 8px between cards; its title clamps at three lines and its id is 12px secondary (S4).</summary>
    [Fact]
    public void AppCss_BoardCard_FollowsTheSpec()
    {
        string card = CssText.RuleBody(CssText.AppCss(CssNewlines.AsIs), ".task-card");
        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("padding: 12px;", card, StringComparison.Ordinal);
        Assert.Contains("border: 1px solid var(--ink-line);", card, StringComparison.Ordinal);
        Assert.Contains("gap: 8px;", CssText.RuleBody(CssText.AppCss(CssNewlines.AsIs), ".task-zone"), StringComparison.Ordinal);
        Assert.Contains("line-clamp: 3;", CssText.RuleBody(CssText.AppCss(CssNewlines.AsIs), ".task-card-title"), StringComparison.Ordinal);
        Assert.Contains("font-size: var(--mud-typography-caption-size);", CssText.RuleBody(CssText.AppCss(CssNewlines.AsIs), ".task-card-id"), StringComparison.Ordinal);
        Assert.Contains("width: 8px;", CssText.RuleBody(CssText.AppCss(CssNewlines.AsIs), ".task-state-dot"), StringComparison.Ordinal);
    }

    /// <summary>The drawer's label column is 120px, its footer meta 12px secondary and its title clamps at three lines (S6).</summary>
    [Fact]
    public void AppCss_TaskDrawer_FollowsTheSpec()
    {
        string css = CssText.AppCss(CssNewlines.AsIs);

        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("grid-template-columns: 120px minmax(0, 1fr);", css, StringComparison.Ordinal);
        Assert.Contains("font-size: var(--mud-typography-caption-size);", CssText.RuleBody(CssText.AppCss(CssNewlines.AsIs), ".task-detail-footer-meta"), StringComparison.Ordinal);
        Assert.Contains("margin: 0;", CssText.RuleBody(CssText.AppCss(CssNewlines.AsIs), ".task-detail-group > .group-label"), StringComparison.Ordinal);
    }

    /// <summary>A Board lane is a Surface panel with a Divider hairline and the card radius, not a Background-gray tint that is heavy in Light and invisible in Dark.</summary>
    [Fact]
    public void AppCss_BoardLane_IsASurfaceWithADividerBorder()
    {
        string lane = CssText.RuleBody(CssText.AppCss(CssNewlines.AsIs), ".task-board-column");

        Assert.Contains("background-color: var(--hu-board-column-background);", lane, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.Contains("border: 1px solid var(--hu-board-column-line);", lane, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.Contains("border-radius: var(--hu-radius-panel);", lane, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.DoesNotContain("background-gray", lane, StringComparison.Ordinal);
    }

    /// <summary>The Backlog column's dot is a hollow dashed ring, so it differs from To Do now that both are Default.</summary>
    [Fact]
    public void AppCss_BacklogStateDot_IsHollowAndDashed()
    {
        string dot = CssText.RuleBody(CssText.AppCss(CssNewlines.AsIs), ".task-state-dot-backlog");

        Assert.Contains("background: transparent;", dot, StringComparison.Ordinal);
        Assert.Contains("border: 1px dashed var(--mud-palette-text-secondary);", dot, StringComparison.Ordinal);
    }

    /// <summary>A card's due date is 12px like the id, and the overdue icon takes the date's size (1em), never the subtitle2 size.</summary>
    [Fact]
    public void AppCss_CardDueDate_IsCaptionSized_AndTheOverdueIconFollowsIt()
    {
        Assert.Contains("font-size: var(--mud-typography-caption-size);", CssText.RuleBody(CssText.AppCss(CssNewlines.AsIs), ".task-card-field-due-date"), StringComparison.Ordinal);
        string icon = CssText.RuleBody(CssText.AppCss(CssNewlines.AsIs), ".task-due-overdue .task-due-icon");
        Assert.Contains("width: 1em;", icon, StringComparison.Ordinal);
        Assert.Contains("height: 1em;", icon, StringComparison.Ordinal);
        Assert.Contains("font-size: inherit;", icon, StringComparison.Ordinal);
        Assert.DoesNotContain("subtitle2", icon, StringComparison.Ordinal);
        string overdue = CssText.RuleBody(CssText.AppCss(CssNewlines.AsIs), ".task-due-overdue");
        Assert.Contains("font-weight: 600;", overdue, StringComparison.Ordinal);
        Assert.Contains("var(--mud-palette-error)", overdue, StringComparison.Ordinal);
    }
}
