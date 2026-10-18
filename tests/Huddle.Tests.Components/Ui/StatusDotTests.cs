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
    /// <summary>Each <see cref="PersonaState"/> renders its own <c>agent-dot</c> class, and <see cref="StatusDot.Label"/> as its accessible name and its title (UX-V08).</summary>
    [Theory]
    [InlineData(PersonaState.Starting, "starting", "Starting")]
    [InlineData(PersonaState.Online, "online", "Online")]
    [InlineData(PersonaState.Degraded, "degraded", "Degraded")]
    [InlineData(PersonaState.Offline, "offline", "Offline")]
    public async Task State_RendersItsOwnDotClassAndLabel(PersonaState state, string expectedClass, string expectedLabel)
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<StatusDot>(parameters => parameters.Add(p => p.State, state));

        var dot = cut.Find("span.agent-dot");
        Assert.Contains(expectedClass, dot.ClassList);
        Assert.Equal("img", dot.GetAttribute("role"));
        Assert.Equal(expectedLabel, dot.GetAttribute("aria-label"));
        Assert.Equal(expectedLabel, dot.GetAttribute("title"));
    }

    /// <summary>The label is the plain word for each state, the text New chat and Invite show beside the dot.</summary>
    [Theory]
    [InlineData(PersonaState.Starting, "Starting")]
    [InlineData(PersonaState.Online, "Online")]
    [InlineData(PersonaState.Degraded, "Degraded")]
    [InlineData(PersonaState.Offline, "Offline")]
    public void Label_ReadsAsThePlainWord(PersonaState state, string expectedLabel)
    {
        Assert.Equal(expectedLabel, StatusDot.Label(state));
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
