namespace Agency.Huddle.Tests.Ui.Search;

/// <summary>
/// Pins the Spec §6.8 <c>/* Global search */</c> block in <c>app.css</c> as a source fact: the dropdown
/// is sized in the 0.9 space, its viewport units divide by the scale, no search rule sets <c>zoom</c>
/// itself, and the zoom comment names the dropdown so a change of scale changes it too.
/// </summary>
public sealed class AppCssSearchTests
{
    /// <summary>The dropdown's maximum height is 80% of the window in the 0.9 space: <c>calc(80vh / 0.9)</c>.</summary>
    [Fact]
    public void SearchDropdown_MaxHeightIs80vhDividedByTheScale()
    {
        string rule = DropdownRule();

        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("max-height: calc(80vh / 0.9);", rule, StringComparison.Ordinal);
    }

    /// <summary>The dropdown's width is capped at 720px and never wider than the window less a 24px margin each side: <c>min(720px, calc((100vw - 48px) / 0.9))</c>.</summary>
    [Fact]
    public void SearchDropdown_WidthNeverExceedsTheWindow()
    {
        string rule = DropdownRule();

        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("width: min(720px, calc((100vw - 48px) / 0.9));", rule, StringComparison.Ordinal);
    }

    /// <summary>No rule in the <c>/* Global search */</c> block sets <c>zoom</c>; the scale belongs to the provider and <c>html</c> only.</summary>
    [Fact]
    public void SearchRules_NeverSetZoom()
    {
        string text = ReadAppCss();
        int start = text.IndexOf("/* Global search */", StringComparison.Ordinal);
        Assert.True(start >= 0, "Expected a /* Global search */ block in app.css.");

        string block = text[start..];
        string[] lines = block.Split('\n');

        // A declaration line, not the comment prose: the property name opens the trimmed line.
        Assert.DoesNotContain(lines, line => line.TrimStart().StartsWith("zoom:", StringComparison.Ordinal));
    }

    /// <summary>The comment above <c>html { zoom: 0.9; }</c> names <c>.search-dropdown</c> among the places that take the scale.</summary>
    [Fact]
    public void ZoomComment_NamesTheSearchDropdown()
    {
        string text = ReadAppCss();
        int zoomIndex = text.IndexOf("zoom: 0.9;", StringComparison.Ordinal);
        Assert.True(zoomIndex >= 0, "Expected html { zoom: 0.9; } in app.css.");

        // contains-ok: source-fact test, no CSS parser - the comment above the declaration is what's pinned.
        Assert.Contains(".search-dropdown", text[..zoomIndex], StringComparison.Ordinal);
    }

    /// <summary>The match mark a card draws is bold: a <c>.search-card mark</c> rule sets <c>font-weight: 600</c>.</summary>
    [Fact]
    public void SearchCardMark_IsBold()
    {
        string text = ReadAppCss();
        int block = text.IndexOf("/* Global search */", StringComparison.Ordinal);
        int rule = text.IndexOf(".search-card mark {", block, StringComparison.Ordinal);
        Assert.True(rule > block, "Expected a .search-card mark rule in the Global search block.");

        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("font-weight: 600;", text[rule..(text.IndexOf('}', rule) + 1)], StringComparison.Ordinal);
    }

    /// <summary>The search box is filled with <c>var(--mud-palette-surface)</c>, the composer's own fill, in rest, hover and focus.</summary>
    [Fact]
    public void SearchBox_FillMatchesTheComposer()
    {
        string text = ReadAppCss();
        int composer = text.IndexOf(".composer-input {", StringComparison.Ordinal);
        string composerRule = text[composer..(text.IndexOf('}', composer) + 1)];
        int block = text.IndexOf("/* Global search */", StringComparison.Ordinal);
        int rule = text.IndexOf(".search-bar .mud-input-control .mud-input.mud-input-outlined,", block, StringComparison.Ordinal);
        Assert.True(rule > block, "Expected a .search-bar fill rule in the Global search block.");
        string searchRule = text[rule..(text.IndexOf('}', rule) + 1)];

        // contains-ok: source-fact test, no CSS parser - the rules' own text is what's pinned.
        Assert.Contains("background: var(--mud-palette-surface);", composerRule, StringComparison.Ordinal);
        Assert.Contains("background: var(--mud-palette-surface);", searchRule, StringComparison.Ordinal);
        Assert.Contains(":hover", searchRule, StringComparison.Ordinal);
        Assert.Contains(".mud-focused", searchRule, StringComparison.Ordinal);
    }

    /// <summary>Reads <c>app.css</c> as the source fact every test in this class pins.</summary>
    /// <returns>The full text of <c>app.css</c>.</returns>
    private static string ReadAppCss() => File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.css"));

    /// <summary>Extracts the <c>.search-dropdown</c> rule from <c>app.css</c>, from its selector to its closing brace.</summary>
    /// <returns>The rule's text, selector included.</returns>
    private static string DropdownRule()
    {
        string text = ReadAppCss();

        // The selector with its brace: the zoom comment at the top of the file mentions the bare name.
        int classIndex = text.IndexOf(".search-dropdown {", StringComparison.Ordinal);
        Assert.True(classIndex >= 0, "Expected a .search-dropdown rule in app.css.");
        int braceEnd = text.IndexOf('}', classIndex);
        Assert.True(braceEnd > classIndex, "Expected .search-dropdown's rule to be closed.");
        return text[classIndex..(braceEnd + 1)];
    }
}
