using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// Which Work Modes skip the connector approval prompt and so refuse the connectors (Agent Connectors spec 8.4), and that the
/// stored switch reaches the session <c>_meta</c> both ways only when the mode asks.
/// </summary>
public sealed class ConnectorModeTests
{
    private static WorkModePolicy Policy(params string[] hidden) => new(Options.Create(new TeamOptions { Acp = { HiddenModes = hidden } }));

    /// <summary>Bypass and auto skip approval when offered; every asking mode, and a hidden mode (replaced by the default), does not.</summary>
    [Fact]
    public void SkipsApproval_OnlyForOfferedBypassAndAuto()
    {
        WorkModePolicy offered = Policy("plan");
        WorkModePolicy defaults = Policy("bypassPermissions", "auto", "plan");

        Assert.True(offered.SkipsApproval("bypassPermissions"));
        Assert.True(offered.SkipsApproval("auto"));
        Assert.False(offered.SkipsApproval("default"));
        Assert.False(offered.SkipsApproval("acceptEdits"));
        Assert.False(offered.SkipsApproval(null));
        Assert.False(defaults.SkipsApproval("bypassPermissions"));
    }

    /// <summary>The switch reaches the meta both ways, and a stored "on" with a skipping mode is not passed.</summary>
    [Fact]
    public void EffectiveConnectors_FollowsTheSwitch_ExceptWhenTheModeSkipsApproval()
    {
        WorkModePolicy policy = Policy("plan");

        Assert.True(DotAcpAgentHostFactory.EffectiveConnectors(new Persona("nova", "x", Connectors: true), policy));
        Assert.False(DotAcpAgentHostFactory.EffectiveConnectors(new Persona("nova", "x", Connectors: false), policy));
        Assert.False(DotAcpAgentHostFactory.EffectiveConnectors(new Persona("nova", "x", WorkMode: "bypassPermissions", Connectors: true), policy));
        Assert.True(DotAcpAgentHostFactory.EffectiveConnectors(new Persona("nova", "x", WorkMode: "acceptEdits", Connectors: true), policy));
    }
}
