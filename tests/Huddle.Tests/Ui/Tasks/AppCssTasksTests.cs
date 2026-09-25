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
}
