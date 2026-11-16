using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Helpers;

namespace Agency.Huddle.Tests.Helpers;

/// <summary>
/// <see cref="HelperSessionMeta"/> switches off built-in tools, claude.ai connectors and extra MCP servers for a Helper
/// session on an Adapter that isolates user settings (Helpers Spec §6.2, OD-3, plan HL-0).
/// </summary>
public sealed class HelperSessionMetaTests
{
    private static readonly string[] SettingSources = ["project", "local"];

    /// <summary>An Adapter profile with the given isolation flag.</summary>
    /// <param name="isolate">Whether the Adapter isolates user settings.</param>
    private static AdapterProfile Profile(bool isolate) =>
        new("claude", "Claude", null, "node", null, null, true, null, true, isolate, true, null, true);

    /// <summary>Pulls the nested <c>claudeCode.options</c> dictionary out of the meta.</summary>
    /// <param name="meta">The meta.</param>
    private static Dictionary<string, object> Options(IReadOnlyDictionary<string, object> meta) =>
        (Dictionary<string, object>)((Dictionary<string, object>)meta["claudeCode"])["options"];

    /// <summary>The meta carries the three settings the spike found, on top of the usual isolation.</summary>
    [Fact]
    public void For_IsolatingAdapter_TurnsOffTools_Connectors_AndExtraMcpServers()
    {
        IReadOnlyDictionary<string, object>? meta = HelperSessionMeta.For(Profile(isolate: true));

        Assert.NotNull(meta);
        Dictionary<string, object> options = Options(meta);
        Assert.Empty(Assert.IsType<string[]>(options["tools"]));
        Assert.Equal("false", Assert.IsType<Dictionary<string, object>>(options["env"])["ENABLE_CLAUDEAI_MCP_SERVERS"]);
        Dictionary<string, object?> extraArgs = Assert.IsType<Dictionary<string, object?>>(options["extraArgs"]);
        Assert.True(extraArgs.ContainsKey("strict-mcp-config"));
        Assert.Null(extraArgs["strict-mcp-config"]);
        Assert.Equal(SettingSources, Assert.IsType<string[]>(options["settingSources"]));
    }

    /// <summary>An Adapter that does not isolate user settings gets no meta: the keys are Claude-adapter specific.</summary>
    [Fact]
    public void For_NonIsolatingAdapter_IsNull() =>
        Assert.Null(HelperSessionMeta.For(Profile(isolate: false)));

    /// <summary>Each call builds a fresh dictionary, so one Helper's meta cannot be changed through another's.</summary>
    [Fact]
    public void For_BuildsAFreshDictionaryEachTime()
    {
        IReadOnlyDictionary<string, object>? first = HelperSessionMeta.For(Profile(isolate: true));
        IReadOnlyDictionary<string, object>? second = HelperSessionMeta.For(Profile(isolate: true));

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotSame(first, second);
        Assert.NotSame(Options(first), Options(second));
    }

    /// <summary>#551: a Helper has no tools and never a browser, even on a profile that runs Claude: no <c>mcpServers</c>, no <c>permissions</c>, no mention of Playwright.</summary>
    [Fact]
    public void Helper_NeverGetsBrowser()
    {
        AdapterProfile claude = new("claude", "Claude", null, "claude-agent-acp", null, null, true, null, true, true, true, null, true);

        IReadOnlyDictionary<string, object>? meta = HelperSessionMeta.For(claude);

        Assert.NotNull(meta);
        Dictionary<string, object> options = Options(meta);
        Assert.False(options.ContainsKey("mcpServers"));
        Assert.False(((Dictionary<string, object>)options["settings"]).ContainsKey("permissions"));
        Assert.DoesNotContain("playwright", System.Text.Json.JsonSerializer.Serialize(meta), StringComparison.OrdinalIgnoreCase);
    }
}
