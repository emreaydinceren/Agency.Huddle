namespace Agency.Huddle.Tests.Ui.Sidebar;

/// <summary>
/// Pins the two script entry points the Outline panel's page calls - <c>revealLine</c> on the editor handle and
/// <c>libraryDocument.scrollToHeading</c> in <c>library-document.js</c> - as source facts, because the repo has no JavaScript test runner
/// (plan T7.3; the same approach <see cref="SidebarCollapseSourceTests"/> uses).
/// </summary>
public sealed class LibraryEditorRevealSourceTests
{
    /// <summary>The editor handle returned by <c>create</c> exposes a <c>revealLine</c> function (plan T7.3).</summary>
    [Fact]
    public void LibraryEditorJs_Handle_HasRevealLine()
    {
        string text = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "library-editor.js"));

        // contains-ok: source-fact test, no JS runner - library-editor.js's own text is what's pinned.
        Assert.Contains("revealLine: (line) =>", text, StringComparison.Ordinal);
    }

    /// <summary>The <c>revealLine</c> body clamps the requested line into 1..document length before reading the line (plan T7.3).</summary>
    [Fact]
    public void LibraryEditorJs_RevealLine_ClampsToTheDocument()
    {
        string text = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "library-editor.js"));
        int start = text.IndexOf("revealLine: (line) =>", StringComparison.Ordinal);
        Assert.True(start >= 0, "Expected the handle to define revealLine.");
        int end = text.IndexOf("dispose:", start, StringComparison.Ordinal);
        Assert.True(end > start, "Expected revealLine to be followed by dispose.");
        string body = text[start..end];

        // contains-ok: source-fact test, no JS runner - library-editor.js's own text is what's pinned.
        Assert.Contains("Math.max(1, line)", body, StringComparison.Ordinal);
        // contains-ok: source-fact test, no JS runner - library-editor.js's own text is what's pinned.
        Assert.Contains("view.state.doc.lines", body, StringComparison.Ordinal);
    }

    /// <summary><c>library-document.js</c> defines <c>scrollToHeading(root, id)</c> and exports it (plan T7.3).</summary>
    [Fact]
    public void LibraryDocumentJs_HasScrollToHeading()
    {
        string block = LibraryEditorRevealSourceTests.LibraryDocumentBlock();

        // contains-ok: source-fact test, no JS runner - library-document.js's own text is what's pinned.
        Assert.Contains("scrollToHeading(root, id) {", block, StringComparison.Ordinal);
        string text = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "library-document.js"));
        // contains-ok: source-fact test, no JS runner - library-document.js's own text is what's pinned.
        Assert.Contains("scrollToHeading", text[text.IndexOf("\nexport const {", StringComparison.Ordinal)..], StringComparison.Ordinal);
    }

    /// <summary><c>scrollToHeading</c> builds its selector through <c>CSS.escape</c>, so an id with selector characters cannot break the query (plan T7.3).</summary>
    [Fact]
    public void LibraryDocumentJs_ScrollToHeading_EscapesTheId()
    {
        string block = LibraryEditorRevealSourceTests.LibraryDocumentBlock();
        int start = block.IndexOf("scrollToHeading(root, id) {", StringComparison.Ordinal);
        Assert.True(start >= 0, "Expected libraryDocument to define scrollToHeading.");

        // contains-ok: source-fact test, no JS runner - library-document.js's own text is what's pinned.
        Assert.Contains("CSS.escape(id)", block[start..], StringComparison.Ordinal);
    }

    /// <summary>Returns the <c>libraryDocument</c> object literal in <c>library-document.js</c>, from its name to the <c>};</c> that closes it.</summary>
    private static string LibraryDocumentBlock()
    {
        string text = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "library-document.js"));
        int start = text.IndexOf("const libraryDocument = {", StringComparison.Ordinal);
        int end = start < 0 ? -1 : text.IndexOf("\n};", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, "Expected const libraryDocument in library-document.js to be an object literal closed by \"};\".");
        return text[start..(end + 3)];
    }
}
