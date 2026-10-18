namespace Agency.Huddle.Tests.Library;

/// <summary>
/// Tests for <see cref="Agency.Huddle.App.Library.MarkdownOutline"/>: the heading slug rule, the top-level heading list of a
/// Markdown text, the frontmatter split, and the 1-based line of each heading in the whole text
/// (Spec <i>Outline</i>, plan Task 6.1).
/// </summary>
public sealed class MarkdownOutlineTests
{
    /// <summary>A heading's slug is <c>doc-h-</c> plus a lowercase hyphenated form of its text.</summary>
    /// <param name="text">The heading's plain text.</param>
    /// <param name="expected">The id the slug rule must produce.</param>
    [Theory]
    [InlineData("Plan", "doc-h-plan")]
    [InlineData("Plan: Risks & Costs", "doc-h-plan-risks-costs")]
    [InlineData("  Two   spaces ", "doc-h-two-spaces")]
    [InlineData("Ünïcode Wörds", "doc-h-ünïcode-wörds")]
    [InlineData("--dashes--", "doc-h-dashes")]
    [InlineData("!!!", "doc-h-section")]
    [InlineData("`code` heading", "doc-h-code-heading")]
    public void Slug_IsLowercaseHyphenated(string text, string expected)
    {
        string actual = Agency.Huddle.App.Library.MarkdownOutline.Slug(text);

        Assert.Equal(expected, actual);
    }

    /// <summary>ATX and setext headings are listed in document order with their level and text.</summary>
    [Fact]
    public void Headings_AtxAndSetext_InDocumentOrder()
    {
        const string markdown = "# A\n\nB\n---\n\n### C\n";
        Agency.Huddle.App.Library.OutlineHeading[] expected =
        [
            new Agency.Huddle.App.Library.OutlineHeading(1, "A", "doc-h-a", 1),
            new Agency.Huddle.App.Library.OutlineHeading(2, "B", "doc-h-b", 3),
            new Agency.Huddle.App.Library.OutlineHeading(3, "C", "doc-h-c", 6),
        ];

        IReadOnlyList<Agency.Huddle.App.Library.OutlineHeading> actual = Agency.Huddle.App.Library.MarkdownOutline.Headings(markdown);

        Assert.Equal<Agency.Huddle.App.Library.OutlineHeading>(expected, actual);
    }

    /// <summary>A repeated heading text gets the ids <c>-2</c>, <c>-3</c> for its later occurrences.</summary>
    [Fact]
    public void Headings_Repeated_GetNumberedIds()
    {
        const string markdown = "# A\n\n# A\n\n# A\n";

        IReadOnlyList<Agency.Huddle.App.Library.OutlineHeading> actual = Agency.Huddle.App.Library.MarkdownOutline.Headings(markdown);

        Assert.Equal(["doc-h-a", "doc-h-a-2", "doc-h-a-3"], actual.Select(static heading => heading.Id));
    }

    /// <summary>The line of a heading is 1-based and counts every line of the whole text.</summary>
    [Fact]
    public void Headings_LineIsOneBasedInTheWholeText()
    {
        const string markdown = "Intro\n\n## Second\n";

        IReadOnlyList<Agency.Huddle.App.Library.OutlineHeading> actual = Agency.Huddle.App.Library.MarkdownOutline.Headings(markdown);

        Agency.Huddle.App.Library.OutlineHeading heading = Assert.Single(actual);
        Assert.Equal(3, heading.Line);
    }

    /// <summary>Frontmatter is not part of the outline, and its lines are counted in the heading's line.</summary>
    [Fact]
    public void Headings_Frontmatter_IsSkippedAndLinesCountIt()
    {
        const string markdown = "---\nname: x\n---\n# A";

        IReadOnlyList<Agency.Huddle.App.Library.OutlineHeading> actual = Agency.Huddle.App.Library.MarkdownOutline.Headings(markdown);

        Agency.Huddle.App.Library.OutlineHeading heading = Assert.Single(actual);
        Assert.Equal(4, heading.Line);
        Assert.Equal("A", heading.Text);
    }

    /// <summary>A line that looks like a heading inside a fenced code block is not a heading.</summary>
    [Fact]
    public void Headings_InsideCodeFence_AreIgnored()
    {
        const string markdown = "```\n# not\n```\n\n# real\n";

        IReadOnlyList<Agency.Huddle.App.Library.OutlineHeading> actual = Agency.Huddle.App.Library.MarkdownOutline.Headings(markdown);

        Agency.Huddle.App.Library.OutlineHeading heading = Assert.Single(actual);
        Assert.Equal("real", heading.Text);
        Assert.Equal(5, heading.Line);
    }

    /// <summary>Only top-level headings are listed; a heading nested in a blockquote is left out.</summary>
    [Fact]
    public void Headings_NestedInBlockquote_AreIgnored()
    {
        const string markdown = "> # quoted\n\n# top\n";

        IReadOnlyList<Agency.Huddle.App.Library.OutlineHeading> actual = Agency.Huddle.App.Library.MarkdownOutline.Headings(markdown);

        Agency.Huddle.App.Library.OutlineHeading heading = Assert.Single(actual);
        Assert.Equal("top", heading.Text);
    }

    /// <summary>A text with no headings gives an empty outline.</summary>
    [Fact]
    public void Headings_None_IsEmpty()
    {
        IReadOnlyList<Agency.Huddle.App.Library.OutlineHeading> actual = Agency.Huddle.App.Library.MarkdownOutline.Headings("Just text.\n");

        Assert.Empty(actual);
    }
}
