using Bunit;
using Agency.Huddle.App.Components.Shared;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Renders <see cref="EmptyState"/> on its own. An empty state is the one sentence that says what is missing and what to
/// do next (design-system "Patterns worth copying"; UX-R20, UX-077): a <c>Typo.body1</c> line in secondary text with
/// <c>role="status"</c>, inline in <c>.empty-state</c>, or in <c>.tab-empty</c> when it fills a pane.
/// </summary>
public sealed class EmptyStateTests
{
    private const string Sentence = "No pages are open. Choose one from the menu.";

    /// <summary>The sentence renders in a role=status region, body1 and secondary text, inside .empty-state.</summary>
    [Fact]
    public async Task Text_RendersAsStatusRegionInSecondaryText()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<EmptyState>(parameters => parameters.Add(p => p.Text, Sentence));

        var status = cut.Find(".empty-state [role='status']");
        Assert.Equal(Sentence, status.TextContent);
        Assert.Contains("mud-typography-body1", status.ClassList);
        Assert.Contains("mud-text-secondary", status.ClassList);
    }

    /// <summary>A full-pane empty state uses the .tab-empty root and not the inline .empty-state root.</summary>
    [Fact]
    public async Task FullPane_UsesTabEmptyRoot()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<EmptyState>(parameters => parameters
            .Add(p => p.Text, Sentence)
            .Add(p => p.FullPane, true));

        Assert.NotNull(cut.Find(".tab-empty [role='status']"));
        Assert.Empty(cut.FindAll(".empty-state"));
    }

    /// <summary>An inline empty state renders no .tab-empty root.</summary>
    [Fact]
    public async Task Inline_DoesNotUseTabEmptyRoot()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<EmptyState>(parameters => parameters.Add(p => p.Text, Sentence));

        Assert.Empty(cut.FindAll(".tab-empty"));
    }

    /// <summary>The optional icon renders above the text; without one there is no svg at all.</summary>
    [Fact]
    public async Task Icon_RendersOnlyWhenGiven()
    {
        await using MudBunitContext ctx = new();

        var plain = ctx.Render<EmptyState>(parameters => parameters.Add(p => p.Text, Sentence));
        var iconed = ctx.Render<EmptyState>(parameters => parameters
            .Add(p => p.Text, Sentence)
            .Add(p => p.Icon, Icons.Material.Filled.Inbox));

        Assert.Empty(plain.FindAll("svg"));
        Assert.NotNull(iconed.Find(".empty-state svg"));
    }

    /// <summary>The optional action renders inside the empty state.</summary>
    [Fact]
    public async Task Action_RendersInsideTheState()
    {
        await using MudBunitContext ctx = new();
        RenderFragment action = builder => builder.AddMarkupContent(0, "<button class=\"empty-action\">New chat</button>");

        var cut = ctx.Render<EmptyState>(parameters => parameters
            .Add(p => p.Text, Sentence)
            .Add(p => p.Action, action));

        Assert.NotNull(cut.Find(".empty-state button.empty-action"));
    }
}
