using Agency.Huddle.App.Guide;
using Agency.Huddle.App.Services;

namespace Agency.Huddle.Tests.Guide;

/// <summary>Pins how <see cref="MarkdownRenderer"/> draws a <c>guide:</c> image inline (ADR-0087 D-1, D-2).</summary>
public sealed class MarkdownRendererGuideTests
{
    /// <summary>A known id becomes the diagram's SVG, inside a <c>guide-diagram</c> span, in its paragraph.</summary>
    [Fact]
    public void ToHtml_KnownId_InlinesTheDiagram()
    {
        string svg = GuideDiagrams.All["layers"].Svg;

        string html = MarkdownRenderer.ToHtml("Here is where things live:\n\n![The layers](guide:layers)");

        Assert.Equal(
            "<p>Here is where things live:</p>\n<p><span class=\"guide-diagram\" data-guide-id=\"layers\">" + svg + "</span></p>\n",
            html);
    }

    /// <summary>The diagram is drawn on every render path, including a Message with Mentions marked.</summary>
    [Fact]
    public void ToHtml_WithMentions_StillInlinesTheDiagram()
    {
        MentionMarks marks = new(["Nova"], ["Sam"]);

        string html = MarkdownRenderer.ToHtml("@Nova look: ![x](guide:layers)", null, null, marks);

        Assert.Contains("<span class=\"guide-diagram\" data-guide-id=\"layers\"><svg", html, StringComparison.Ordinal); // contains-ok: the Mention markup around it is pinned by MarkdownRendererMentionTests.
    }

    /// <summary>An unknown id renders as its alt text, never as an image.</summary>
    [Fact]
    public void ToHtml_UnknownId_RendersTheAltText()
    {
        string html = MarkdownRenderer.ToHtml("![The task flow](guide:no-such-diagram)");

        Assert.Equal("<p>The task flow</p>\n", html);
    }

    /// <summary>An unknown id with no alt text renders as the id.</summary>
    [Fact]
    public void ToHtml_UnknownIdWithoutAlt_RendersTheId()
    {
        string html = MarkdownRenderer.ToHtml("![](guide:no-such-diagram)");

        Assert.Equal("<p>no-such-diagram</p>\n", html);
    }

    /// <summary>A plain link to a <c>guide:</c> address is not a diagram, and its address is not trusted.</summary>
    [Fact]
    public void ToHtml_PlainLinkToGuideAddress_IsNotInlined()
    {
        string html = MarkdownRenderer.ToHtml("[the layers](guide:layers)");

        Assert.DoesNotContain("guide-diagram", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<svg", html, StringComparison.Ordinal);
    }

    /// <summary>SVG markup written in the Message itself is still escaped: only the catalog's markup is ever written.</summary>
    [Fact]
    public void ToHtml_SvgWrittenInText_IsEscaped()
    {
        string html = MarkdownRenderer.ToHtml("<svg onload=\"alert(1)\"></svg> ![x](guide:<svg onload=alert(1)>)");

        Assert.DoesNotContain("<svg onload", html, StringComparison.Ordinal);
        Assert.DoesNotContain("guide-diagram", html, StringComparison.Ordinal);
    }
}
