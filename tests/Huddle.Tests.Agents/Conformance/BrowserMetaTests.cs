using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Conformance;

/// <summary>
/// Whether the Playwright MCP server reaches a real composed <c>session/new</c> request through <c>DotAcpAgentHostFactory</c> (#551), driven the
/// same way <see cref="ConnectorsMetaTests"/> drives the connectors <c>_meta</c>. The browser facts are named by the test, so nothing is installed or started.
/// </summary>
public sealed class BrowserMetaTests
{
    private static readonly BrowserLaunchFacts Available = new(true, Path.Combine("tools", "acp", "cli.js"), "msedge", false, false, 30);

    /// <summary>The fixture's default Args name no real Adapter, so name the Claude one: the browser is passed only to a profile that runs Claude. The fake launcher never reads it.</summary>
    private static readonly Dictionary<string, string?> RunsClaude = new(StringComparer.Ordinal)
    {
        ["Team:Acp:Args:0"] = "claude-agent-acp",
    };

    private static JsonObject Child(JsonNode? parent, string key)
    {
        JsonObject? child = (parent as JsonObject)?[key] as JsonObject;
        Assert.NotNull(child);
        return child;
    }

    private static JsonObject OptionsOf(JsonObject sessionNewRequest) =>
        Child(Child(MockAdapterFixture.SessionNewMeta(sessionNewRequest), "claudeCode"), "options");

    private static string[] Strings(JsonNode? node)
    {
        JsonArray? array = node as JsonArray;
        Assert.NotNull(array);
        return [.. array.Select(item => item?.GetValue<string>() ?? string.Empty)];
    }

    /// <summary>An Agent whose browser is on starts a session whose <c>_meta</c> carries the server, on its own profile folder and output folder, with the ask and deny rules.</summary>
    [Fact]
    public async Task BrowserOn_SessionNewCarriesTheServerRulesAndPaths()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Persona persona = new("nova", "You are Nova.", WorkMode: "default");

        await using MockAdapterFixture fixture = await MockAdapterFixture.StartAsync(persona, RunsClaude, browserFacts: Available, cancellationToken: ct);

        JsonObject options = OptionsOf(await fixture.SessionNewAsync(ct));
        JsonObject entry = Child(Child(options, "mcpServers"), "playwright");
        string[] args = Strings(entry["args"]);
        AgentPaths paths = new(Options.Create(new TeamOptions { DataDir = fixture.DataDirPath }));
        Assert.Equal(Available.CliPath, args[0]);
        Assert.Equal(paths.BrowserProfile("nova"), args[Array.IndexOf(args, "--user-data-dir") + 1]);
        Assert.Equal(Path.Combine(paths.WorkDir("nova"), "browser-output"), args[Array.IndexOf(args, "--output-dir") + 1]);
        JsonObject permissions = Child(Child(options, "settings"), "permissions");
        Assert.Equal("mcp__playwright__*", Strings(permissions["ask"])[^1]);
        Assert.Equal(["mcp__playwright__browser_run_code_unsafe"], Strings(permissions["deny"]));
    }

    /// <summary>An Agent whose browser the Human switched off starts a session with no server and no deny rule.</summary>
    [Fact]
    public async Task BrowserOff_SessionNewCarriesNoServer()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Persona persona = new("nova", "You are Nova.", WorkMode: "default", BrowserOff: true);

        await using MockAdapterFixture fixture = await MockAdapterFixture.StartAsync(persona, RunsClaude, browserFacts: Available, cancellationToken: ct);

        JsonObject options = OptionsOf(await fixture.SessionNewAsync(ct));
        Assert.False(options.ContainsKey("mcpServers"));
        Assert.False(Child(Child(options, "settings"), "permissions").ContainsKey("deny"));
    }
}
