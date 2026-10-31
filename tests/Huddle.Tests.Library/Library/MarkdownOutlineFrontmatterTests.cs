using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>Pins that the Outline splits frontmatter exactly where the rendered document does (<see cref="NoteFrontmatter"/>), so its ids and lines match the page.</summary>
public sealed class MarkdownOutlineFrontmatterTests
{
    /// <summary>A block closed with <c>...</c> is frontmatter: the heading after it is listed, at its true line, and a later <c>---</c> rule does not close anything.</summary>
    [Fact]
    public void Headings_DotsCloser_ListsTheHeadingAfterTheBlock()
    {
        const string Markdown = "---\ntitle: x\n...\n# Intro\n\ntext\n\n---\n# Next\n";

        IReadOnlyList<OutlineHeading> headings = MarkdownOutline.Headings(Markdown);

        Assert.Equal(["Intro:4", "Next:9"], headings.Select(static h => $"{h.Text}:{h.Line}"));
    }

    /// <summary>A block that never closes is body: its first line is not frontmatter, so headings keep their own lines.</summary>
    [Fact]
    public void Headings_UnclosedBlock_IsBody()
    {
        IReadOnlyList<OutlineHeading> headings = MarkdownOutline.Headings("---\n# A\n");

        Assert.Equal(["A:2"], headings.Select(static h => $"{h.Text}:{h.Line}"));
    }
}
