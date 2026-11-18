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

    /// <summary>The stock profile can open a clean session (it is offered the tool, #627 B9); an explicit Claude profile can; a plain or non-isolating one cannot.</summary>
    [Fact]
    public void CanOpenClean_StockAndClaudeYes_PlainAndNonIsolatingNo()
    {
        AcpOptions acp = new()
        {
            Adapters =
            [
                new AdapterProfileOptions { Id = "claude", Command = "node", Args = ["claude-agent-acp"], IsolateUserSettings = true },
                new AdapterProfileOptions { Id = "open", Command = "node", Args = ["claude-agent-acp"], IsolateUserSettings = false },
                new AdapterProfileOptions { Id = "bare", Command = "agency-acp", IsolateUserSettings = true },
            ],
        };
        AdapterCatalog stock = new(TestOptions.Create(opts => opts.Acp = new AcpOptions()));
        AdapterCatalog catalog = new(TestOptions.Create(opts => opts.Acp = acp));

        Assert.Equal(
            [true, true, false, false],
            [DotAcpPersonaHost.CanOpenClean(stock.Default), .. catalog.Profiles.Select(DotAcpPersonaHost.CanOpenClean)]);
    }

    /// <summary>The clean meta on the stock profile carries what an explicit Claude profile's does: connectors off, the ask rules and the Read deny list; the ordinary meta has no deny list.</summary>
    [Fact]
    public void BuildIsolationMeta_StockProfile_CleanCarriesTheDenyListOrdinaryDoesNot()
    {
        AdapterProfile stock = new AdapterCatalog(TestOptions.Create(opts => opts.Acp = new AcpOptions())).Default;
        string[] deny = ["Read(//data/memory/**)"];

        string clean = System.Text.Json.JsonSerializer.Serialize(
            DotAcpAgentHostFactory.BuildIsolationMeta(stock, connectors: false, askRules: true, browserServer: null, readDeny: deny));
        string ordinary = System.Text.Json.JsonSerializer.Serialize(DotAcpAgentHostFactory.BuildIsolationMeta(stock, connectors: true));

        Assert.Equal(
            [true, true, true, false, false],
            [clean.Contains("\"deny\":[\"Read(//data/memory/**)\"]", StringComparison.Ordinal), clean.Contains("\"ask\":", StringComparison.Ordinal), clean.Contains("ENABLE_CLAUDEAI_MCP_SERVERS", StringComparison.Ordinal), ordinary.Contains("\"deny\"", StringComparison.Ordinal), ordinary.Contains("ENABLE_CLAUDEAI_MCP_SERVERS", StringComparison.Ordinal)]); // contains-ok: the serialised meta is the wire text; the keys are what is pinned
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
