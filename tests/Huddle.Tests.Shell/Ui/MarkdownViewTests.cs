using AngleSharp.Dom;
using Bunit;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Library;

namespace Agency.Huddle.Tests.Ui;

/// <summary>Renders <see cref="MarkdownView"/> on its own: the one div around rendered Markdown, with the classes, resolvers and modes its sites rely on.</summary>
public sealed class MarkdownViewTests
{
    private const string PlanPath = @"E:\Data\Teams\Marketing\plan.md";

    private const string EvidenceDescription = "Body.\n\n## Evidence\n- Checked E:\\Data\\Teams\\Marketing\\plan.md by hand\n\n## Notes\nSee E:\\Data\\Teams\\Marketing\\plan.md";

    private static readonly LibraryPath NotePath = new(
        new LibraryRoot("teams", "Teams", @"E:\Teams", LibraryRootKind.Teams), "Marketing/a.md", @"E:\Teams\Marketing\a.md", LibraryNodeRole.File);

    /// <summary>The Markdown is drawn as HTML inside one div: emphasis becomes an element, not the typed asterisks.</summary>
    [Fact]
    public async Task Markdown_IsDrawnAsHtmlInOneDiv()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<MarkdownView> cut = Render(ctx, "A **bold** word.", p => p.Add(v => v.Class, "message-body"));

        IElement div = Assert.Single(cut.FindAll("div"));
        Assert.Equal("bold", div.QuerySelector("strong")?.TextContent);
        Assert.DoesNotContain("**", div.TextContent, StringComparison.Ordinal);
    }

    /// <summary>The div carries the caller's classes first and <c>library-rendered</c> after them when asked, and no class attribute when it has neither.</summary>
    /// <param name="callerClass">The <c>Class</c> parameter.</param>
    /// <param name="libraryStyle">The <c>LibraryStyle</c> parameter.</param>
    /// <param name="expected">The class attribute the div must have, or null for none.</param>
    [Theory]
    [InlineData(null, false, null)]
    [InlineData("playbook-text", false, "playbook-text")]
    [InlineData(null, true, "library-rendered")]
    [InlineData("playbook-text", true, "playbook-text library-rendered")]
    public async Task Div_Class_IsTheCallersThenLibraryRendered(string? callerClass, bool libraryStyle, string? expected)
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<MarkdownView> cut = Render(ctx, "text", p => p.Add(v => v.Class, callerClass).Add(v => v.LibraryStyle, libraryStyle));

        Assert.Equal(expected, cut.Find("div").GetAttribute("class"));
    }

    /// <summary>Raw HTML typed in the Markdown is not markup: a script tag and an inline handler come out as text.</summary>
    [Fact]
    public async Task RawHtmlInTheMarkdown_IsNotDrawnAsMarkup()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<MarkdownView> cut = Render(ctx, "<script>alert(1)</script> and <b onclick=\"x()\">b</b>");

        Assert.Empty(cut.FindAll("script"));
        Assert.Empty(cut.FindAll("b"));
        Assert.Contains("alert(1)", cut.Find("div").TextContent, StringComparison.Ordinal);
    }

    /// <summary>A Task id the Tasks resolver knows becomes a link; with no resolver it stays text, as <c>ToHtml(markdown, null)</c> leaves it.</summary>
    [Fact]
    public async Task Tasks_ResolvedId_BecomesALink_AndStaysTextWithoutAResolver()
    {
        await using MudBunitContext ctx = new();
        Assert.True(TaskId.TryParse("PLAT-0042", out TaskId id));
        FakeTaskReferenceResolver resolver = new();
        resolver.Add(new TaskReference(id, "Ship the thing", Closed: false));

        IRenderedComponent<MarkdownView> linked = Render(ctx, "See PLAT-0042 today.", p => p.Add(v => v.Tasks, resolver));
        IRenderedComponent<MarkdownView> plain = Render(ctx, "See PLAT-0042 today.");

        Assert.Equal("PLAT-0042", linked.Find("a.task-ref").TextContent);
        Assert.Empty(plain.FindAll("a"));
    }

    /// <summary>With <c>TaskText</c>, a bare path in the Evidence section shows in full and elsewhere as its file name; without it, every path shows as its file name.</summary>
    [Fact]
    public async Task TaskText_ShowsTheFullPathOnlyInTheEvidenceSection()
    {
        await using MudBunitContext ctx = new();
        FakeLibraryNoteResolver library = new(resolvePath: candidate => string.Equals(candidate, PlanPath, StringComparison.Ordinal)
            ? new LibraryReference("teams", "Marketing/plan.md", Exists: true)
            : null);

        IRenderedComponent<MarkdownView> task = Render(ctx, EvidenceDescription, p => p.Add(v => v.Library, library).Add(v => v.TaskText, true));
        IRenderedComponent<MarkdownView> chat = Render(ctx, EvidenceDescription, p => p.Add(v => v.Library, library));

        Assert.Equal([PlanPath, "plan.md"], task.FindAll("a.library-ref").Select(a => a.TextContent).ToArray());
        Assert.Equal(["plan.md", "plan.md"], chat.FindAll("a.library-ref").Select(a => a.TextContent).ToArray());
    }

    /// <summary>A Mention of a known handle is drawn as a pill, and the Human's own handle also gets <c>mention--me</c>.</summary>
    [Fact]
    public async Task Mentions_KnownHandle_IsDrawnAsAPill()
    {
        await using MudBunitContext ctx = new();
        MentionMarks marks = new(["Nova"], ["Owen"]);

        IRenderedComponent<MarkdownView> cut = Render(ctx, "Hi @Nova and @Owen.", p => p.Add(v => v.Mentions, marks));

        Assert.Equal(["@Nova", "@Owen"], cut.FindAll("span.mention").Select(s => s.TextContent).ToArray());
        Assert.Equal(["@Owen"], cut.FindAll("span.mention--me").Select(s => s.TextContent).ToArray());
    }

    /// <summary>With <c>From</c> the library is the note resolver and a wikilink is linked; without it the same text is not a wikilink.</summary>
    [Fact]
    public async Task From_MakesTheLibraryTheNoteResolver_ForWikilinks()
    {
        await using MudBunitContext ctx = new();
        FakeLibraryNoteResolver library = new(resolveWikiLink: (_, _) => new LibraryReference("teams", "Marketing/plan.md", Exists: true));

        IRenderedComponent<MarkdownView> note = Render(ctx, "See [[plan]] for details.", p => p.Add(v => v.Library, library).Add(v => v.From, NotePath));
        IRenderedComponent<MarkdownView> chat = Render(ctx, "See [[plan]] for details.", p => p.Add(v => v.Library, library));

        Assert.Equal("?library=teams/Marketing/plan.md", note.Find("a.library-ref").GetAttribute("href"));
        Assert.Empty(chat.FindAll("a.library-ref"));
    }

    private static IRenderedComponent<MarkdownView> Render(MudBunitContext ctx, string markdown, Action<ComponentParameterCollectionBuilder<MarkdownView>>? more = null) =>
        ctx.Render<MarkdownView>(parameters =>
        {
            _ = parameters.Add(p => p.Markdown, markdown);
            more?.Invoke(parameters);
        });
}
