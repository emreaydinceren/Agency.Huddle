using System.Globalization;

namespace Agency.Huddle.App.Acp;

/// <summary>Builds the MCP server entry that gives an Agent a browser through Playwright MCP.</summary>
internal static class BrowserServerConfig
{
    /// <summary>The name the server is registered under, so its tools are <c>mcp__playwright__*</c>.</summary>
    internal const string ServerName = "playwright";

    /// <summary>
    /// Every <c>PLAYWRIGHT_MCP_*</c> key the pinned <c>@playwright/mcp</c> 0.0.84 reads, taken from playwright-core's
    /// <c>configFromEnv</c>. Each is set to "" (empty is unset in its parser) because an <c>env</c> block an Agent writes
    /// into its <c>settings.local.json</c> otherwise reaches the server: <c>PLAYWRIGHT_MCP_CAPS=vision</c> added tools (measured
    /// in the #551 spike). Command-line flags already beat the environment. Re-read the list when the pin moves.
    /// </summary>
    private static readonly string[] EnvironmentKeys =
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

    /// <summary>Builds the server entry.</summary>
    /// <param name="facts">What was found on this computer.</param>
    /// <param name="command">The program that runs the CLI (the Adapter profile's node).</param>
    /// <param name="profileDirectory">The browser profile folder.</param>
    /// <param name="outputDirectory">The folder for auto-named files.</param>
    /// <returns>The entry, or <see langword="null"/> when the facts are not available.</returns>
    internal static Dictionary<string, object>? Build(
        BrowserLaunchFacts facts,
        string command,
        string profileDirectory,
        string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (!facts.Available || facts.CliPath is null || facts.Channel is null)
        {
            return null;
        }

        List<string> args =
        [
            facts.CliPath,
            "--browser", facts.Channel,
            "--user-data-dir", profileDirectory,
            "--output-dir", outputDirectory,
            "--idle-timeout", (facts.IdleTimeoutMinutes * 60000L).ToString(CultureInfo.InvariantCulture),
            "--no-webmcp",
        ];
        if (facts.Headless)
        {
            args.Add("--headless");
        }

        Dictionary<string, string> env = new(StringComparer.Ordinal);
        foreach (string key in EnvironmentKeys)
        {
            env["PLAYWRIGHT_MCP_" + key] = string.Empty;
        }

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["type"] = "stdio",
            ["command"] = command,
            ["args"] = args,
            ["env"] = env,
        };
    }
}
