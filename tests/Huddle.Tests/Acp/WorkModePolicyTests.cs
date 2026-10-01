using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// Pins <see cref="WorkModePolicy"/>, the list of Work Modes that are never offered or sent unless an
/// operator lifts it. Every configuration case binds through <c>ConfigurationBinder</c> from an
/// in-memory source: the default lives in code because the binder writes an array into a
/// pre-populated one by index, so only a real bind proves the three meanings of
/// <c>Team:Acp:HiddenModes</c>.
/// </summary>
public sealed class WorkModePolicyTests
{
    /// <summary>With the key absent, <c>bypassPermissions</c> and <c>auto</c> are hidden and everything else is offered.</summary>
    [Fact]
    public void IsOffered_KeyAbsent_HidesBypassPermissionsAndAuto()
    {
        WorkModePolicy policy = Policy([]);

        Assert.False(policy.IsOffered("bypassPermissions"));
        Assert.False(policy.IsOffered("auto"));
        Assert.True(policy.IsOffered("default"));
        Assert.True(policy.IsOffered("acceptEdits"));
    }

    /// <summary>
    /// <c>plan</c> is hidden by default (OQ-2, measured live on 2026-09-30): the Adapter ends a refused plan
    /// Turn as cancelled with the plan in a tool call, not in text, so nothing reaches the Room. It stays
    /// hidden until a later phase captures the plan from the tool call.
    /// </summary>
    [Fact]
    public void IsOffered_KeyAbsent_HidesPlanUntilItCanShowItsPlan()
    {
        WorkModePolicy policy = Policy([]);

        Assert.False(policy.IsOffered("plan"));
    }

    /// <summary>
    /// "Hide nothing" is written as a list holding one empty string, because configuration cannot
    /// express an empty list: it replaces the default and hides no real mode, so an operator can offer
    /// every advertised mode.
    /// </summary>
    [Fact]
    public void IsOffered_ListOfOneEmptyString_HidesNothing()
    {
        WorkModePolicy policy = Policy(new Dictionary<string, string?> { ["Team:Acp:HiddenModes:0"] = string.Empty });

        Assert.True(policy.IsOffered("bypassPermissions"));
        Assert.True(policy.IsOffered("auto"));
        Assert.True(policy.IsOffered("plan"));
    }

    /// <summary>
    /// Pins why "hide nothing" needs the empty-string element: <c>ConfigurationBinder</c> reads an empty
    /// value as absent, so a bare empty <c>HiddenModes</c> still gets the default. If this ever fails,
    /// the platform changed and the documented spelling can be simplified.
    /// </summary>
    [Fact]
    public void IsOffered_EmptyValue_IsReadAsAbsentSoTheDefaultApplies()
    {
        WorkModePolicy policy = Policy(new Dictionary<string, string?> { ["Team:Acp:HiddenModes"] = string.Empty });

        Assert.False(policy.IsOffered("bypassPermissions"));
        Assert.False(policy.IsOffered("auto"));
    }

    /// <summary>
    /// A configured list replaces the default exactly. Writing <c>["auto"]</c> over a pre-filled
    /// default would give <c>["auto", "auto"]</c> and silently unhide <c>bypassPermissions</c>.
    /// </summary>
    [Fact]
    public void IsOffered_ConfiguredList_ReplacesTheDefaultExactly()
    {
        WorkModePolicy policy = Policy(new Dictionary<string, string?> { ["Team:Acp:HiddenModes:0"] = "auto" });

        Assert.False(policy.IsOffered("auto"));
        Assert.True(policy.IsOffered("bypassPermissions"));
    }

    /// <summary>A list of two hides both and nothing else.</summary>
    [Fact]
    public void IsOffered_ConfiguredListOfTwo_HidesExactlyThose()
    {
        WorkModePolicy policy = Policy(new Dictionary<string, string?>
        {
            ["Team:Acp:HiddenModes:0"] = "plan",
            ["Team:Acp:HiddenModes:1"] = "acceptEdits",
        });

        Assert.False(policy.IsOffered("plan"));
        Assert.False(policy.IsOffered("acceptEdits"));
        Assert.True(policy.IsOffered("default"));
        Assert.True(policy.IsOffered("auto"));
    }

    /// <summary>Ids are compared ordinally: a differently cased id is a different mode and is not hidden by the default.</summary>
    [Fact]
    public void IsOffered_IsOrdinal()
    {
        WorkModePolicy policy = Policy([]);

        Assert.True(policy.IsOffered("Auto"));
        Assert.True(policy.IsOffered("BYPASSPERMISSIONS"));
    }

    /// <summary>Filtering drops hidden modes, keeps the rest in wire order, and keeps <c>default</c> (Manual).</summary>
    [Fact]
    public void Filter_DropsHiddenModesAndKeepsOrder()
    {
        WorkModePolicy policy = Policy([]);
        IReadOnlyList<AgentModeOption> advertised =
        [
            new AgentModeOption("default", "Manual", "Always ask"),
            new AgentModeOption("acceptEdits", "Accept edits", null),
            new AgentModeOption("plan", "Plan", null),
            new AgentModeOption("auto", "Auto", null),
            new AgentModeOption("bypassPermissions", "Bypass permissions", null),
        ];

        IReadOnlyList<AgentModeOption> offered = policy.Filter(advertised);

        Assert.Equal(["default", "acceptEdits"], offered.Select(mode => mode.Id));
    }

    /// <summary>Filtering never mutates or aliases what it is given.</summary>
    [Fact]
    public void Filter_EmptyInput_ReturnsEmpty()
    {
        WorkModePolicy policy = Policy([]);

        IReadOnlyList<AgentModeOption> offered = policy.Filter([]);

        Assert.Empty(offered);
    }

    private static WorkModePolicy Policy(Dictionary<string, string?> values)
    {
        IConfigurationRoot configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        TeamOptions teamOptions = new();
        configuration.GetSection(TeamOptions.SectionName).Bind(teamOptions);
        return new WorkModePolicy(Options.Create(teamOptions));
    }
}
