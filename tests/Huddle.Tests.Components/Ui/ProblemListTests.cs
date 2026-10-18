using Bunit;
using Microsoft.AspNetCore.Components;
using Agency.Huddle.App.Components.Settings;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Tests for <see cref="ProblemList"/>, the one "Problems" section the Skills and Team Types panels
/// share (UX-058, UX-R16): always present, with an empty line instead of vanishing.
/// </summary>
public sealed class ProblemListTests
{
    /// <summary>With no problems the section still shows its heading, and says "No problems." instead of rendering nothing (UX-058).</summary>
    [Fact]
    public async Task NoProblems_ShowsHeadingAndTheEmptyLine()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<ProblemList>(parameters => parameters
            .Add(p => p.Count, 0));

        Assert.Equal("Problems", cut.Find(".problem-list-heading").TextContent.Trim());
        Assert.Equal("No problems.", cut.Find(".problem-list-empty").TextContent.Trim());
    }

    /// <summary>With problems the caller's cards render under the heading and the empty line is gone.</summary>
    [Fact]
    public async Task WithProblems_RendersTheCardsAndNoEmptyLine()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<ProblemList>(parameters => parameters
            .Add(p => p.Count, 2)
            .Add(p => p.ChildContent, (RenderFragment)(builder => builder.AddMarkupContent(0, "<div class=\"card\">one</div><div class=\"card\">two</div>"))));

        Assert.Equal("Problems", cut.Find(".problem-list-heading").TextContent.Trim());
        Assert.Equal(2, cut.FindAll(".problem-list .card").Count);
        Assert.Empty(cut.FindAll(".problem-list-empty"));
    }

    /// <summary>With no problems the caller's content is not rendered even if one was passed.</summary>
    [Fact]
    public async Task NoProblems_DoesNotRenderTheContent()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<ProblemList>(parameters => parameters
            .Add(p => p.Count, 0)
            .Add(p => p.ChildContent, (RenderFragment)(builder => builder.AddMarkupContent(0, "<div class=\"card\">stale</div>"))));

        Assert.Empty(cut.FindAll(".card"));
    }
}
