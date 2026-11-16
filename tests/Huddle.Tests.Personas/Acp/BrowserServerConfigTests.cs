using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Acp;

/// <summary>Tests for <see cref="BrowserServerConfig"/>.</summary>
public sealed class BrowserServerConfigTests
{
    private static readonly string[] ExpectedEnvironmentKeys =
    [
        "ALLOWED_HOSTS", "ALLOWED_ORIGINS", "ALLOW_UNRESTRICTED_FILE_ACCESS", "BLOCKED_ORIGINS",
        "BLOCK_SERVICE_WORKERS", "BROWSER", "CAPS", "CDP_ENDPOINT", "CDP_HEADERS", "CDP_TIMEOUT", "CODEGEN",
        "CONFIG", "CONSOLE_LEVEL", "DEVICE", "EXECUTABLE_PATH", "EXTENSION", "FILE_PATHS", "GRANT_PERMISSIONS",
        "HEADLESS", "HOST", "IGNORE_HTTPS_ERRORS", "IGNORE_ROOTS", "INIT_PAGE", "INIT_SCRIPT", "ISOLATED",
        "IMAGE_RESPONSES", "MOBILE", "SANDBOX", "OUTPUT_DIR", "OUTPUT_MAX_SIZE", "PORT", "PROXY_BYPASS",
        "PROXY_SERVER", "REDACT_URL_PARAMS", "REMOTE_HEADERS", "SECRETS_FILE", "STORAGE_STATE",
        "TEST_ID_ATTRIBUTE", "TIMEOUT_ACTION", "IDLE_TIMEOUT", "TIMEOUT_NAVIGATION", "TIMEOUT_SETTLE",
        "USER_AGENT", "WEBMCP", "USER_DATA_DIR", "VIEWPORT_SIZE",
    ];

    private static BrowserLaunchFacts Facts(bool headless = false, int idleMinutes = 30) =>
        new(true, "/cli.js", "msedge", headless, false, idleMinutes);

    private static Dictionary<string, object> Build(BrowserLaunchFacts facts)
    {
        Dictionary<string, object>? entry = BrowserServerConfig.Build(facts, "node", "/profile", "/out");
        Assert.NotNull(entry);
        return entry;
    }

    /// <summary>The arguments are exactly the pinned list, in order, with no extras.</summary>
    [Fact]
    public void Build_Available_PinsTheWholeArgumentList()
    {
        Dictionary<string, object> entry = Build(Facts());

        Assert.Equal(
            [
                "/cli.js", "--browser", "msedge", "--user-data-dir", "/profile", "--output-dir", "/out",
                "--idle-timeout", "1800000", "--no-webmcp",
            ],
            Assert.IsType<List<string>>(entry["args"]));
    }

    /// <summary>The headless flag is added last, and only when the facts say headless.</summary>
    [Fact]
    public void Build_Headless_AddsTheFlagLastAndOnlyThen()
    {
        List<string> headed = Assert.IsType<List<string>>(Build(Facts())["args"]);
        List<string> headless = Assert.IsType<List<string>>(Build(Facts(headless: true))["args"]);

        Assert.DoesNotContain("--headless", headed);
        Assert.Equal("--headless", headless[^1]);
        Assert.Equal(headed.Count + 1, headless.Count);
    }

    /// <summary>The idle timeout minutes are passed to the server in milliseconds.</summary>
    [Fact]
    public void Build_IdleTimeout_IsMillisecondsAsInvariantText()
    {
        List<string> args = Assert.IsType<List<string>>(Build(Facts(idleMinutes: 2))["args"]);

        Assert.Equal("120000", args[args.IndexOf("--idle-timeout") + 1]);
    }

    /// <summary>The type is stdio and the command is the one given.</summary>
    [Fact]
    public void Build_Available_IsStdioWithTheGivenCommand()
    {
        Dictionary<string, object> entry = Build(Facts());

        Assert.Equal("stdio", entry["type"]);
        Assert.Equal("node", entry["command"]);
        Assert.Equal("playwright", BrowserServerConfig.ServerName);
    }

    /// <summary>Every PLAYWRIGHT_MCP key the pinned version reads is set to empty, so an Agent-written env cannot reach the server.</summary>
    [Fact]
    public void Build_Available_PinsEveryPlaywrightEnvKeyToEmpty()
    {
        Dictionary<string, string> env = Assert.IsType<Dictionary<string, string>>(Build(Facts())["env"]);

        Assert.Equal(
            ExpectedEnvironmentKeys.Select(static key => "PLAYWRIGHT_MCP_" + key).Order(StringComparer.Ordinal),
            env.Keys.Order(StringComparer.Ordinal));
        Assert.All(env.Values, static value => Assert.Equal(string.Empty, value));
    }

    /// <summary>Facts that are not available give no entry.</summary>
    [Fact]
    public void Build_Unavailable_ReturnsNull()
    {
        Assert.Null(BrowserServerConfig.Build(Facts() with { CliPath = null }, "node", "/profile", "/out"));
        Assert.Null(BrowserServerConfig.Build(Facts() with { Channel = null }, "node", "/profile", "/out"));
        Assert.Null(BrowserServerConfig.Build(Facts() with { Enabled = false }, "node", "/profile", "/out"));
    }
}
