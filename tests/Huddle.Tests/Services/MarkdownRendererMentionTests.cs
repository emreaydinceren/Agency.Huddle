using Agency.Huddle.App.Services;

namespace Agency.Huddle.Tests.Services;

/// <summary>
/// Pins how <see cref="MarkdownRenderer.ToHtml(string, Agency.Huddle.App.Tasks.ITaskReferenceResolver?, Agency.Huddle.App.Library.ILibraryReferenceResolver?, MentionMarks?)"/>
/// wraps Mentions in a <c>mention</c> span, and leaves everything else exactly as the three-argument overload renders it.
/// </summary>
public sealed class MarkdownRendererMentionTests
{
    /// <summary>A Room holding Emily, Emily Lee and a Teammate with the Alias "ops", seen by a Human named Sam (Alias "me").</summary>
    private static readonly MentionMarks Marks = new(["Emily", "Emily Lee", "Morgan", "ops"], ["Sam", "me"]);

    /// <summary>Renders <paramref name="markdown"/> with <see cref="Marks"/> and no Task or Library resolver.</summary>
    /// <param name="markdown">The Message text.</param>
    private static string Render(string markdown) => MarkdownRenderer.ToHtml(markdown, null, null, MarkdownRendererMentionTests.Marks);

    /// <summary>A known Teammate Name becomes a <c>mention</c> span holding the <c>@</c> and the Name.</summary>
    [Fact]
    public void ToHtml_KnownName_IsWrappedInMentionSpan()
    {
        string html = MarkdownRendererMentionTests.Render("hi @Morgan, thanks");

        Assert.Equal("<p>hi <span class=\"mention\">@Morgan</span>, thanks</p>\n", html);
    }

    /// <summary>A multi-word Name is marked whole, longest first, rather than stopping at "Emily".</summary>
    [Fact]
    public void ToHtml_MultiWordName_IsMarkedWhole()
    {
        string html = MarkdownRendererMentionTests.Render("ask @Emily Lee today");

        Assert.Equal("<p>ask <span class=\"mention\">@Emily Lee</span> today</p>\n", html);
    }

    /// <summary>A shorter Name still matches on its own when the longer one does not follow.</summary>
    [Fact]
    public void ToHtml_ShorterName_MatchesWhenLongerDoesNot()
    {
        string html = MarkdownRendererMentionTests.Render("ask @Emily now");

        Assert.Equal("<p>ask <span class=\"mention\">@Emily</span> now</p>\n", html);
    }

    /// <summary>An Alias is accepted anywhere a Name is.</summary>
    [Fact]
    public void ToHtml_Alias_IsWrappedInMentionSpan()
    {
        string html = MarkdownRendererMentionTests.Render("@ops deploy it");

        Assert.Equal("<p><span class=\"mention\">@ops</span> deploy it</p>\n", html);
    }

    /// <summary>The Human's own Name and Alias also carry <c>mention--me</c>.</summary>
    [Fact]
    public void ToHtml_OwnHandle_CarriesMentionMeClass()
    {
        string html = MarkdownRendererMentionTests.Render("@Sam and @me");

        Assert.Equal("<p><span class=\"mention mention--me\">@Sam</span> and <span class=\"mention mention--me\">@me</span></p>\n", html);
    }

    /// <summary>Matching ignores case, and the span keeps the characters exactly as typed.</summary>
    [Fact]
    public void ToHtml_DifferentCase_MatchesAndKeepsTypedText()
    {
        string html = MarkdownRendererMentionTests.Render("hey @eMiLy lEe");

        Assert.Equal("<p>hey <span class=\"mention\">@eMiLy lEe</span></p>\n", html);
    }

    /// <summary>An <c>@word</c> naming nobody in the Room stays plain text.</summary>
    [Fact]
    public void ToHtml_UnknownHandle_StaysPlainText()
    {
        string html = MarkdownRendererMentionTests.Render("hi @Nobody");

        Assert.Equal("<p>hi @Nobody</p>\n", html);
    }

    /// <summary>A handle that only begins a longer word is not a Mention, so "@Morganite" stays plain.</summary>
    [Fact]
    public void ToHtml_HandleFollowedByNameCharacter_StaysPlainText()
    {
        string html = MarkdownRendererMentionTests.Render("hi @Morganite");

        Assert.Equal("<p>hi @Morganite</p>\n", html);
    }

    /// <summary>An email address whose domain is a known handle is not a Mention.</summary>
    [Fact]
    public void ToHtml_EmailAddress_StaysPlainText()
    {
        string html = MarkdownRendererMentionTests.Render("write to a@ops.example.com");

        Assert.Equal("<p>write to a@ops.example.com</p>\n", html);
    }

    /// <summary>A Mention inside an inline code span is left as code.</summary>
    [Fact]
    public void ToHtml_InsideInlineCode_IsNotMarked()
    {
        string html = MarkdownRendererMentionTests.Render("run `@Morgan` now");

        Assert.Equal("<p>run <code>@Morgan</code> now</p>\n", html);
    }

    /// <summary>A Mention inside a fenced code block is left as code.</summary>
    [Fact]
    public void ToHtml_InsideCodeBlock_IsNotMarked()
    {
        string html = MarkdownRendererMentionTests.Render("```\n@Morgan\n```");

        Assert.DoesNotContain("mention", html, StringComparison.Ordinal);
    }

    /// <summary>A Mention inside a Markdown link's text is not given a nested span.</summary>
    [Fact]
    public void ToHtml_InsideLink_IsNotMarked()
    {
        string html = MarkdownRendererMentionTests.Render("[@Morgan](https://a.b)");

        Assert.Equal("<p><a href=\"https://a.b\">@Morgan</a></p>\n", html);
    }

    /// <summary>A known handle inside a bare autolinked URL is not marked.</summary>
    [Fact]
    public void ToHtml_InsideAutolink_IsNotMarked()
    {
        string html = MarkdownRendererMentionTests.Render("see https://a.b/@Morgan");

        Assert.DoesNotContain("mention", html, StringComparison.Ordinal);
    }

    /// <summary>A Mention beside emphasis is still marked, since the emphasis only splits the literal run.</summary>
    [Fact]
    public void ToHtml_BesideEmphasis_IsMarked()
    {
        string html = MarkdownRendererMentionTests.Render("**now** @Morgan");

        Assert.Equal("<p><strong>now</strong> <span class=\"mention\">@Morgan</span></p>\n", html);
    }

    /// <summary>An escaped <c>\@</c> is not drawn as a Mention, and its text is not duplicated.</summary>
    [Fact]
    public void ToHtml_EscapedAt_StaysPlainText()
    {
        string html = MarkdownRendererMentionTests.Render("hi \\@Morgan");

        Assert.Equal("<p>hi @Morgan</p>\n", html);
    }

    /// <summary>Markup characters next to a Mention are still HTML-escaped.</summary>
    [Fact]
    public void ToHtml_MentionBesideMarkup_EscapesMarkup()
    {
        string html = MarkdownRendererMentionTests.Render("<b>@Morgan</b>");

        Assert.Equal("<p>&lt;b&gt;<span class=\"mention\">@Morgan</span>&lt;/b&gt;</p>\n", html);
    }

    /// <summary>A handle holding markup characters is escaped inside the span, never written as markup.</summary>
    [Fact]
    public void ToHtml_HandleWithMarkupCharacters_IsEscaped()
    {
        MentionMarks marks = new(["x<i>"], []);

        string html = MarkdownRenderer.ToHtml("@x<i> done", null, null, marks);

        Assert.DoesNotContain("<i>", html, StringComparison.Ordinal);
    }

    /// <summary>With no <see cref="MentionMarks"/>, the output is byte-identical to the three-argument overload.</summary>
    /// <param name="markdown">The Message text.</param>
    [Theory]
    [InlineData("hi @Morgan, and @Emily Lee")]
    [InlineData("**bold** @Sam `@ops` [x](https://a.b)")]
    [InlineData("a@example.com and \\@me")]
    public void ToHtml_NullMentions_MatchesThreeArgumentOverload(string markdown)
    {
        string withoutMarks = MarkdownRenderer.ToHtml(markdown, null, null);

        string html = MarkdownRenderer.ToHtml(markdown, null, null, mentions: null);

        Assert.Equal(withoutMarks, html);
    }

    /// <summary>Empty handle lists mark nothing.</summary>
    [Fact]
    public void ToHtml_EmptyHandles_MarksNothing()
    {
        string html = MarkdownRenderer.ToHtml("hi @Morgan", null, null, new MentionMarks([], []));

        Assert.Equal("<p>hi @Morgan</p>\n", html);
    }
}