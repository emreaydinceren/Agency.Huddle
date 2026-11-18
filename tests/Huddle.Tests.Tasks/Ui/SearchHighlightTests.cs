using Bunit;
using Agency.Huddle.App.Components.Shared;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Pins <see cref="SearchHighlight"/>, which replaced <c>MudHighlighter</c>: MudHighlighter splits with a regular
/// expression under a fixed one-second match timeout, so a server stalled for a second inside one scan threw
/// <c>RegexMatchTimeoutException</c> from <c>OnParametersSet</c> (red CI run on <c>main</c>, 2026-11-18,
/// <c>TeamTasksTabTests.Search_OnTheProjectPage_NarrowsWithinTheProject</c>). A stall cannot be forced from a
/// test, so the build bans <c>MudHighlighter</c> itself (<c>BannedSymbols.txt</c>) and these tests pin what the
/// replacement draws, including the inputs a regex would have to escape.
/// </summary>
public sealed class SearchHighlightTests
{
    /// <summary>Every occurrence is marked, ignoring case, and the text keeps the case it had.</summary>
    [Fact]
    public async Task Term_MarksEveryOccurrenceIgnoringCase()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<SearchHighlight> cut = Render(ctx, "Alpha beta ALPHA", "alpha");

        Assert.Equal(["Alpha", "ALPHA"], [.. cut.FindAll("mark").Select(mark => mark.TextContent)]);
        Assert.Equal("Alpha beta ALPHA", PlainText(cut));
    }

    /// <summary>Regular-expression characters in the term are plain text, and adjacent occurrences are each marked.</summary>
    [Fact]
    public async Task Term_WithRegexCharacters_IsMatchedLiterally()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<SearchHighlight> cut = Render(ctx, "a.b a.ba.b axb", "a.b");

        Assert.Equal(["a.b", "a.b", "a.b"], [.. cut.FindAll("mark").Select(mark => mark.TextContent)]);
    }

    /// <summary>A null, empty or whitespace term, or a null text, marks nothing and does not throw.</summary>
    [Theory]
    [InlineData("Some title", null)]
    [InlineData("Some title", "")]
    [InlineData("Some title", "  ")]
    [InlineData(null, "title")]
    public async Task NoUsableInput_MarksNothing(string? text, string? term)
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<SearchHighlight> cut = Render(ctx, text, term);

        Assert.Empty(cut.FindAll("mark"));
        Assert.Equal(text ?? string.Empty, PlainText(cut));
    }

    /// <summary>Markup in the text is encoded, never parsed, so a title such as <c>&lt;b&gt;x&lt;/b&gt;</c> cannot inject an element.</summary>
    [Fact]
    public async Task Text_WithMarkup_IsEncoded()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<SearchHighlight> cut = Render(ctx, "<b>bold</b> text", "bold");

        Assert.Empty(cut.FindAll("b"));
        Assert.Equal("bold", Assert.Single(cut.FindAll("mark")).TextContent);
        Assert.Equal("<b>bold</b> text", PlainText(cut));
    }

    /// <summary>A long text full of matches draws without throwing.</summary>
    [Fact]
    public async Task LongTextOfMatches_DrawsWithoutThrowing()
    {
        await using MudBunitContext ctx = new();
        string text = new('a', 200_000);

        IRenderedComponent<SearchHighlight> cut = Render(ctx, text, "a");

        Assert.Equal(200_000, cut.FindAll("mark").Count);
    }

    /// <summary>The text a reader sees: every node of the render joined.</summary>
    private static string PlainText(IRenderedComponent<SearchHighlight> cut) => string.Concat(cut.Nodes.Select(node => node.TextContent));

    /// <summary>Renders <see cref="SearchHighlight"/> for the given parameters.</summary>
    private static IRenderedComponent<SearchHighlight> Render(MudBunitContext ctx, string? text, string? term) =>
        ctx.Render<SearchHighlight>(parameters => parameters
            .Add(component => component.Text, text)
            .Add(component => component.Term, term));
}
