using System.Text.RegularExpressions;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Services;
using Agency.Huddle.Tests.Library;

namespace Agency.Huddle.Tests.Services;

/// <summary>Heading ids are given on the note render path only, never in chat (Spec: Outline, Missing today).</summary>
public sealed partial class MarkdownRendererHeadingIdTests
{
    private static readonly LibraryRoot NoteRoot = new("teams", "Teams", "E:\\Teams", LibraryRootKind.Teams);
    private static readonly LibraryPath NotePath = new(
        MarkdownRendererHeadingIdTests.NoteRoot, "Marketing/a.md", "E:\\Teams\\Marketing\\a.md", LibraryNodeRole.File);

    [GeneratedRegex("id=\"([^\"]*)\"", RegexOptions.CultureInvariant)]
    private static partial Regex IdAttribute();

    private static string NoteHtml(string markdown) =>
        MarkdownRenderer.ToHtml(markdown, null, new FakeLibraryNoteResolver(), MarkdownRendererHeadingIdTests.NotePath);

    /// <summary>A heading on the note path carries the <c>doc-h-</c> slug as its id.</summary>
    [Fact]
    public void NoteRender_Heading_HasDocHId()
    {
        string html = MarkdownRendererHeadingIdTests.NoteHtml("## Risks\n\ntext");

        Assert.Contains("<h2 id=\"doc-h-risks\">", html, StringComparison.Ordinal); // contains-ok: one heading tag in a fragment; the id attribute is the unit under test.
    }

    /// <summary>A repeated heading text gets <c>-2</c>, <c>-3</c> on its later occurrences.</summary>
    [Fact]
    public void NoteRender_RepeatedHeadings_AreNumbered()
    {
        string html = MarkdownRendererHeadingIdTests.NoteHtml("## Risks\n\n## Risks\n\n## Risks");

        Assert.Contains("<h2 id=\"doc-h-risks\">", html, StringComparison.Ordinal); // contains-ok: one heading tag in a fragment; the id attribute is the unit under test.
        Assert.Contains("<h2 id=\"doc-h-risks-2\">", html, StringComparison.Ordinal); // contains-ok: one heading tag in a fragment; the id attribute is the unit under test.
        Assert.Contains("<h2 id=\"doc-h-risks-3\">", html, StringComparison.Ordinal); // contains-ok: one heading tag in a fragment; the id attribute is the unit under test.
    }

    /// <summary>The chat overloads emit no heading ids, so Messages never repeat them.</summary>
    [Fact]
    public void ChatRender_Heading_HasNoId()
    {
        string oneArgument = MarkdownRenderer.ToHtml("## Risks");
        string twoArguments = MarkdownRenderer.ToHtml("## Risks", null);

        Assert.Contains("<h2>Risks</h2>", oneArgument, StringComparison.Ordinal); // contains-ok: one heading tag in a fragment; the id attribute is the unit under test.
        Assert.Contains("<h2>Risks</h2>", twoArguments, StringComparison.Ordinal); // contains-ok: one heading tag in a fragment; the id attribute is the unit under test.
        Assert.DoesNotContain("id=", oneArgument, StringComparison.Ordinal);
        Assert.DoesNotContain("id=", twoArguments, StringComparison.Ordinal);
    }

    /// <summary>Every rendered id equals the outline's id for the same heading, in order.</summary>
    [Fact]
    public void NoteRender_IdMatchesOutline()
    {
        const string text = "# Title\n\n## Risks\n\n### Risks\n\n## Plan & Scope\n\n## Risks\n";

        string html = MarkdownRendererHeadingIdTests.NoteHtml(text);

        List<string> rendered = [.. MarkdownRendererHeadingIdTests.IdAttribute().Matches(html).Select(static m => m.Groups[1].Value)];
        List<string> expected = [.. MarkdownOutline.Headings(text).Select(static h => h.Id)];
        Assert.NotEmpty(expected);
        Assert.Equal(expected, rendered);
    }

    /// <summary>A heading that tries to break out of markup yields a slug-only id and no raw script tag.</summary>
    [Fact]
    public void NoteRender_HostileHeading_IdIsSlugOnly()
    {
        string html = MarkdownRendererHeadingIdTests.NoteHtml("# \"><script>");

        Assert.Contains("id=\"doc-h-script\"", html, StringComparison.Ordinal); // contains-ok: one heading tag in a fragment; the id attribute is the unit under test.
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
    }
}
