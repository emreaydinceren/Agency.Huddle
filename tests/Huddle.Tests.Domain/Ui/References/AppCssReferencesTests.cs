using Agency.Huddle.Tests.Css;

namespace Agency.Huddle.Tests.Ui.References;

/// <summary>
/// Pins the <c>/* References */</c> block in <c>app.css</c> as a source fact (AT-T13, spec §5.6 and §7
/// step 4): the picker opens above the composer with a viewport-unit height (the page is not zoomed,
/// ADR-0056), the block sets no <c>zoom</c>, and a Room or Team link is styled exactly as a Task link.
/// </summary>
public sealed class AppCssReferencesTests
{
    private const string BlockMarker = "/* ---- References ";

    /// <summary>The picker sits above the composer and its height is <c>min(320px, 40vh)</c>.</summary>
    [Fact]
    public void Picker_OpensAboveWithAViewportHeight()
    {
        string rule = PickerRule();

        Assert.True(rule.Contains("bottom: 100%;", StringComparison.Ordinal), "Expected bottom: 100% in the picker rule.");
        Assert.True(
            rule.Contains("max-height: min(320px, 40vh);", StringComparison.Ordinal),
            "Expected the viewport-unit max-height in the picker rule.");
    }

    /// <summary>No rule in the <c>/* References */</c> block sets <c>zoom</c>; the picker is laid out at the page's own scale.</summary>
    [Fact]
    public void ReferencesBlock_SetsNoZoom()
    {
        string block = Block();

        // A declaration line, not the comment prose: the property name opens the trimmed line.
        Assert.DoesNotContain(block.Split('\n'), line => line.TrimStart().StartsWith("zoom:", StringComparison.Ordinal));
    }

    /// <summary>
    /// A Room link and a Team link take the same declarations as <c>.task-ref</c> because one
    /// shared <c>.ref-link</c> rule lists all of them (UX-R22).
    /// </summary>
    [Fact]
    public void RoomAndTeamRefs_StyledLikeTaskRef()
    {
        string text = CssText.AppCss(CssNewlines.AsIs);

        Assert.Matches(@"\.task-ref,\s*\.library-ref,\s*a\.room-ref,\s*a\.team-ref,\s*\.ref-link\s*\{", text);

        Assert.Matches(@"\.ref-link\s*\{[^}]*text-decoration:\s*underline dotted", text);
    }

    /// <summary>Returns the <c>/* References */</c> block, from its marker to the end of the file.</summary>
    /// <returns>The block's text.</returns>
    private static string Block()
    {
        string text = CssText.AppCss(CssNewlines.AsIs);
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
}
