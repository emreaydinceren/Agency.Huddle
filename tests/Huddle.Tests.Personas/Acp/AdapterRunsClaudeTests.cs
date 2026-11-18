using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.Tests.Factories;

namespace Agency.Huddle.Tests.Acp;

/// <summary>Which profiles <see cref="AdapterCatalog.LaunchesClaude"/> calls Claude: the ones the host would launch claude-agent-acp for.</summary>
public sealed class AdapterRunsClaudeTests
{
    /// <summary>The synthesised default profile (Command "node", no Args, no AdapterPath) launches the bundled adapter, so it launches Claude.</summary>
    [Fact]
    public void LaunchesClaude_StockDefaultProfile_IsTrue()
    {
        AdapterCatalog catalog = new(TestOptions.Create(opts => opts.Acp = new AcpOptions()));

        Assert.True(AdapterCatalog.LaunchesClaude(catalog.Default));
    }

    /// <summary>The stock install's default profile is offered the goldfish tools when the mode is on; a plain profile is not.</summary>
    [Fact]
    public void OffersGoldfish_DefaultProfileYesPlainProfileNo()
    {
        TeamOptions on = TestOptions.Create(opts => opts.Goldfish.Enabled = true).Value;
        AdapterCatalog stock = new(TestOptions.Create(opts => opts.Acp = new AcpOptions()));
        AdapterProfile plain = new AdapterCatalog(TestOptions.Create(opts => opts.Acp = new AcpOptions
        {
            Adapters = [new AdapterProfileOptions { Id = "bare", Command = "agency-acp" }],
        })).Default;

        Assert.Equal([true, false], [DotAcpAgentHostFactory.OffersGoldfish(on, stock.Default), DotAcpAgentHostFactory.OffersGoldfish(on, plain)]);
    }

    /// <summary>A profile that names its own Args without the Claude adapter, or runs another Command, does not launch Claude.</summary>
    [Fact]
    public void LaunchesClaude_PlainProfiles_AreFalse()
    {
        AcpOptions acp = new()
        {
            Adapters =
            [
                new AdapterProfileOptions { Id = "args", Command = "node", Args = ["plain-agent"] },
                new AdapterProfileOptions { Id = "bare", Command = "agency-acp" },
            ],
        };
        AdapterCatalog catalog = new(TestOptions.Create(opts => opts.Acp = acp));

        Assert.Equal([false, false], catalog.Profiles.Select(AdapterCatalog.LaunchesClaude));
    }
}
