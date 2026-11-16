using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Acp;

/// <summary>Which Playwright tool calls ask the Human (issue #551, decision 4): the reads run, everything else asks.</summary>
public sealed class BrowserCallPolicyTests
{
    private const string Prefix = "mcp__playwright__";

    private static ToolCallInfo Call(string? toolName, string? raw = "{}", string? server = "playwright", string? title = null) =>
        new("call-1", title ?? toolName, ToolKind.Other, ToolCallStatus.Pending, raw, toolName, server, "dynamic");

    /// <summary>The exact set of tools that run without asking is pinned, so a version bump that adds a tool fails until it is classified.</summary>
    [Fact]
    public void ReadTools_Pinned()
    {
        string[] expected =
        [
            "browser_snapshot", "browser_find", "browser_take_screenshot", "browser_console_messages", "browser_network_requests",
            "browser_network_request", "browser_navigate", "browser_navigate_back", "browser_tabs", "browser_wait_for", "browser_resize",
            "browser_close",
        ];

        Assert.Equal(expected.Order(StringComparer.Ordinal), BrowserCallPolicy.ReadToolNames.Order(StringComparer.Ordinal));
        Assert.Equal(13, BrowserCallPolicy.ActingTools.Count);
        Assert.Empty(BrowserCallPolicy.ActingTools.Intersect(BrowserCallPolicy.ReadToolNames, StringComparer.Ordinal));
    }

    /// <summary>Each of the 13 acting tools asks, whatever its arguments.</summary>
    /// <param name="tool">The bare tool name.</param>
    [Theory]
    [InlineData("browser_click")]
    [InlineData("browser_type")]
    [InlineData("browser_fill_form")]
    [InlineData("browser_select_option")]
    [InlineData("browser_press_key")]
    [InlineData("browser_hover")]
    [InlineData("browser_drag")]
    [InlineData("browser_drop")]
    [InlineData("browser_file_upload")]
    [InlineData("browser_handle_dialog")]
    [InlineData("browser_evaluate")]
    [InlineData("browser_emulate_media")]
    [InlineData("browser_run_code_unsafe")]
    public void ActingTools_Pinned_Ask(string tool)
    {
        Assert.Contains(tool, BrowserCallPolicy.ActingTools);
        Assert.True(BrowserCallPolicy.AsksHuman(Call(Prefix + tool, """{"element":"Buy","ref":"e5"}""")));
    }

    /// <summary>A Playwright tool this code has never heard of asks (fail safe).</summary>
    [Fact]
    public void UnknownPlaywrightTool_Asks() =>
        Assert.True(BrowserCallPolicy.AsksHuman(Call(Prefix + "browser_new_thing")));

    /// <summary>The read tools run with ordinary arguments.</summary>
    /// <param name="tool">The bare tool name.</param>
    [Theory]
    [InlineData("browser_snapshot")]
    [InlineData("browser_find")]
    [InlineData("browser_console_messages")]
    [InlineData("browser_network_requests")]
    [InlineData("browser_navigate_back")]
    [InlineData("browser_wait_for")]
    [InlineData("browser_resize")]
    [InlineData("browser_close")]
    public void ReadTool_PlainArguments_DoesNotAsk(string tool) =>
        Assert.False(BrowserCallPolicy.AsksHuman(Call(Prefix + tool, """{"time":1}""")));

    /// <summary>A read tool given a filename writes a file into the Work Dir unseen, so it asks.</summary>
    [Fact]
    public void ReadToolWithFilename_Asks()
    {
        Assert.True(BrowserCallPolicy.AsksHuman(Call(Prefix + "browser_take_screenshot", """{"filename":"../x.png"}""")));
        Assert.False(BrowserCallPolicy.AsksHuman(Call(Prefix + "browser_take_screenshot", "{}")));
    }

    /// <summary>A navigation to anything but an http or https address asks.</summary>
    /// <param name="url">The address.</param>
    [Theory]
    [InlineData("file:///C:/secret.txt")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,hi")]
    [InlineData("/relative/path")]
    [InlineData("example.com")]
    public void NavigateNonHttp_Asks(string url) =>
        Assert.True(BrowserCallPolicy.AsksHuman(Call(Prefix + "browser_navigate", $$"""{"url":"{{url}}"}""")));

    /// <summary>A navigation to an https or http address runs; a navigation with no url asks.</summary>
    [Fact]
    public void NavigateHttps_DoesNotAsk()
    {
        Assert.False(BrowserCallPolicy.AsksHuman(Call(Prefix + "browser_navigate", """{"url":"https://example.com/a?b=1"}""")));
        Assert.False(BrowserCallPolicy.AsksHuman(Call(Prefix + "browser_navigate", """{"url":"http://localhost:5000"}""")));
        Assert.True(BrowserCallPolicy.AsksHuman(Call(Prefix + "browser_navigate", "{}")));
    }

    /// <summary>A tabs call that carries a non-http url asks; one without a url, or with an https one, does not.</summary>
    [Fact]
    public void TabsWithUrl_NonHttp_Asks()
    {
        Assert.True(BrowserCallPolicy.AsksHuman(Call(Prefix + "browser_tabs", """{"action":"new","url":"file:///x"}""")));
        Assert.False(BrowserCallPolicy.AsksHuman(Call(Prefix + "browser_tabs", """{"action":"list"}""")));
        Assert.False(BrowserCallPolicy.AsksHuman(Call(Prefix + "browser_tabs", """{"action":"new","url":"https://example.com"}""")));
    }

    /// <summary>Arguments that are not a JSON object ask, even for a read tool.</summary>
    /// <param name="raw">The raw arguments.</param>
    [Theory]
    [InlineData("not json")]
    [InlineData("[1]")]
    [InlineData("")]
    public void UnparseableArgs_Asks(string raw) =>
        Assert.True(BrowserCallPolicy.AsksHuman(Call(Prefix + "browser_snapshot", raw)));

    /// <summary>Missing arguments ask for a navigation.</summary>
    [Fact]
    public void NullArgs_ForNavigate_Asks() =>
        Assert.True(BrowserCallPolicy.AsksHuman(Call(Prefix + "browser_navigate", null)));

    /// <summary>Calls of other servers are not the browser's concern.</summary>
    [Fact]
    public void OtherServer_NotConcerned()
    {
        Assert.False(BrowserCallPolicy.AsksHuman(Call("mcp__team__x", "{}", "team")));
        Assert.False(BrowserCallPolicy.AsksHuman(Call("Bash", "{}", null)));
        Assert.False(BrowserCallPolicy.AsksHuman(Call("mcp__other__browser_click", "{}", "other")));
    }

    /// <summary>A call known only by its server name is the browser's, and an unplaceable title asks.</summary>
    [Fact]
    public void ServerNameOnly_Detected()
    {
        Assert.True(BrowserCallPolicy.AsksHuman(Call(null, "{}", "playwright", "Something")));
        Assert.True(BrowserCallPolicy.AsksHuman(Call(null, "{}", "PLAYWRIGHT", "browser_snapshot")));
        Assert.False(BrowserCallPolicy.AsksHuman(Call(null, "{}", "playwright", Prefix + "browser_snapshot")));
    }

    /// <summary>A call that carries only a Playwright title, with no tool or server name, is still a browser call and asks.</summary>
    [Fact]
    public void TitleOnly_Detected()
    {
        Assert.True(BrowserCallPolicy.Concerns(Call(null, "{}", null, Prefix + "browser_click")));
        Assert.True(BrowserCallPolicy.AsksHuman(Call(null, "{}", null, Prefix + "browser_click")));
    }

    /// <summary>The prefix is matched ignoring case.</summary>
    [Fact]
    public void CaseInsensitivePrefix_Detected()
    {
        Assert.True(BrowserCallPolicy.AsksHuman(Call("MCP__PLAYWRIGHT__browser_click", "{}", null)));
        Assert.False(BrowserCallPolicy.AsksHuman(Call("MCP__Playwright__browser_snapshot", "{}", null)));
    }
}
