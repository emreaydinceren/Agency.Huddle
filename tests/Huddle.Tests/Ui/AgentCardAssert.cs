using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Data;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Shared assertions for the five Transcript agent cards (UX-030 to UX-035): every card renders the
/// same shell, so one helper pins it and each card's test supplies its own title and label.
/// </summary>
internal static class AgentCardAssert
{
    /// <summary>
    /// Registers what the shared <c>AgentCardBase</c> needs beside a card's own services: the Team
    /// Directory for the archived-Room guard and the <see cref="RoomEvents"/> hub it subscribes to.
    /// </summary>
    /// <param name="ctx">The bUnit context to register into.</param>
    /// <param name="directory">The directory that knows the card's Room.</param>
    public static void RegisterRoomServices(MudBunitContext ctx, ITeamDirectory directory)
    {
        ctx.Services.TryAddSingleton(directory);
        ctx.Services.TryAddSingleton(new RoomEvents(NullLogger<RoomEvents>.Instance));
    }

    /// <summary>
    /// Pins the shell: an outlined <c>role="region"</c> panel (not a <c>MudAlert</c>, not
    /// <c>role="alert"</c>) with the expected <c>aria-label</c>, a <c>subtitle1</c> title, and an actions
    /// row that is left-aligned with the Filled primary first and no Filled button after it.
    /// </summary>
    /// <param name="cut">The rendered card.</param>
    /// <param name="title">The expected card title.</param>
    /// <param name="label">The expected <c>aria-label</c> of the region.</param>
    /// <param name="hasPrimary">Whether the first action is a Filled primary; a card that sends on tap has none.</param>
    /// <returns>The actions row, for further assertions.</returns>
    public static IElement IsShell<TComponent>(IRenderedComponent<TComponent> cut, string title, string label, bool hasPrimary = true)
        where TComponent : IComponent
    {
        IElement region = cut.Find("[role=region]");
        Assert.Contains("mud-paper-outlined", region.ClassList);
        Assert.Equal(label, region.GetAttribute("aria-label"));
        Assert.Empty(cut.FindAll("[role=alert]"));
        Assert.Empty(cut.FindAll(".mud-alert"));

        IElement heading = region.QuerySelector(".agent-card-title") ?? throw new InvalidOperationException("The card has no .agent-card-title.");
        Assert.Contains("mud-typography-subtitle1", heading.ClassList);
        Assert.Equal(title, heading.TextContent.Trim());

        IElement actions = region.QuerySelector(".agent-card-actions") ?? throw new InvalidOperationException("The card has no .agent-card-actions row.");
        string actionClasses = actions.ClassName ?? string.Empty;
        Assert.DoesNotContain("justify-end", actionClasses, StringComparison.Ordinal);
        Assert.DoesNotContain("justify-space-between", actionClasses, StringComparison.Ordinal);
        Assert.DoesNotContain("ml-auto", actionClasses, StringComparison.Ordinal);

        List<IElement> buttons = [.. actions.QuerySelectorAll("button")];
        Assert.NotEmpty(buttons);
        Assert.Equal(hasPrimary, buttons[0].ClassList.Contains("mud-button-filled"));
        Assert.DoesNotContain(buttons.Skip(1), static button => button.ClassList.Contains("mud-button-filled"));
        return actions;
    }

    /// <summary>The visible labels of the buttons in <paramref name="actions"/>, in order.</summary>
    /// <param name="actions">The actions row returned by <see cref="IsShell"/>.</param>
    public static List<string> Verbs(IElement actions)
    {
        return [.. actions.QuerySelectorAll("button").Select(static button => button.TextContent.Trim())];
    }
}
