namespace Agency.Huddle.Tests.Ui.References;

/// <summary>
/// Pins the <c>/* References */</c> block in <c>app.css</c> as a source fact (AT-T13, spec §5.6 and §7
/// step 4): the picker opens above the composer in the 0.9 space, the block sets no <c>zoom</c>, the zoom
/// comment names the picker, and a Room or Team link is styled exactly as a Task link.
/// </summary>
public sealed class AppCssReferencesTests
{
    private const string BlockMarker = "/* References */";

    /// <summary>The picker sits above the composer and its height is <c>min(320px, calc(40vh / 0.9))</c>.</summary>
    [Fact]
    public void Picker_OpensAboveWithZoomAwareHeight()
    {
        string rule = PickerRule();

        Assert.True(rule.Contains("bottom: 100%;", StringComparison.Ordinal), "Expected bottom: 100% in the picker rule.");
        Assert.True(
            rule.Contains("max-height: min(320px, calc(40vh / 0.9));", StringComparison.Ordinal),
            "Expected the zoom-aware max-height in the picker rule.");
    }

    /// <summary>No rule in the <c>/* References */</c> block sets <c>zoom</c>; the picker takes the page scale.</summary>
    [Fact]
    public void ReferencesBlock_SetsNoZoom()
    {
        string block = Block();

        // A declaration line, not the comment prose: the property name opens the trimmed line.
        Assert.DoesNotContain(block.Split('\n'), line => line.TrimStart().StartsWith("zoom:", StringComparison.Ordinal));
    }

    /// <summary>The comment above <c>html { zoom: 0.9; }</c> names <c>.composer-picker</c> among the places that take the scale.</summary>
    [Fact]
    public void ZoomComment_NamesComposerPicker()
    {
        string text = ReadAppCss();
        int zoomIndex = text.IndexOf("zoom: 0.9;", StringComparison.Ordinal);
        Assert.True(zoomIndex >= 0, "Expected html { zoom: 0.9; } in app.css.");

        Assert.True(
            text[..zoomIndex].Contains(".composer-picker", StringComparison.Ordinal),
            "Expected .composer-picker in the zoom comment above html { zoom: 0.9; }.");
    }

    /// <summary>A Room link and a Team link take the same declarations as <c>.task-ref</c>.</summary>
    [Fact]
    public void RoomAndTeamRefs_StyledLikeTaskRef()
    {
        string text = ReadAppCss();
        string taskBody = RuleBody(text, ".task-ref {");
        string refBody = RuleBody(text, "a.room-ref, a.team-ref {");

        Assert.Equal(taskBody, refBody);
    }

    /// <summary>Reads <c>app.css</c> as the source fact every test in this class pins.</summary>
    /// <returns>The full text of <c>app.css</c>.</returns>
    private static string ReadAppCss() => File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.css"));

    /// <summary>Returns the <c>/* References */</c> block, from its marker to the end of the file.</summary>
    /// <returns>The block's text.</returns>
    private static string Block()
    {
        string text = ReadAppCss();
        int start = text.IndexOf(BlockMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, "Expected a /* References */ block in app.css.");
        return text[start..];
    }

    /// <summary>Extracts the <c>.composer-picker</c> rule from the <c>/* References */</c> block, selector included.</summary>
    /// <returns>The rule's text, from its selector to its closing brace.</returns>
    private static string PickerRule()
    {
        string block = Block();
        int classIndex = block.IndexOf(".composer-picker {", StringComparison.Ordinal);
        Assert.True(classIndex >= 0, "Expected a .composer-picker rule in the References block.");
        return block[classIndex..(block.IndexOf('}', classIndex) + 1)];
    }

    /// <summary>Returns the declarations of the first rule with the given selector, without the braces.</summary>
    /// <param name="text">The whole stylesheet.</param>
    /// <param name="selector">The selector with its opening brace, as written in the stylesheet.</param>
    /// <returns>The text between the braces, trimmed.</returns>
    private static string RuleBody(string text, string selector)
    {
        int start = text.IndexOf(selector, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Expected a rule '{selector}' in app.css.");
        int open = start + selector.Length;
        int close = text.IndexOf('}', open);
        Assert.True(close > open, $"Expected the rule '{selector}' to be closed.");
        return text[open..close].Trim();
    }
}
