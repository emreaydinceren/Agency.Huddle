using Agency.Huddle.App.Components.Settings;
using Agency.Huddle.Tests.Acp.Fakes;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// <see cref="PromptsPanel"/> honours <c>?highlight=&lt;key&gt;</c>: it marks that field and scrolls it into view, as a Helper
/// card's "Edit the prompt" link needs (Helpers Spec §6.7, plan HL-17).
/// </summary>
public sealed class PromptsPanelHighlightTests
{
    private const string Key = "helper.needsHuman.system";

    /// <summary>Renders the panel with every prompt, after navigating to the given address.</summary>
    /// <param name="ctx">The bUnit context.</param>
    /// <param name="address">The address the Human arrived at.</param>
    private static IRenderedComponent<PromptsPanel> Render(MudBunitContext ctx, string address)
    {
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo(address);
        return ctx.Render<PromptsPanel>(parameters => parameters
            .Add(p => p.Groups, PromptFieldFactory.Build(new FakePromptSource(), new Dictionary<string, string>(StringComparer.Ordinal))));
    }

    /// <summary>Only the named field is marked, and each field has an id the scroll can find.</summary>
    [Fact]
    public async Task Highlight_MarksOnlyTheNamedField()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<PromptsPanel> cut = Render(ctx, "/settings/prompts?highlight=" + Key);

        Assert.Contains("search-hit-flash", cut.Find($"[id='prompt-field-{Key}']").ClassList);
        Assert.Single(cut.FindAll(".search-hit-flash"));
        Assert.NotNull(cut.Find("[id='prompt-field-helper.needsHuman.task']"));
    }

    /// <summary>The named field is scrolled into view once, by the page's existing scroll helper.</summary>
    [Fact]
    public async Task Highlight_ScrollsTheFieldIntoView()
    {
        await using MudBunitContext ctx = new();

        _ = Render(ctx, "/settings/prompts?highlight=" + Key);

        JSRuntimeInvocation call = Assert.Single(ctx.JSInterop.Invocations["teamScroll.intoView"]);
        Assert.Equal("prompt-field-" + Key, Assert.Single(call.Arguments));
    }

    /// <summary>With no highlight, nothing is marked and nothing scrolls.</summary>
    [Fact]
    public async Task NoHighlight_MarksAndScrollsNothing()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<PromptsPanel> cut = Render(ctx, "/settings/prompts");

        Assert.Empty(cut.FindAll(".search-hit-flash"));
        Assert.DoesNotContain(ctx.JSInterop.Invocations, static invocation => string.Equals(invocation.Identifier, "teamScroll.intoView", StringComparison.Ordinal));
    }

    /// <summary>A highlight naming no prompt marks nothing and does not scroll.</summary>
    [Fact]
    public async Task UnknownHighlight_MarksAndScrollsNothing()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<PromptsPanel> cut = Render(ctx, "/settings/prompts?highlight=no.such.prompt");

        Assert.Empty(cut.FindAll(".search-hit-flash"));
        Assert.DoesNotContain(ctx.JSInterop.Invocations, static invocation => string.Equals(invocation.Identifier, "teamScroll.intoView", StringComparison.Ordinal));
    }
}
