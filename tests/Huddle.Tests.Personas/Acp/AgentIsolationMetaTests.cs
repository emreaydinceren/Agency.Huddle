using Agency.Huddle.App;
using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// The Agent session <c>_meta</c> built by <see cref="DotAcpAgentHostFactory.BuildIsolationMeta"/> keeps the Human's
/// claude.ai account connectors off unless told otherwise (Agent Connectors Spec sections 8.1 and 8.2, tests T-2 to T-6; T-12 and T-13 are in ConnectorApprovalTests).
/// </summary>
public sealed class AgentIsolationMetaTests
{
    private static readonly string[] SettingSources = ["project", "local"];

    /// <summary>An Adapter profile with the given isolation flag.</summary>
    /// <param name="isolate">Whether the Adapter isolates user settings.</param>
    private static AdapterProfile Profile(bool isolate) =>
        new("claude", "Claude", null, "node", null, null, true, null, true, isolate, true, null, true);

    /// <summary>Pulls the nested <c>claudeCode.options</c> dictionary out of the meta.</summary>
    /// <param name="meta">The meta.</param>
    private static Dictionary<string, object> Options(Dictionary<string, object>? meta) =>
        (Dictionary<string, object>)((Dictionary<string, object>)Assert.IsType<Dictionary<string, object>>(meta)["claudeCode"])["options"];

    /// <summary>T-2: with connectors off the options carry exactly the one env key, and the isolation keys are unchanged.</summary>
    [Fact]
    public void BuildIsolationMeta_ConnectorsOff_CarriesTheOneEnvKey()
    {
        Dictionary<string, object> options = Options(DotAcpAgentHostFactory.BuildIsolationMeta(Profile(isolate: true), connectors: false));

        Dictionary<string, object> env = Assert.IsType<Dictionary<string, object>>(options["env"]);
        KeyValuePair<string, object> entry = Assert.Single(env);
        Assert.Equal("ENABLE_CLAUDEAI_MCP_SERVERS", entry.Key);
        Assert.Equal("false", entry.Value);
        Assert.Equal(SettingSources, Assert.IsType<string[]>(options["settingSources"]));
        Assert.False((bool)Assert.IsType<Dictionary<string, object>>(options["settings"])["autoMemoryEnabled"]);
        Assert.Equal(3, options.Count);
    }

    /// <summary>T-3: with connectors on the options carry no env, so the account connectors load as before.</summary>
    [Fact]
    public void BuildIsolationMeta_ConnectorsOn_CarriesNoEnv()
    {
        Dictionary<string, object> options = Options(DotAcpAgentHostFactory.BuildIsolationMeta(Profile(isolate: true), connectors: true));

        Assert.False(options.ContainsKey("env"));
        Assert.Equal(SettingSources, Assert.IsType<string[]>(options["settingSources"]));
        Assert.Equal(2, options.Count);
    }

    /// <summary>
    /// Spec 8.4: with connectors on, <c>settings</c> carries Huddle's own <c>permissions.ask</c> rule for every claude.ai tool beside
    /// <c>autoMemoryEnabled</c> (the option replaces the Adapter's settings), and with connectors off it carries only
    /// <c>autoMemoryEnabled</c>, exactly as T-2 pins.
    /// </summary>
    [Fact]
    public void BuildIsolationMeta_ConnectorsOn_AddsTheAskRule_AndOffDoesNot()
    {
        Dictionary<string, object> on = Assert.IsType<Dictionary<string, object>>(
            Options(DotAcpAgentHostFactory.BuildIsolationMeta(Profile(isolate: true), connectors: true))["settings"]);
        Dictionary<string, object> off = Assert.IsType<Dictionary<string, object>>(
            Options(DotAcpAgentHostFactory.BuildIsolationMeta(Profile(isolate: true), connectors: false))["settings"]);

        Assert.Equal(2, on.Count);
        Assert.False((bool)on["autoMemoryEnabled"]);
        string[] onList = AskList(on);
        Assert.Equal("mcp__claude_ai_*", onList[^1]);
        Assert.Equal(ExpectedAgentRules, onList[..^1]);
        Assert.Equal(2, off.Count);
        Assert.False((bool)off["autoMemoryEnabled"]);
        Assert.Equal(ExpectedAgentRules, AskList(off));
    }

    /// <summary>The rules every Agent session carries, whatever the connectors setting (issue #432).</summary>
    private static readonly string[] ExpectedAgentRules =
    [
        "Bash(*git*merge*)", "Bash(*git*push*)", "Bash(*git*pull*)",
        "PowerShell(*git*merge*)", "PowerShell(*git*push*)", "PowerShell(*git*pull*)",
        "Edit", "Write",
    ];

    /// <summary>Reads <c>permissions.ask</c> out of a settings dictionary, requiring it to be the only permissions key.</summary>
    /// <param name="settings">The <c>options.settings</c> dictionary.</param>
    private static string[] AskList(Dictionary<string, object> settings)
    {
        Dictionary<string, object> permissions = Assert.IsType<Dictionary<string, object>>(settings["permissions"]);
        KeyValuePair<string, object> rule = Assert.Single(permissions);
        Assert.Equal("ask", rule.Key);
        return Assert.IsType<string[]>(rule.Value);
    }

    /// <summary>
    /// Issue #432: an Agent's own list never holds an allow rule or a hook, and a Helper's meta carries none of it: it has no tools and its
    /// <c>settings</c> stay <c>autoMemoryEnabled</c> alone.
    /// </summary>
    [Fact]
    public void HelperSessionMeta_Isolating_SettingsHoldOnlyAutoMemory()
    {
        IReadOnlyDictionary<string, object>? meta = Agency.Huddle.App.Helpers.HelperSessionMeta.For(Profile(isolate: true));

        Dictionary<string, object> options = Options(Assert.IsType<Dictionary<string, object>>(meta));
        KeyValuePair<string, object> only = Assert.Single(Assert.IsType<Dictionary<string, object>>(options["settings"]));
        Assert.Equal("autoMemoryEnabled", only.Key);
    }

    private static AdapterProfile NonIsolatingClaude() =>
        new("custom", "Custom Claude", null, "claude-agent-acp", null, null, true, null, true, false, true, null, true);

    /// <summary>
    /// F4: a custom profile that runs Claude but does not isolate still gets the connectors switched off when the setting is off: the
    /// <c>_meta</c> carries <c>options.env</c> and nothing else, so no isolation key is added to a profile that did not ask for it.
    /// </summary>
    [Fact]
    public void BuildIsolationMeta_ClaudeProfileNotIsolating_ConnectorsOff_CarriesOnlyTheEnvKey()
    {
        Dictionary<string, object> options = Options(DotAcpAgentHostFactory.BuildIsolationMeta(NonIsolatingClaude(), connectors: false));

        Dictionary<string, object> env = Assert.IsType<Dictionary<string, object>>(Assert.Single(options).Value);
        Assert.Equal("false", env["ENABLE_CLAUDEAI_MCP_SERVERS"]);
        Assert.Single(env);
        Assert.False(options.ContainsKey("settingSources"));
        Assert.False(options.ContainsKey("settings"));
    }

    /// <summary>F4: the same profile with the setting on, and a profile that does not run Claude on either setting, still get no meta.</summary>
    /// <param name="connectors">The connectors setting.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BuildIsolationMeta_NoMetaNeeded_ReturnsNull(bool connectors)
    {
        Assert.Null(DotAcpAgentHostFactory.BuildIsolationMeta(Profile(isolate: false), connectors: connectors));
        if (connectors)
        {
            Assert.Null(DotAcpAgentHostFactory.BuildIsolationMeta(NonIsolatingClaude(), connectors: true));
        }
    }

    /// <summary>F4: a stored "on" is ignored on a profile that does not isolate, and honoured on one that does.</summary>
    [Fact]
    public void ConnectorsPassed_StoredOn_IsIgnoredWithoutIsolation()
    {
        Assert.False(DotAcpAgentHostFactory.ConnectorsPassed(NonIsolatingClaude(), effective: true));
        Assert.True(DotAcpAgentHostFactory.ConnectorsPassed(Profile(isolate: true), effective: true));
        Assert.False(DotAcpAgentHostFactory.ConnectorsPassed(Profile(isolate: true), effective: false));
    }

    /// <summary>F4: a Helper on a non-isolating profile still gets no meta: it keeps its old behaviour, and its handler refuses every request.</summary>
    [Fact]
    public void HelperSessionMeta_ClaudeProfileNotIsolating_StaysNull() =>
        Assert.Null(Agency.Huddle.App.Helpers.HelperSessionMeta.For(NonIsolatingClaude()));

    /// <summary>T-4: an Adapter that does not isolate gets no meta whichever way the setting is (Spec 3.6).</summary>
    /// <param name="connectors">The connectors setting.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BuildIsolationMeta_ProfileNotIsolating_ReturnsNull_ForEitherSetting(bool connectors) =>
        Assert.Null(DotAcpAgentHostFactory.BuildIsolationMeta(Profile(isolate: false), connectors: connectors));

    /// <summary>T-5: an Agent's meta never carries <c>extraArgs</c> (no strict-mcp-config, Spec 3.3) or <c>tools</c>, whatever the setting.</summary>
    /// <param name="connectors">The connectors setting.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BuildIsolationMeta_Agent_HasNoExtraArgsAndNoTools(bool connectors)
    {
        Dictionary<string, object> options = Options(DotAcpAgentHostFactory.BuildIsolationMeta(Profile(isolate: true), connectors: connectors));

        Assert.False(options.ContainsKey("extraArgs"));
        Assert.False(options.ContainsKey("tools"));
    }

    /// <summary>
    /// T-6: rules.md carries the Agent row, and the meta it describes holds the env key. A build of an Agent meta without
    /// the key fails here, and so does deleting the row.
    /// </summary>
    [Fact]
    public void RulesMd_AgentRow_IsQuoted_AndTheMetaHonoursIt()
    {
        string[] rows = File.ReadAllLines(RepoPaths.Combine("docs", "engineering", "rules.md"));
        string row = Assert.Single(rows, line => line.StartsWith("| **An Agent session starts with the account connectors off.**", StringComparison.Ordinal));
        Assert.Contains("ENABLE_CLAUDEAI_MCP_SERVERS=false", row, StringComparison.Ordinal); // contains-ok: quoting a rules.md row
        Assert.Contains("BuildIsolationMeta", row, StringComparison.Ordinal); // contains-ok: quoting a rules.md row
        Assert.Contains("Bash(*git*merge*)", row, StringComparison.Ordinal); // contains-ok: quoting a rules.md row
        Assert.Contains("AgentSettingsGuard", row, StringComparison.Ordinal); // contains-ok: quoting a rules.md row

        Dictionary<string, object> env = Assert.IsType<Dictionary<string, object>>(
            Options(DotAcpAgentHostFactory.BuildIsolationMeta(Profile(isolate: true), connectors: false))["env"]);
        Assert.Equal("false", env["ENABLE_CLAUDEAI_MCP_SERVERS"]);
    }

    private static AdapterProfile IsolatingClaude() =>
        new("claude", "Claude", null, "claude-agent-acp", null, null, true, null, true, true, true, null, true);

    private static BrowserLaunchFacts Facts(bool enabled = true, string? cli = "cli.js", string? channel = "msedge") =>
        new(enabled, cli, channel, false, false, 30);

    private static WorkModePolicy Policy(params string[] hidden) => new(Microsoft.Extensions.Options.Options.Create(new TeamOptions { Acp= { HiddenModes = hidden } }));

    private static Dictionary<string, object> Entry() =>
        BrowserServerConfig.Build(Facts(), "node", Path.Combine("data", "browser-profiles", "nova"), Path.Combine("data", "work", "browser-output"))
        ?? throw new InvalidOperationException("The entry was not built.");

    /// <summary>The settings of an isolating Claude meta built with the given server and connectors setting.</summary>
    private static Dictionary<string, object> Settings(Dictionary<string, object> options) =>
        Assert.IsType<Dictionary<string, object>>(options["settings"]);

    /// <summary>
    /// #551: a browser entry adds <c>options.mcpServers.playwright</c> exactly as given, the <c>mcp__playwright__*</c> ask rule after the existing
    /// ones (connectors first) and a deny rule that holds <c>browser_run_code_unsafe</c> alone, all in the one <c>permissions</c> key.
    /// </summary>
    [Fact]
    public void Browser_On_AddsServerAndAskRule()
    {
        Dictionary<string, object> entry = Entry();

        Dictionary<string, object> options = Options(DotAcpAgentHostFactory.BuildIsolationMeta(IsolatingClaude(), connectors: true, browserServer: entry));

        Dictionary<string, object> servers = Assert.IsType<Dictionary<string, object>>(options["mcpServers"]);
        KeyValuePair<string, object> server = Assert.Single(servers);
        Assert.Equal("playwright", server.Key);
        Assert.Same(entry, server.Value);
        Dictionary<string, object> permissions = Assert.IsType<Dictionary<string, object>>(Settings(options)["permissions"]);
        Assert.Equal(["ask", "deny"], permissions.Keys.Order(StringComparer.Ordinal));
        string[] ask = Assert.IsType<string[]>(permissions["ask"]);
        Assert.Equal([.. ExpectedAgentRules, "mcp__claude_ai_*", "mcp__playwright__*"], ask);
        Assert.Equal(["mcp__playwright__browser_run_code_unsafe"], Assert.IsType<string[]>(permissions["deny"]));
        Assert.False(options.ContainsKey("extraArgs"));
        Assert.False(options.ContainsKey("tools"));
    }

    /// <summary>#551: with no entry the meta is the one it was before the browser: no server, no deny rule, the same ask list, for either connectors setting.</summary>
    /// <param name="connectors">The connectors setting.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Browser_Off_NoServerNoRule(bool connectors)
    {
        Dictionary<string, object> options = Options(DotAcpAgentHostFactory.BuildIsolationMeta(IsolatingClaude(), connectors, browserServer: null));

        Assert.False(options.ContainsKey("mcpServers"));
        Dictionary<string, object> permissions = Assert.IsType<Dictionary<string, object>>(Settings(options)["permissions"]);
        Assert.False(permissions.ContainsKey("deny"));
        string[] expected = connectors ? [.. ExpectedAgentRules, "mcp__claude_ai_*"] : ExpectedAgentRules;
        Assert.Equal(expected, Assert.IsType<string[]>(permissions["ask"]));
        Assert.Equal(
            System.Text.Json.JsonSerializer.Serialize(DotAcpAgentHostFactory.BuildIsolationMeta(IsolatingClaude(), connectors)),
            System.Text.Json.JsonSerializer.Serialize(DotAcpAgentHostFactory.BuildIsolationMeta(IsolatingClaude(), connectors, browserServer: null)));
    }

    /// <summary>#551: an entry never reaches a meta without ask rules (a Helper) or one for a profile that does not isolate: both stay as they were.</summary>
    [Fact]
    public void Browser_WithoutAskRulesOrIsolation_AddsNothing()
    {
        Dictionary<string, object> entry = Entry();

        Dictionary<string, object> noAskRules = Options(DotAcpAgentHostFactory.BuildIsolationMeta(IsolatingClaude(), connectors: false, askRules: false, browserServer: entry));

        Assert.False(noAskRules.ContainsKey("mcpServers"));
        Assert.False(Settings(noAskRules).ContainsKey("permissions"));
        Assert.Null(DotAcpAgentHostFactory.BuildIsolationMeta(Profile(isolate: false), connectors: true, browserServer: entry));
        Dictionary<string, object> nonIsolating = Options(DotAcpAgentHostFactory.BuildIsolationMeta(NonIsolatingClaude(), connectors: false, browserServer: entry));
        Assert.False(nonIsolating.ContainsKey("mcpServers"));
        Assert.False(nonIsolating.ContainsKey("settings"));
    }

    /// <summary>#551: Bypass and Auto skip the approval prompt every browser call needs, so the browser is not offered there; Manual, Accept edits and Plan keep it, and the switch off wins.</summary>
    [Fact]
    public void Browser_ModeSkipsApproval_NoServer()
    {
        WorkModePolicy policy = Policy("plan");

        Assert.False(DotAcpAgentHostFactory.EffectiveBrowser(new Persona("nova", "x", WorkMode: "bypassPermissions"), policy));
        Assert.False(DotAcpAgentHostFactory.EffectiveBrowser(new Persona("nova", "x", WorkMode: "auto"), policy));
        Assert.True(DotAcpAgentHostFactory.EffectiveBrowser(new Persona("nova", "x", WorkMode: "default"), policy));
        Assert.True(DotAcpAgentHostFactory.EffectiveBrowser(new Persona("nova", "x", WorkMode: "acceptEdits"), policy));
        Assert.True(DotAcpAgentHostFactory.EffectiveBrowser(new Persona("nova", "x"), policy));
        Assert.False(DotAcpAgentHostFactory.EffectiveBrowser(new Persona("nova", "x", BrowserOff: true), policy));
        Assert.False(DotAcpAgentHostFactory.BrowserPassed(IsolatingClaude(), effective: false, Facts()));
    }

    /// <summary>#551: a profile that does not isolate has no <c>settings</c> to carry the rules, so it never gets the browser.</summary>
    [Fact]
    public void Browser_NonIsolatingProfile_NoServer()
    {
        Assert.False(DotAcpAgentHostFactory.BrowserPassed(NonIsolatingClaude(), effective: true, Facts()));
        Assert.True(DotAcpAgentHostFactory.BrowserPassed(IsolatingClaude(), effective: true, Facts()));
    }

    /// <summary>#551: an isolating profile that does not run Claude (the permission rule forms are Claude's) never gets the browser.</summary>
    [Fact]
    public void Browser_NotClaude_NoServer() =>
        Assert.False(DotAcpAgentHostFactory.BrowserPassed(Profile(isolate: true), effective: true, Facts()));

    /// <summary>#551: with no Playwright CLI, or no browser found, the facts are unavailable and nothing is passed.</summary>
    [Fact]
    public void Browser_CliMissing_NoServer()
    {
        Assert.False(DotAcpAgentHostFactory.BrowserPassed(IsolatingClaude(), effective: true, Facts(cli: null)));
        Assert.False(DotAcpAgentHostFactory.BrowserPassed(IsolatingClaude(), effective: true, Facts(channel: null)));
    }

    /// <summary>#551: <c>Team:Browser:Enabled</c> false switches the browser off for every Agent.</summary>
    [Fact]
    public void Browser_KillSwitchOff_NoServer() =>
        Assert.False(DotAcpAgentHostFactory.BrowserPassed(IsolatingClaude(), effective: true, Facts(enabled: false)));

    /// <summary>#551: <see cref="DotAcpAgentHostFactory.AskRules"/> adds exactly the browser rule, last, and nothing else, so the connector rule keeps its place.</summary>
    [Fact]
    public void AskRules_BrowserRule_IsLastAndAloneAdded()
    {
        Assert.Equal(ExpectedAgentRules, DotAcpAgentHostFactory.AskRules(connectors: false, browser: false));
        Assert.Equal([.. ExpectedAgentRules, "mcp__playwright__*"], DotAcpAgentHostFactory.AskRules(connectors: false, browser: true));
        Assert.Equal([.. ExpectedAgentRules, "mcp__claude_ai_*", "mcp__playwright__*"], DotAcpAgentHostFactory.AskRules(connectors: true, browser: true));
    }

    /// <summary>#551: the profile folder is under <c>browser-profiles</c> beside the Agent folders, never inside one.</summary>
    [Fact]
    public void BrowserProfile_IsUnderBrowserProfilesRoot()
    {
        AgentPaths paths = new(Microsoft.Extensions.Options.Options.Create(new TeamOptions { DataDir = Path.Combine("data") }));

        Assert.Equal(Path.Combine("data", "browser-profiles", "Nova"), paths.BrowserProfile("Nova"));
        Assert.Equal(Path.Combine("data", "browser-profiles"), paths.BrowserProfilesRoot);
        Assert.False(paths.BrowserProfile("Nova").StartsWith(paths.DefinitionsRoot, StringComparison.Ordinal));
    }
}
