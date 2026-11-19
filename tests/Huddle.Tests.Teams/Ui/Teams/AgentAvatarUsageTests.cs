using Agency.Huddle.Tests.Css;

namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>
/// Pins U20 T20.1 (UX-051 to UX-054): Build a team's roster draws its avatars through the shared <c>AgentAvatar</c> rather
/// than a bare <c>MudAvatar</c>, and the stylesheet carries no <c>.agent-avatar</c> size override.
/// </summary>
public sealed class AgentAvatarUsageTests
{
    /// <summary>The stylesheet no longer declares the <c>.agent-avatar</c> size and radius override.</summary>
    [Fact]
    public void Stylesheet_HasNoAgentAvatarOverride()
    {
        string css = CssText.AppCss(CssNewlines.AsIs);

        Assert.DoesNotContain(".agent-avatar", css, StringComparison.Ordinal);
    }

    /// <summary>Build a team's roster card is the shared <c>ProfileCard</c>, and that card draws its avatar through <c>AgentAvatar</c>, not a bare <c>MudAvatar</c>.</summary>
    [Fact]
    public void BuildTeam_Source_UsesAgentAvatar()
    {
        string buildTeam = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Pages", "BuildTeam.razor"));
        string card = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Shared", "ProfileCard.razor"));

        Assert.Contains("<ProfileCard ", buildTeam, StringComparison.Ordinal); // contains-ok: haystack is the razor source file, which a render cannot distinguish
        Assert.DoesNotContain("<MudAvatar", buildTeam, StringComparison.Ordinal);
        Assert.Contains("<AgentAvatar ", card, StringComparison.Ordinal); // contains-ok: haystack is the razor source file, which a render cannot distinguish
        Assert.DoesNotContain("<MudAvatar", card, StringComparison.Ordinal);
    }
}
