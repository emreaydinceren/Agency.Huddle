using Bunit;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components.Shared;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Renders <see cref="TeammateLabel"/> on its own. It is the row New chat and Invite show for a Teammate (design-system
/// "Components to build"; UX-R23): a <see cref="StatusDot"/>, a small <see cref="TeammateAvatar"/> and the name, with the
/// state's words as the dot's accessible name.
/// </summary>
public sealed class TeammateLabelTests
{
    /// <summary>The row renders the status dot, the avatar at Size.Small and the name as its own text.</summary>
    [Fact]
    public async Task Renders_StatusDotSmallAvatarAndName()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<TeammateLabel>(parameters => parameters
            .Add(p => p.Name, "Ada")
            .Add(p => p.State, PersonaState.Online));

        Assert.NotNull(cut.Find("span.agent-dot"));
        Assert.Contains("mud-avatar-small", cut.Find(".mud-avatar").ClassList);
        Assert.Equal("Ada", cut.Find("span.teammate-label > span:last-child").TextContent);
    }

    /// <summary>A User label (the Human) renders the avatar and the name, and no status dot.</summary>
    [Fact]
    public async Task IsUser_RendersNoStatusDot()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<TeammateLabel>(parameters => parameters
            .Add(p => p.Name, "Emre")
            .Add(p => p.IsUser, true));

        Assert.Empty(cut.FindAll("span.agent-dot"));
        Assert.NotNull(cut.Find(".mud-avatar"));
        Assert.Equal("Emre", cut.Find("span.teammate-label > span:last-child").TextContent);
    }

    /// <summary>The dot names the state in words, through <see cref="StatusDot.Label"/>, and not by its CSS class.</summary>
    [Theory]
    [InlineData(PersonaState.Starting, "Starting")]
    [InlineData(PersonaState.Online, "Online")]
    [InlineData(PersonaState.Degraded, "Degraded")]
    [InlineData(PersonaState.Offline, "Offline")]
    public async Task Dot_NamesTheStateInWords(PersonaState state, string expectedLabel)
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<TeammateLabel>(parameters => parameters
            .Add(p => p.Name, "Ada")
            .Add(p => p.State, state));

        var dot = cut.Find("span.agent-dot");
        Assert.Equal("img", dot.GetAttribute("role"));
        Assert.Equal(expectedLabel, dot.GetAttribute("aria-label"));
    }
}
