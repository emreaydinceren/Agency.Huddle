using System.Collections.Frozen;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.Tests.Services;

public sealed class MarkdownRendererTests
{
    [Fact]
    public void ToHtml_RendersBold()
    {
        var html = MarkdownRenderer.ToHtml("**x**");

        Assert.Contains("<strong>x</strong>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ToHtml_EscapesRawHtml()
    {
        var html = MarkdownRenderer.ToHtml("<script>alert(1)</script>");

        Assert.DoesNotContain("<script", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ToHtml_RewritesUnsafeLinkScheme()
    {
        var html = MarkdownRenderer.ToHtml("[x](javascript:alert(1))");

        Assert.Contains("href=\"#\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ToHtml_KeepsHttpsLink()
    {
        var html = MarkdownRenderer.ToHtml("[x](https://a.b)");

        Assert.Contains("href=\"https://a.b\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ToHtml_RendersFencedCode()
    {
        var html = MarkdownRenderer.ToHtml("```cs\nint x;\n```");

        Assert.Contains("<pre><code", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ToHtml_PreservesLineBreaksAsParagraphs()
    {
        var html = MarkdownRenderer.ToHtml("first paragraph\n\nsecond paragraph");

        Assert.Equal(2, html.Split("<p>").Length - 1);
    }

    [Fact]
    public void ToHtml_DoesNotEmitEventHandlerAttributes()
    {
        var headingHtml = MarkdownRenderer.ToHtml("# Hi {onclick=\"alert(1)\"}");
        var paragraphHtml = MarkdownRenderer.ToHtml("Hi {onmouseover=\"alert(1)\"}");

        Assert.DoesNotContain(" onclick=\"", headingHtml, StringComparison.Ordinal);
        Assert.DoesNotContain(" onmouseover=\"", paragraphHtml, StringComparison.Ordinal);
    }

    [Fact]
    public void ToHtml_DoesNotEmitIframes()
    {
        var html = MarkdownRenderer.ToHtml("https://www.youtube.com/watch?v=x");

        Assert.DoesNotContain("<iframe", html, StringComparison.Ordinal);
    }

    /// <summary>An id the resolver knows becomes an anchor, and its attributes and text are asserted separately through AngleSharp rather than by matching a literal HTML string, since Markdig's own attribute order is not part of the contract under test.</summary>
    [Fact]
    public void ToHtml_ResolverKnowsTheId_BecomesATaskRefLink()
    {
        TaskId id = MarkdownRendererTests.MakeId("PLAT-0042");
        FakeTaskReferenceResolver resolver = new(new TaskReference(id, "Ship the thing", Closed: false));

        var html = MarkdownRenderer.ToHtml("See PLAT-0042 for details.", resolver);
        IElement anchor = MarkdownRendererTests.FindAnchor(html, "PLAT-0042");

        Assert.Equal("/tasks/item/PLAT-0042", anchor.GetAttribute("href"));
        Assert.Equal("Ship the thing", anchor.GetAttribute("title"));
        Assert.Contains("task-ref", anchor.ClassList, StringComparer.Ordinal);
        Assert.Equal("PLAT-0042", anchor.TextContent);
    }

    /// <summary>A Closed Task's link additionally carries the struck-through class.</summary>
    [Fact]
    public void ToHtml_ClosedTask_AddsTaskRefClosedClass()
    {
        TaskId id = MarkdownRendererTests.MakeId("PLAT-0042");
        FakeTaskReferenceResolver resolver = new(new TaskReference(id, "Ship the thing", Closed: true));

        var html = MarkdownRenderer.ToHtml("See PLAT-0042 for details.", resolver);
        IElement anchor = MarkdownRendererTests.FindAnchor(html, "PLAT-0042");

        Assert.Contains("task-ref", anchor.ClassList, StringComparer.Ordinal);
        Assert.Contains("task-ref-closed", anchor.ClassList, StringComparer.Ordinal);
    }

    /// <summary>An id-shaped token the resolver does not know stays plain text.</summary>
    [Fact]
    public void ToHtml_UnknownId_StaysPlain()
    {
        FakeTaskReferenceResolver resolver = new();

        var html = MarkdownRenderer.ToHtml("See PLAT-0999 for details.", resolver);
        var document = new HtmlParser().ParseDocument(html);

        Assert.DoesNotContain("<a", html, StringComparison.Ordinal);
        Assert.Equal("See PLAT-0999 for details.", document.Body?.TextContent.Trim());
    }

    /// <summary>"UTF-8" has the same shape as a Task id but never resolves, so it stays plain even though the resolver is present.</summary>
    [Fact]
    public void ToHtml_UtfEightLookalike_StaysPlain()
    {
        FakeTaskReferenceResolver resolver = new();

        var html = MarkdownRenderer.ToHtml("Encode as UTF-8.", resolver);

        Assert.DoesNotContain("<a", html, StringComparison.Ordinal);
    }

    /// <summary>The recognition regex requires an upper-case prefix in chat text (D-32), so a lower-case id never links even when the resolver would otherwise resolve it.</summary>
    [Fact]
    public void ToHtml_LowerCasePrefix_StaysPlain()
    {
        TaskId id = MarkdownRendererTests.MakeId("PLAT-0042");
        FakeTaskReferenceResolver resolver = new(new TaskReference(id, "Ship the thing", Closed: false));

        var html = MarkdownRenderer.ToHtml("See plat-0042 for details.", resolver);
        var document = new HtmlParser().ParseDocument(html);

        Assert.DoesNotContain("<a", html, StringComparison.Ordinal);
        Assert.Equal("See plat-0042 for details.", document.Body?.TextContent.Trim());
    }

    /// <summary>An id inside an inline code span is never a candidate literal, so it is never linked.</summary>
    [Fact]
    public void ToHtml_IdInsideCodeSpan_StaysPlain()
    {
        TaskId id = MarkdownRendererTests.MakeId("PLAT-0042");
        FakeTaskReferenceResolver resolver = new(new TaskReference(id, "Ship the thing", Closed: false));

        var html = MarkdownRenderer.ToHtml("Run `PLAT-0042` locally.", resolver);
        var document = new HtmlParser().ParseDocument(html);
        var code = document.QuerySelector("code");

        Assert.DoesNotContain("<a", html, StringComparison.Ordinal);
        Assert.NotNull(code);
        Assert.Equal("PLAT-0042", code.TextContent);
    }

    /// <summary>An id inside a fenced code block is never a candidate literal, so it is never linked.</summary>
    [Fact]
    public void ToHtml_IdInsideFencedCodeBlock_StaysPlain()
    {
        TaskId id = MarkdownRendererTests.MakeId("PLAT-0042");
        FakeTaskReferenceResolver resolver = new(new TaskReference(id, "Ship the thing", Closed: false));

        var html = MarkdownRenderer.ToHtml("```\nPLAT-0042\n```", resolver);

        Assert.DoesNotContain("<a", html, StringComparison.Ordinal);
    }

    /// <summary>An id that is already the text of an existing link is skipped, never rewrapped in a second anchor.</summary>
    [Fact]
    public void ToHtml_IdInsideExistingLink_StaysUnrewritten()
    {
        TaskId id = MarkdownRendererTests.MakeId("PLAT-0042");
        FakeTaskReferenceResolver resolver = new(new TaskReference(id, "Ship the thing", Closed: false));

        var html = MarkdownRenderer.ToHtml("[PLAT-0042](https://a.b)", resolver);
        var document = new HtmlParser().ParseDocument(html);
        var anchors = document.QuerySelectorAll("a");

        Assert.Single(anchors);
        Assert.DoesNotContain("task-ref", anchors[0].ClassList, StringComparer.Ordinal);
        Assert.Equal("https://a.b", anchors[0].GetAttribute("href"));
    }

    /// <summary>Explicit relative task links pass the narrowed <c>IsSafe</c> unchanged, independent of any resolver.</summary>
    [Fact]
    public void ToHtml_ExplicitTaskItemLink_IsSafe()
    {
        var html = MarkdownRenderer.ToHtml("[x](/tasks/item/PLAT-0042)");
        var document = new HtmlParser().ParseDocument(html);
        var anchor = document.QuerySelector("a");

        Assert.NotNull(anchor);
        Assert.Equal("/tasks/item/PLAT-0042", anchor.GetAttribute("href"));
    }

    /// <summary>A lower-case remainder does not equal the canonical id text exactly, so the link is rewritten to "#".</summary>
    [Fact]
    public void ToHtml_TaskItemLinkWithLowerCaseRemainder_IsRewrittenToHash()
    {
        var html = MarkdownRenderer.ToHtml("[x](/tasks/item/plat-0042)");
        var document = new HtmlParser().ParseDocument(html);
        var anchor = document.QuerySelector("a");

        Assert.NotNull(anchor);
        Assert.Equal("#", anchor.GetAttribute("href"));
    }

    /// <summary>Trailing path segments after the id are not a bare task reference, so the link is rewritten to "#".</summary>
    [Fact]
    public void ToHtml_TaskItemLinkWithTrailingSegment_IsRewrittenToHash()
    {
        var html = MarkdownRenderer.ToHtml("[x](/tasks/item/PLAT-0042/x)");
        var document = new HtmlParser().ParseDocument(html);
        var anchor = document.QuerySelector("a");

        Assert.NotNull(anchor);
        Assert.Equal("#", anchor.GetAttribute("href"));
    }

    /// <summary>A query string after the id is not a bare task reference, so the link is rewritten to "#".</summary>
    [Fact]
    public void ToHtml_TaskItemLinkWithQueryString_IsRewrittenToHash()
    {
        var html = MarkdownRenderer.ToHtml("[x](/tasks/item/PLAT-0042?x)");
        var document = new HtmlParser().ParseDocument(html);
        var anchor = document.QuerySelector("a");

        Assert.NotNull(anchor);
        Assert.Equal("#", anchor.GetAttribute("href"));
    }

    /// <summary>A traversal attempt dressed up as a task path is still rewritten to "#", the case the narrowed <c>IsSafe</c> exists to close.</summary>
    [Fact]
    public void ToHtml_PathTraversalDressedAsTaskLink_IsRewrittenToHash()
    {
        var html = MarkdownRenderer.ToHtml("[x](/tasks/item/../../evil)");
        var document = new HtmlParser().ParseDocument(html);
        var anchor = document.QuerySelector("a");

        Assert.NotNull(anchor);
        Assert.Equal("#", anchor.GetAttribute("href"));
    }

    /// <summary>An unrelated relative link is still rewritten to "#".</summary>
    [Fact]
    public void ToHtml_UnrelatedRelativeLink_IsRewrittenToHash()
    {
        var html = MarkdownRenderer.ToHtml("[x](/other)");
        var document = new HtmlParser().ParseDocument(html);
        var anchor = document.QuerySelector("a");

        Assert.NotNull(anchor);
        Assert.Equal("#", anchor.GetAttribute("href"));
    }

    /// <summary>A Task title carrying markup stays HTML-escaped in the rendered <c>title</c> attribute, never breaking out of it.</summary>
    [Fact]
    public void ToHtml_TitleContainingMarkup_StaysEscaped()
    {
        TaskId id = MarkdownRendererTests.MakeId("PLAT-0042");
        FakeTaskReferenceResolver resolver = new(new TaskReference(id, "\"><script>alert(1)</script>", Closed: false));

        var html = MarkdownRenderer.ToHtml("See PLAT-0042 for details.", resolver);

        Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
        IElement anchor = MarkdownRendererTests.FindAnchor(html, "PLAT-0042");
        Assert.Equal("\"><script>alert(1)</script>", anchor.GetAttribute("title"));
    }

    /// <summary>A candidate token immediately preceded by a letter is not a whole token, so it stays plain.</summary>
    [Fact]
    public void ToHtml_IdPrecededByALetter_StaysPlain()
    {
        TaskId id = MarkdownRendererTests.MakeId("PLAT-0042");
        FakeTaskReferenceResolver resolver = new(new TaskReference(id, "Ship the thing", Closed: false));

        var lowerPrefixed = MarkdownRenderer.ToHtml("wPLAT-0042 is not it.", resolver);
        var upperPrefixed = MarkdownRenderer.ToHtml("hPLAT-0042 is not it.", resolver);

        Assert.DoesNotContain("<a", lowerPrefixed, StringComparison.Ordinal);
        Assert.DoesNotContain("<a", upperPrefixed, StringComparison.Ordinal);
    }

    /// <summary>A candidate token immediately followed by an underscore is not a whole token, so it stays plain.</summary>
    [Fact]
    public void ToHtml_IdFollowedByAnUnderscore_StaysPlain()
    {
        TaskId id = MarkdownRendererTests.MakeId("PLAT-0042");
        FakeTaskReferenceResolver resolver = new(new TaskReference(id, "Ship the thing", Closed: false));

        var html = MarkdownRenderer.ToHtml("PLAT-0042_x is not it.", resolver);

        Assert.DoesNotContain("<a", html, StringComparison.Ordinal);
    }

    /// <summary>An id wrapped in emphasis markers still links, nested correctly inside the emphasis element.</summary>
    [Fact]
    public void ToHtml_IdInsideBoldEmphasis_LinksInsideTheStrongElement()
    {
        TaskId id = MarkdownRendererTests.MakeId("PLAT-0042");
        FakeTaskReferenceResolver resolver = new(new TaskReference(id, "Ship the thing", Closed: false));

        var html = MarkdownRenderer.ToHtml("**PLAT-0042**", resolver);
        var document = new HtmlParser().ParseDocument(html);
        var strong = document.QuerySelector("strong");

        Assert.NotNull(strong);
        var anchor = strong.QuerySelector("a.task-ref");
        Assert.NotNull(anchor);
        Assert.Equal("PLAT-0042", anchor.TextContent);
    }

    /// <summary>The single-argument overload keeps behaving exactly as before: no resolver means no linking, even for a token that would otherwise match.</summary>
    [Fact]
    public void ToHtml_NoResolverOverload_NeverLinksTaskIds()
    {
        var html = MarkdownRenderer.ToHtml("See PLAT-0042 for details.");
        var document = new HtmlParser().ParseDocument(html);

        Assert.DoesNotContain("<a", html, StringComparison.Ordinal);
        Assert.Equal("See PLAT-0042 for details.", document.Body?.TextContent.Trim());
    }

    /// <summary>With <c>Tasks.Enabled</c> false the renderer is called with a null resolver, so nothing links even for an id that a real resolver would resolve.</summary>
    [Fact]
    public void ToHtml_NullResolver_NeverLinksTaskIds()
    {
        var html = MarkdownRenderer.ToHtml("See PLAT-0042 for details.", null);
        var document = new HtmlParser().ParseDocument(html);

        Assert.DoesNotContain("<a", html, StringComparison.Ordinal);
        Assert.Equal("See PLAT-0042 for details.", document.Body?.TextContent.Trim());
    }

    /// <summary>Parses <paramref name="text"/> into a <see cref="TaskId"/>, failing the test loudly if the fixture text is not a valid id rather than silently falling back to <c>default</c>.</summary>
    private static TaskId MakeId(string text)
    {
        Assert.True(TaskId.TryParse(text, out TaskId id));
        return id;
    }

    /// <summary>Finds the single anchor in <paramref name="html"/> whose text is exactly <paramref name="text"/>.</summary>
    private static IElement FindAnchor(string html, string text)
    {
        var document = new HtmlParser().ParseDocument(html);
        var anchor = document.QuerySelectorAll("a").FirstOrDefault(a => string.Equals(a.TextContent, text, StringComparison.Ordinal));
        Assert.NotNull(anchor);
        return anchor;
    }

    /// <summary>A dictionary-backed fake of <see cref="ITaskReferenceResolver"/>, keyed by <see cref="TaskId"/>.</summary>
    private sealed class FakeTaskReferenceResolver : ITaskReferenceResolver
    {
        private readonly FrozenDictionary<TaskId, TaskReference> tasks;

        /// <summary>Creates a resolver that knows exactly the given <paramref name="tasks"/>.</summary>
        public FakeTaskReferenceResolver(params TaskReference[] tasks)
        {
            this.tasks = tasks.ToFrozenDictionary(t => t.Id);
        }

        /// <inheritdoc />
        public TaskReference? Resolve(TaskId id) => this.tasks.TryGetValue(id, out TaskReference? task) ? task : null;
    }
}
