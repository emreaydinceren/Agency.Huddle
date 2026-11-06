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
        Dictionary<string, object> permissions = Assert.IsType<Dictionary<string, object>>(on["permissions"]);
        KeyValuePair<string, object> rule = Assert.Single(permissions);
        Assert.Equal("ask", rule.Key);
        Assert.Equal(["mcp__claude_ai_*"], Assert.IsType<string[]>(rule.Value));
        KeyValuePair<string, object> only = Assert.Single(off);
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

        Dictionary<string, object> env = Assert.IsType<Dictionary<string, object>>(
            Options(DotAcpAgentHostFactory.BuildIsolationMeta(Profile(isolate: true), connectors: false))["env"]);
        Assert.Equal("false", env["ENABLE_CLAUDEAI_MCP_SERVERS"]);
    }
}
