using Agency.Huddle.App.Services;

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
}