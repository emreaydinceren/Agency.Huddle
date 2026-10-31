using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Agency.Huddle.App.Components.Shared.AgentCards;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Tests for the <see cref="AgentCard"/> shell (UX-R01, UX-030, UX-031, UX-032): an outlined
/// <c>role="region"</c> panel with an accessible name, a <c>subtitle1</c> title, the body, and an actions
/// row that is left-aligned with the primary first.
/// </summary>
public sealed class AgentCardTests
{
    /// <summary>A Filled primary then an Outlined secondary, as every card lays its actions out.</summary>
    private static readonly RenderFragment PrimaryThenSecondary = builder =>
    {
        builder.AddContent(0, Button(Variant.Filled, "Approve"));
        builder.AddContent(1, Button(Variant.Outlined, "Decline"));
    };

    /// <summary>A <see cref="MudButton"/> with the given variant and label.</summary>
    /// <param name="variant">The button's variant.</param>
    /// <param name="label">The button's label.</param>
    private static RenderFragment Button(Variant variant, string label)
    {
        return builder =>
        {
            builder.OpenComponent<MudButton>(0);
            builder.AddAttribute(1, nameof(MudButton.Variant), variant);
            builder.AddAttribute(2, nameof(MudButton.ChildContent), (RenderFragment)(content => content.AddContent(0, label)));
            builder.CloseComponent();
        };
    }

    /// <summary>Renders the shell with a title, an explicit label, a body and the two actions.</summary>
    /// <param name="ctx">The bUnit context.</param>
    /// <param name="ariaLabel">The label to pass, or null to omit it.</param>
    private static IRenderedComponent<AgentCard> RenderCard(MudBunitContext ctx, string? ariaLabel) =>
        ctx.Render<AgentCard>(parameters => parameters
            .Add(card => card.Title, "Nova is asking")
            .Add(card => card.AriaLabel, ariaLabel)
            .Add(card => card.ChildContent, "<p class=\"card-body\">Body</p>")
            .Add(card => card.Actions, PrimaryThenSecondary));

    /// <summary>The shell is an outlined region named by its label, titled in subtitle1, not an alert, with the primary first and no right alignment (UX-030, UX-031, UX-032).</summary>
    [Fact]
    public async Task Renders_OutlinedRegionWithTitleAndLeftAlignedActionsPrimaryFirst()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<AgentCard> cut = RenderCard(ctx, "Question from Nova");

        IElement actions = AgentCardAssert.IsShell(cut, "Nova is asking", "Question from Nova");
        Assert.Equal(["Approve", "Decline"], AgentCardAssert.Verbs(actions));
    }

    /// <summary>With no explicit label the region is named by the title, so it is never unnamed.</summary>
    [Fact]
    public async Task AriaLabel_Omitted_FallsBackToTheTitle()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<AgentCard> cut = RenderCard(ctx, null);

        Assert.Equal("Nova is asking", cut.Find("[role=region]").GetAttribute("aria-label"));
    }

    /// <summary>The body sits between the title and the actions row, in that order.</summary>
    [Fact]
    public async Task Body_SitsBetweenTitleAndActions()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<AgentCard> cut = RenderCard(ctx, "Question from Nova");

        string html = cut.Find("[role=region]").InnerHtml;
        int title = html.IndexOf("agent-card-title", StringComparison.Ordinal);
        int body = html.IndexOf("card-body", StringComparison.Ordinal);
        int actions = html.IndexOf("agent-card-actions", StringComparison.Ordinal);
        Assert.True(title >= 0 && title < body && body < actions, $"Order was title {title}, body {body}, actions {actions}.");
    }

    /// <summary>A card with no actions renders no empty actions row.</summary>
    [Fact]
    public async Task NoActions_RendersNoActionsRow()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<AgentCard> cut = ctx.Render<AgentCard>(parameters => parameters
            .Add(card => card.Title, "Nova is asking")
            .Add(card => card.ChildContent, "<p>Body</p>"));

        Assert.Equal("Nova is asking", cut.Find(".agent-card-title").TextContent.Trim());
        Assert.Empty(cut.FindAll(".agent-card-actions"));
    }
}
