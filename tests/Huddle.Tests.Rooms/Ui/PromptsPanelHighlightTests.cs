using Agency.Huddle.App.Components.Settings;
using Agency.Huddle.Tests.Acp.Fakes;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Components.Shared;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// <see cref="PromptsPanel"/> honours <c>?highlight=&lt;key&gt;</c>: it selects that prompt, marks its list row and scrolls the
/// list to it, as a Helper card's "Edit the prompt" link needs (Helpers Spec §6.7, plan HL-17).
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

    /// <summary>The named prompt is selected (open in the editor, current in the list), and only its row is marked.</summary>
    [Fact]
    public async Task Highlight_SelectsAndMarksOnlyTheNamedPrompt()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<PromptsPanel> cut = Render(ctx, "/settings/prompts?highlight=" + Key);

        Assert.Contains("search-hit-flash", cut.Find($"[id='prompt-item-{Key}']").ClassList);
        Assert.Single(cut.FindAll(".search-hit-flash"));
        Assert.Equal("true", cut.Find($"[id='prompt-item-{Key}']").GetAttribute("aria-current"));
        Assert.NotNull(cut.Find($"[id='prompt-field-{Key}']"));
        Assert.NotNull(cut.Find("[id='prompt-item-helper.needsHuman.task']"));
        Assert.Empty(cut.FindAll("[id='prompt-field-helper.needsHuman.task']"));
    }

    /// <summary>The named prompt's row is scrolled into view once, by the page's existing scroll helper.</summary>
    [Fact]
    public async Task Highlight_ScrollsTheListRowIntoView()
    {
        await using MudBunitContext ctx = new();

        _ = Render(ctx, "/settings/prompts?highlight=" + Key);

        JSRuntimeInvocation call = Assert.Single(ctx.Module(JsModule.Scroll).Invocations["intoView"]);
        Assert.Equal("prompt-item-" + Key, Assert.Single(call.Arguments));
    }

    /// <summary>With no highlight, nothing is marked and nothing scrolls, and the editor holds the first prompt.</summary>
    [Fact]
    public async Task NoHighlight_MarksAndScrollsNothing()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<PromptsPanel> cut = Render(ctx, "/settings/prompts");

        Assert.Single(cut.FindAll(".prompts-detail-pane textarea"));
        Assert.Empty(cut.FindAll(".search-hit-flash"));
        Assert.DoesNotContain(ctx.Module(JsModule.Scroll).Invocations, static invocation => string.Equals(invocation.Identifier, "intoView", StringComparison.Ordinal));
    }

    /// <summary>A highlight naming no prompt marks nothing and does not scroll.</summary>
    [Fact]
    public async Task UnknownHighlight_MarksAndScrollsNothing()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<PromptsPanel> cut = Render(ctx, "/settings/prompts?highlight=no.such.prompt");

        Assert.Empty(cut.FindAll(".search-hit-flash"));
        Assert.DoesNotContain(ctx.Module(JsModule.Scroll).Invocations, static invocation => string.Equals(invocation.Identifier, "intoView", StringComparison.Ordinal));
    }
}
