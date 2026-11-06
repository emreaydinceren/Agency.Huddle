using Agency.Huddle.App.Acp;
using Agency.Huddle.Acp.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// The Agent session <c>_meta</c> built by <see cref="DotAcpAgentHostFactory.BuildIsolationMeta"/> keeps the Human's
/// claude.ai account connectors off unless told otherwise (Agent Connectors Spec sections 8.1 and 8.2, tests T-2 to T-6, T-12).
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

    /// <summary>
    /// T-12, a characterization of today: a claude.ai connector call is approved with no Human asked. Decision OQ-1
    /// replaces this test when the approval prompt is built; it is meant to fail then.
    /// </summary>
    [Fact]
    public async Task CurrentBehaviour_ConnectorToolCall_IsApprovedWithNoHumanAsked()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        IReadOnlyList<PermissionOptionInfo> options =
        [
            new PermissionOptionInfo("allow-1", "Allow once", PermissionOptionKind.AllowOnce),
            new PermissionOptionInfo("reject-1", "Reject once", PermissionOptionKind.RejectOnce),
        ];
        PermissionRequestContext request = new(
            "session-1",
            new ToolCallInfo("call-1", "mcp__claude_ai_ms365__outlook_send_mail", ToolKind.Other, ToolCallStatus.Pending, """{"to":"a@example.com"}"""),
            options);
        WorkDirPermissionHandler handler = new(Path.Combine(Path.GetTempPath(), "huddle-protected"), NullLogger<WorkDirPermissionHandler>.Instance);

        PermissionDecision decision = await handler.DecideAsync(request, ct);

        Assert.Equal("allow-1", Assert.IsType<SelectedDecision>(decision).OptionId);
    }
}
