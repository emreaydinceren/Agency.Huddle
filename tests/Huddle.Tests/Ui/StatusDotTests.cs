using Bunit;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components.Shared;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Renders <see cref="StatusDot"/> on its own for each <see cref="PersonaState"/> value. Stage 5 of
/// the MudBlazor migration pulled the <c>agent-dot</c> class mapping out of four copies (
/// <c>Teammates.razor</c>, <c>TeammateCard.razor</c>, <c>NewChat.razor</c>, <c>InviteTeammate.razor</c>)
/// into this one component, so this is now the single place that mapping is pinned.
/// </summary>
public sealed class StatusDotTests
{
    /// <summary>Each <see cref="PersonaState"/> renders its own, distinct <c>agent-dot</c> class, carried as the dot's own tooltip too.</summary>
    [Theory]
    [InlineData(PersonaState.Starting, "starting")]
    [InlineData(PersonaState.Online, "online")]
    [InlineData(PersonaState.Degraded, "degraded")]
    [InlineData(PersonaState.Offline, "offline")]
    public async Task State_RendersItsOwnDotClassAndTitle(PersonaState state, string expectedClass)
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<StatusDot>(parameters => parameters.Add(p => p.State, state));

        var dot = cut.Find("span.agent-dot");
        Assert.Contains(expectedClass, dot.ClassList);
        Assert.Equal(expectedClass, dot.GetAttribute("title"));
    }

    /// <summary>The four states map to four distinct classes - no two collapse onto the same dot.</summary>
    [Fact]
    public async Task AllFourStates_RenderDistinctClasses()
    {
        await using MudBunitContext ctx = new();
        PersonaState[] states = [PersonaState.Starting, PersonaState.Online, PersonaState.Degraded, PersonaState.Offline];

        var classes = states
            .Select(state => ctx.Render<StatusDot>(parameters => parameters.Add(p => p.State, state)).Find("span.agent-dot").GetAttribute("title"))
            .ToList();

        Assert.Equal(classes.Count, classes.Distinct(StringComparer.Ordinal).Count());
    }
}
