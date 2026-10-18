namespace Agency.Huddle.Tests.Ui.Sidebar;

/// <summary>
/// Pins the Right sidebar panel header, body and the <c>window.huddleSidebar</c> collapse helper (Right sidebar
/// spec D-8) as source facts, because the repo has no JavaScript test runner - the same source-text approach
/// <see cref="Tasks.AppJsSourceTests"/> uses for <c>app.js</c> and <c>app.css</c>.
/// </summary>
public sealed class SidebarCollapseSourceTests
{
    /// <summary>Both <c>window.huddleSidebar</c> functions wrap their storage access in a try block (Right sidebar spec D-8).</summary>
    [Fact]
    public void AppJs_HuddleSidebar_GuardsEveryStorageAccess()
    {
        string block = SidebarCollapseSourceTests.HuddleSidebarBlock();
        string isCollapsed = SidebarCollapseSourceTests.FunctionBlock(block, "isCollapsed(id) {", "setCollapsed(id, collapsed) {");
        string setCollapsed = SidebarCollapseSourceTests.FunctionBlock(block, "setCollapsed(id, collapsed) {", null);

        // contains-ok: source-fact test, no JS runner - app.js's own text is what's pinned.
        Assert.Contains("try {", isCollapsed, StringComparison.Ordinal);
        // contains-ok: source-fact test, no JS runner - app.js's own text is what's pinned.
        Assert.Contains("try {", setCollapsed, StringComparison.Ordinal);
    }

    /// <summary>The <c>window.huddleSidebar</c> keys are built from the <c>huddle.sidebar.collapsed.</c> prefix plus the panel id (Right sidebar spec D-8).</summary>
    [Fact]
    public void AppJs_HuddleSidebar_UsesThePanelKeyPrefix()
    {
        string block = SidebarCollapseSourceTests.HuddleSidebarBlock();

        // contains-ok: source-fact test, no JS runner - app.js's own text is what's pinned.
        Assert.Contains("\"huddle.sidebar.collapsed.\" + id", block, StringComparison.Ordinal);
    }

    /// <summary>The sidebar panel header, count, title and body rules use only palette variables, never a hex or <c>rgb</c> colour (Right sidebar spec D-8, plan T4.4).</summary>
    [Fact]
    public void AppCss_SidebarPanel_UsesOnlyPaletteColours()
    {
        string css = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.css"));
        int start = css.IndexOf(".sidebar-panel-header {", StringComparison.Ordinal);
        int bodyStart = css.IndexOf(".sidebar-panel-body {", StringComparison.Ordinal);
        Assert.True(start >= 0 && bodyStart > start, "Expected the .sidebar-panel-header rules to be followed by .sidebar-panel-body.");
        int end = css.IndexOf('}', bodyStart);
        Assert.True(end > bodyStart, "Expected the .sidebar-panel-body rule to close.");
        string rules = css[start..(end + 1)];

        Assert.DoesNotContain("#", rules, StringComparison.Ordinal);
        Assert.DoesNotContain("rgb(", rules, StringComparison.Ordinal);
        Assert.DoesNotContain("rgba(", rules, StringComparison.Ordinal);
    }

    /// <summary>Returns the <c>window.huddleSidebar</c> object literal, from its name to the <c>};</c> that closes it.</summary>
    private static string HuddleSidebarBlock()
    {
        string text = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.js"));
        int start = text.IndexOf("window.huddleSidebar", StringComparison.Ordinal);
        int end = start < 0 ? -1 : text.IndexOf("\n};", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, "Expected window.huddleSidebar to be an object literal closed by \"};\".");
        return text[start..(end + 3)];
    }

    /// <summary>Returns the text from <paramref name="startMarker"/> up to <paramref name="endMarker"/>, or to the end of <paramref name="block"/> when it is null.</summary>
    private static string FunctionBlock(string block, string startMarker, string? endMarker)
    {
        int start = block.IndexOf(startMarker, StringComparison.Ordinal);
        int end = endMarker is null ? block.Length : block.IndexOf(endMarker, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, $"Expected '{startMarker}' to be present in window.huddleSidebar.");
        return block[start..end];
    }
}
