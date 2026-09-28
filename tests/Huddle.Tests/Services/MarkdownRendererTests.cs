using System.Collections.Frozen;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Library;

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

    // Library notes

    private static readonly LibraryRoot NoteRoot = new("teams", "Teams", "E:\\Teams", LibraryRootKind.Teams);
    private static readonly LibraryPath NotePath = new(
        MarkdownRendererTests.NoteRoot, "Marketing/Launch Q4/a.md", "E:\\Teams\\Marketing\\Launch Q4\\a.md", LibraryNodeRole.File);

    /// <summary>A wikilink the resolver resolves becomes a <c>library-ref</c> link, with the target as its text.</summary>
    [Fact]
    public void Note_WikiLink_Resolved_IsLibraryLink()
    {
        FakeLibraryNoteResolver library = new(resolveWikiLink: (_, link) =>
            new LibraryReference("teams", "Marketing/plan.md", Exists: true));

        var html = MarkdownRenderer.ToHtml("See [[plan]] for details.", tasks: null, library, from: MarkdownRendererTests.NotePath);
        IElement anchor = MarkdownRendererTests.FindAnchor(html, "plan");

        Assert.Equal("?library=teams/Marketing/plan.md", anchor.GetAttribute("href"));
        Assert.Contains("library-ref", anchor.ClassList, StringComparer.Ordinal);
    }

    /// <summary>An alias replaces the target as the link's text.</summary>
    [Fact]
    public void Note_WikiLinkWithAlias_TextIsAlias()
    {
        FakeLibraryNoteResolver library = new(resolveWikiLink: (_, link) =>
            new LibraryReference("teams", "Marketing/plan.md", Exists: true));

        var html = MarkdownRenderer.ToHtml("See [[plan|the plan]].", tasks: null, library, from: MarkdownRendererTests.NotePath);
        IElement anchor = MarkdownRendererTests.FindAnchor(html, "the plan");

        Assert.Equal("?library=teams/Marketing/plan.md", anchor.GetAttribute("href"));
    }

    /// <summary>A heading in the wikilink target is not carried into the href, since v1 links to the file only.</summary>
    [Fact]
    public void Note_WikiLinkWithHeading_LinksToFile()
    {
        FakeLibraryNoteResolver library = new(resolveWikiLink: (_, link) =>
            new LibraryReference("teams", "Marketing/plan.md", Exists: true));

        var html = MarkdownRenderer.ToHtml("See [[plan#Dates]].", tasks: null, library, from: MarkdownRendererTests.NotePath);
        IElement anchor = MarkdownRendererTests.FindAnchor(html, "plan#Dates");

        Assert.Equal("?library=teams/Marketing/plan.md", anchor.GetAttribute("href"));
    }

    /// <summary>An unresolved wikilink still links (offering "Create note"), dimmed via the missing class, with an exact title.</summary>
    [Fact]
    public void Note_WikiLink_Unresolved_HasMissingClassAndTitle()
    {
        FakeLibraryNoteResolver library = new(resolveWikiLink: (_, link) =>
            new LibraryReference("teams", link.Target, Exists: false));

        var html = MarkdownRenderer.ToHtml("See [[plan]].", tasks: null, library, from: MarkdownRendererTests.NotePath);
        IElement anchor = MarkdownRendererTests.FindAnchor(html, "plan");

        Assert.Contains("library-ref-missing", anchor.ClassList, StringComparer.Ordinal);
        Assert.Equal("No note named \"plan\". Click to create it.", anchor.GetAttribute("title"));
    }

    /// <summary>Several notes tying for a wikilink target show an ambiguity title instead of the missing one.</summary>
    [Fact]
    public void Note_WikiLink_Ambiguous_HasTitle()
    {
        FakeLibraryNoteResolver library = new(resolveWikiLink: (_, link) =>
            new LibraryReference("teams", "Marketing/plan.md", Exists: true, IsAmbiguous: true));

        var html = MarkdownRenderer.ToHtml("See [[plan]].", tasks: null, library, from: MarkdownRendererTests.NotePath);
        IElement anchor = MarkdownRendererTests.FindAnchor(html, "plan");

        Assert.Equal("Several notes match \"plan\"; showing the closest.", anchor.GetAttribute("title"));
    }

    /// <summary>An embed token is recognised as a link for rewriting purposes, but never rendered as an embed (Spec §6.5).</summary>
    [Fact]
    public void Note_EmbedToken_RendersAsLink()
    {
        FakeLibraryNoteResolver library = new(resolveWikiLink: (_, link) =>
            new LibraryReference("teams", "Marketing/plan.md", Exists: true));

        var html = MarkdownRenderer.ToHtml("See ![[plan]].", tasks: null, library, from: MarkdownRendererTests.NotePath);
        var document = new HtmlParser().ParseDocument(html);

        Assert.Null(document.QuerySelector("img"));
        IElement anchor = MarkdownRendererTests.FindAnchor(html, "plan");
        Assert.Contains("library-ref", anchor.ClassList, StringComparer.Ordinal);
    }

    /// <summary>A relative Markdown link is resolved from the note's own location.</summary>
    [Fact]
    public void Note_RelativeMarkdownLink_Resolved()
    {
        FakeLibraryNoteResolver library = new(resolveRelative: (from, url) =>
            string.Equals(url, "../plan.md", StringComparison.Ordinal)
                ? new LibraryReference("teams", "Marketing/plan.md", Exists: true)
                : null);

        var html = MarkdownRenderer.ToHtml("[x](../plan.md)", tasks: null, library, from: MarkdownRendererTests.NotePath);
        var document = new HtmlParser().ParseDocument(html);
        var anchor = document.QuerySelector("a");

        Assert.NotNull(anchor);
        Assert.Equal("?library=teams/Marketing/plan.md", anchor.GetAttribute("href"));
    }

    /// <summary>A relative Markdown link carrying a heading fragment still resolves: the resolver strips the fragment (9.3).</summary>
    [Fact]
    public void Note_RelativeLinkWithHeadingFragment_ResolvesStrippingFragment()
    {
        FakeLibraryNoteResolver library = new(resolveRelative: (from, url) =>
            string.Equals(url, "../plan.md#Dates", StringComparison.Ordinal)
                ? new LibraryReference("teams", "Marketing/plan.md", Exists: true)
                : null);

        var html = MarkdownRenderer.ToHtml("[x](../plan.md#Dates)", tasks: null, library, from: MarkdownRendererTests.NotePath);
        var document = new HtmlParser().ParseDocument(html);
        var anchor = document.QuerySelector("a");

        Assert.NotNull(anchor);
        Assert.Equal("?library=teams/Marketing/plan.md", anchor.GetAttribute("href"));
    }

    /// <summary>An absolute https link is not a Library link and keeps its own href unchanged.</summary>
    [Fact]
    public void Note_AbsoluteHttpsLink_KeepsHref()
    {
        FakeLibraryNoteResolver library = new();

        var html = MarkdownRenderer.ToHtml("[x](https://e)", tasks: null, library, from: MarkdownRendererTests.NotePath);
        var document = new HtmlParser().ParseDocument(html);
        var anchor = document.QuerySelector("a");

        Assert.NotNull(anchor);
        Assert.Equal("https://e", anchor.GetAttribute("href"));
    }

    /// <summary>An image link stays "#" until the /library-files/ route exists (corrections-B5 item 21).</summary>
    [Fact]
    public void Note_ImageLink_HrefStaysHash()
    {
        FakeLibraryNoteResolver library = new(resolveRelative: (from, url) =>
            new LibraryReference("teams", "Marketing/img/x.png", Exists: true));

        var html = MarkdownRenderer.ToHtml("![](img/x.png)", tasks: null, library, from: MarkdownRendererTests.NotePath);
        var document = new HtmlParser().ParseDocument(html);
        var image = document.QuerySelector("img");

        Assert.NotNull(image);
        Assert.Equal("#", image.GetAttribute("src"));
    }

    /// <summary>A wikilink inside a code span is left untouched, never linked.</summary>
    [Fact]
    public void Note_WikiLinkInCode_Untouched()
    {
        FakeLibraryNoteResolver library = new(resolveWikiLink: (_, link) =>
            new LibraryReference("teams", "Marketing/plan.md", Exists: true));

        var html = MarkdownRenderer.ToHtml("Run `[[plan]]` locally.", tasks: null, library, from: MarkdownRendererTests.NotePath);
        var document = new HtmlParser().ParseDocument(html);
        var code = document.QuerySelector("code");

        Assert.DoesNotContain("<a", html, StringComparison.Ordinal);
        Assert.NotNull(code);
        Assert.Equal("[[plan]]", code.TextContent);
    }

    /// <summary>An Obsidian callout and highlight markup are not given special rendering: the callout stays a
    /// plain blockquote holding the literal marker text, and "==x==" still renders "&lt;mark&gt;" exactly as it
    /// does today, since <c>UseEmphasisExtras</c> already enables it and the pipeline builder is unchanged
    /// (corrections-B5 item 20 overrides the plan's "renders literally").</summary>
    [Fact]
    public void Note_CalloutAndHighlight_RenderAsPlainMarkdown()
    {
        FakeLibraryNoteResolver library = new();

        var calloutHtml = MarkdownRenderer.ToHtml("> [!note]", tasks: null, library, from: MarkdownRendererTests.NotePath);
        var calloutDocument = new HtmlParser().ParseDocument(calloutHtml);
        var blockquote = calloutDocument.QuerySelector("blockquote");

        Assert.NotNull(blockquote);
        Assert.Equal("[!note]", blockquote.TextContent.Trim());

        var highlightHtml = MarkdownRenderer.ToHtml("==x==", tasks: null, library, from: MarkdownRendererTests.NotePath);
        var highlightDocument = new HtmlParser().ParseDocument(highlightHtml);
        var mark = highlightDocument.QuerySelector("mark");

        Assert.NotNull(mark);
        Assert.Equal("x", mark.TextContent);
    }

    /// <summary>A wikilink written inside emphasis still links, and the italics around it render exactly as
    /// without a Library resolver.</summary>
    [Fact]
    public void Note_WikiLinkInsideEmphasis_LinksAndKeepsEmphasis()
    {
        const string Markdown = "*see [[plan]]*";
        FakeLibraryNoteResolver library = new(resolveWikiLink: (_, link) =>
            new LibraryReference("teams", "Marketing/plan.md", Exists: true));

        var html = MarkdownRenderer.ToHtml(Markdown, tasks: null, library, from: MarkdownRendererTests.NotePath);
        var document = new HtmlParser().ParseDocument(html);
        var emphasis = document.QuerySelector("em");
        var withoutLibraryDocument = new HtmlParser().ParseDocument(MarkdownRenderer.ToHtml(Markdown, tasks: null, library: null, from: null));

        Assert.NotNull(emphasis);
        var anchor = emphasis.QuerySelector("a.library-ref");
        Assert.NotNull(anchor);
        string? expected = withoutLibraryDocument.Body?.TextContent.Replace("[[plan]]", "plan", StringComparison.Ordinal);
        Assert.Equal(expected, document.Body?.TextContent);
    }

    /// <summary>Text escaping the wikilink markers next to it renders exactly as it does with no Library resolver,
    /// once the wikilink token itself is swapped for its link text - the surrounding escaped asterisks are
    /// untouched, so the body reads literally "*plan*", not italicised.</summary>
    [Fact]
    public void Note_WikiLinkNextToEscapedAsterisks_RendersLikeWithoutLibrary()
    {
        const string Markdown = @"\*[[plan]]\*";
        FakeLibraryNoteResolver library = new(resolveWikiLink: (_, link) =>
            new LibraryReference("teams", "Marketing/plan.md", Exists: true));

        var html = MarkdownRenderer.ToHtml(Markdown, tasks: null, library, from: MarkdownRendererTests.NotePath);
        var document = new HtmlParser().ParseDocument(html);
        var withoutLibraryDocument = new HtmlParser().ParseDocument(MarkdownRenderer.ToHtml(Markdown, tasks: null, library: null, from: null));
        var anchor = document.QuerySelector("a.library-ref");

        string? expected = withoutLibraryDocument.Body?.TextContent.Replace("[[plan]]", "plan", StringComparison.Ordinal);
        Assert.Equal(expected, document.Body?.TextContent);
        Assert.NotNull(anchor);
        Assert.Equal("plan", anchor.TextContent);
        Assert.Equal("*plan*", document.Body?.TextContent.Trim());
    }

    /// <summary>An escaped opening bracket breaks the wikilink token, so it is never linked and renders exactly
    /// as it does with no Library resolver at all.</summary>
    [Fact]
    public void Note_EscapedWikiLinkOpener_NotLinked()
    {
        const string Markdown = @"\[[plan]]";
        FakeLibraryNoteResolver library = new(resolveWikiLink: (_, link) =>
            new LibraryReference("teams", "Marketing/plan.md", Exists: true));

        var html = MarkdownRenderer.ToHtml(Markdown, tasks: null, library, from: MarkdownRendererTests.NotePath);
        var document = new HtmlParser().ParseDocument(html);
        var withoutLibraryDocument = new HtmlParser().ParseDocument(MarkdownRenderer.ToHtml(Markdown, tasks: null, library: null, from: null));

        Assert.DoesNotContain("<a", html, StringComparison.Ordinal);
        Assert.Equal(withoutLibraryDocument.Body?.TextContent, document.Body?.TextContent);
    }

    /// <summary>Two wikilinks in one paragraph, plus a Task id beside them, are all linked.</summary>
    [Fact]
    public void Note_TwoWikiLinksAndTaskIdInOneParagraph_AllLinked()
    {
        TaskId id = MarkdownRendererTests.MakeId("PLAT-0042");
        FakeTaskReferenceResolver tasks = new(new TaskReference(id, "Ship the thing", Closed: false));
        FakeLibraryNoteResolver library = new(resolveWikiLink: (_, link) =>
            new LibraryReference("teams", string.Concat("Marketing/", link.Target, ".md"), Exists: true));

        var html = MarkdownRenderer.ToHtml(
            "See [[plan]] and [[budget]], tracked as PLAT-0042.", tasks: tasks, library, from: MarkdownRendererTests.NotePath);
        var document = new HtmlParser().ParseDocument(html);
        var anchors = document.QuerySelectorAll("a");

        List<(string Text, string Class)> actual = [.. anchors.Select(a => (a.TextContent, a.GetAttribute("class") ?? string.Empty))];
        List<(string Text, string Class)> expected =
        [
            ("plan", "library-ref"),
            ("budget", "library-ref"),
            ("PLAT-0042", "task-ref"),
        ];
        Assert.Equal(expected, actual);
    }

    /// <summary>The <c>?library=</c> href URL-encodes a relative path containing spaces.</summary>
    [Fact]
    public void Note_LibraryHref_IsUrlEncoded()
    {
        FakeLibraryNoteResolver library = new(resolveWikiLink: (_, link) =>
            new LibraryReference("teams", "Marketing/Launch Q4/plan.md", Exists: true));

        var html = MarkdownRenderer.ToHtml("See [[plan]].", tasks: null, library, from: MarkdownRendererTests.NotePath);
        IElement anchor = MarkdownRendererTests.FindAnchor(html, "plan");

        Assert.Equal("?library=teams/Marketing/Launch%20Q4/plan.md", anchor.GetAttribute("href"));
    }

    /// <summary>With <c>from</c> null, no wikilink pass runs at all: the existing chat behaviour, where a
    /// wikilink is just plain text.</summary>
    [Fact]
    public void Note_FromNull_NoWikiLinkLinks()
    {
        FakeLibraryNoteResolver library = new(resolveWikiLink: (_, link) =>
            new LibraryReference("teams", "Marketing/plan.md", Exists: true));

        var html = MarkdownRenderer.ToHtml("See [[plan]].", tasks: null, library, from: null);
        var document = new HtmlParser().ParseDocument(html);

        Assert.DoesNotContain("<a", html, StringComparison.Ordinal);
        Assert.Equal("See [[plan]].", document.Body?.TextContent.Trim());
    }
}
