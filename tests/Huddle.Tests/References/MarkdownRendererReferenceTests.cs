using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Ui;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace Agency.Huddle.Tests.References;

/// <summary>Pins the renderer's two reference link shapes, the Room and Team addresses (spec §7, D-10), and that nothing broader passes <c>IsSafe</c>.</summary>
public sealed class MarkdownRendererReferenceTests
{
    /// <summary>A Room address is kept as written and marked with the room-ref class (AT-T5).</summary>
    [Fact]
    public void RoomLink_IsKeptWithRoomRefClass()
    {
        string html = MarkdownRenderer.ToHtml("[Design](/rooms/design-1)");

        Assert.Equal("<p><a href=\"/rooms/design-1\" class=\"room-ref\">Design</a></p>\n", html);
    }

    /// <summary>A Team address is kept as written and marked with the team-ref class (AT-T5).</summary>
    [Fact]
    public void TeamLink_IsKeptWithTeamRefClass()
    {
        string html = MarkdownRenderer.ToHtml("[Alpha Team](/teams/Alpha%20Team)");

        Assert.Equal("<p><a href=\"/teams/Alpha%20Team\" class=\"team-ref\">Alpha Team</a></p>\n", html);
    }

    /// <summary>Every shape outside the two admitted ones renders as a hash and carries no reference class (AT-T5).</summary>
    /// <param name="url">The link address written by the sender.</param>
    [Theory]
    [InlineData("/rooms/../x")]
    [InlineData("/rooms/a/b")]
    [InlineData("/rooms/a?x=1")]
    [InlineData("/rooms/a#f")]
    [InlineData("/teams/a%2Fb")]
    [InlineData("/teams/new")]
    [InlineData("/teams/_x")]
    [InlineData("/rooms/")]
    [InlineData("/teamsx/a")]
    [InlineData("//rooms/a")]
    [InlineData("/rooms/a%2e%2e")]
    [InlineData("/teams/%zz")]
    public void RefusedShapes_BecomeHash(string url)
    {
        string html = MarkdownRenderer.ToHtml($"[x]({url})");

        Assert.Equal("<p><a href=\"#\">x</a></p>\n", html);
    }

    /// <summary>The shapes the renderer admitted before this change are unchanged and get no reference class (AT-T5).</summary>
    /// <param name="url">The link address.</param>
    /// <param name="expectedHref">The href that is rendered.</param>
    [Theory]
    [InlineData("http://a.b/c", "http://a.b/c")]
    [InlineData("mailto:a@b.c", "mailto:a@b.c")]
    [InlineData("/tasks/item/GROW-0001", "/tasks/item/GROW-0001")]
    [InlineData("?library=%2Fa.md", "#")]
    public void ExistingSafeLinks_Unchanged(string url, string expectedHref)
    {
        string html = MarkdownRenderer.ToHtml($"[x]({url})");
        IElement anchor = new HtmlParser().ParseDocument(html).QuerySelectorAll("a")[0];

        Assert.Equal(expectedHref, anchor.GetAttribute("href"));
        Assert.DoesNotContain("room-ref", anchor.ClassList, StringComparer.Ordinal);
        Assert.DoesNotContain("team-ref", anchor.ClassList, StringComparer.Ordinal);
    }

    /// <summary>A link whose address is a script or data scheme is refused and rendered as <c>#</c>, whatever the letter case (spec §7, D-10).</summary>
    /// <param name="url">The unsafe address written in the link.</param>
    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("JavaScript:alert(1)")]
    [InlineData("data:text/html,x")]
    public void UnsafeScheme_RefusedToHash(string url)
    {
        string html = MarkdownRenderer.ToHtml($"[x]({url})");
        IElement anchor = new HtmlParser().ParseDocument(html).QuerySelectorAll("a")[0];

        Assert.Equal("#", anchor.GetAttribute("href"));
    }

    /// <summary>A label with markup renders as escaped text, never as HTML (AT-T5).</summary>
    [Fact]
    public void RoomLinkLabel_IsTextNotHtml()
    {
        string html = MarkdownRenderer.ToHtml("[<b>x</b>](/rooms/a)");
        IElement anchor = new HtmlParser().ParseDocument(html).QuerySelectorAll("a")[0];

        Assert.Equal("<b>x</b>", anchor.TextContent);
        Assert.Empty(anchor.Children);
    }

    /// <summary>An image whose address is a Room address gets no reference class, since only links are references (AT-T5).</summary>
    [Fact]
    public void RoomAddressAsImage_GetsNoReferenceClass()
    {
        string html = MarkdownRenderer.ToHtml("![x](/rooms/a)");

        Assert.DoesNotContain("room-ref", html, StringComparison.Ordinal);
    }

    /// <summary>A Task id still links through the shared pattern (AT-T5).</summary>
    [Fact]
    public void TaskIds_StillLinkThroughTheSharedPattern()
    {
        Assert.True(TaskId.TryParse("GROW-0001", out TaskId id));
        FakeTaskReferenceResolver resolver = new();
        resolver.Add(new TaskReference(id, "Grow", Closed: false));

        string html = MarkdownRenderer.ToHtml("See GROW-0001 and grow-0001.", resolver);
        IElement anchor = new HtmlParser().ParseDocument(html).QuerySelectorAll("a")[0];

        Assert.Equal("/tasks/item/GROW-0001", anchor.GetAttribute("href"));
        Assert.Contains("task-ref", anchor.ClassList, StringComparer.Ordinal);
        Assert.Single(new HtmlParser().ParseDocument(html).QuerySelectorAll("a"));
    }
}
