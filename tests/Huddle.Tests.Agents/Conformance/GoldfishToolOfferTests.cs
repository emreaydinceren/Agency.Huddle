using System.Text.RegularExpressions;
using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Conformance;

/// <summary>
/// Goldfish slice 3 step 2: <c>goldfish</c> and <c>goldfish_done</c> reach a composed system prompt's tool list only for a Claude
/// Adapter that isolates user settings while <c>Team:Goldfish:Enabled</c> is on, and no question tool ever joins them. Driven like
/// <see cref="FileChangesToolOfferTests"/>, against <see cref="MockAdapterFixture"/>.
/// </summary>
public sealed partial class GoldfishToolOfferTests
{
    [GeneratedRegex(@"mcp__team__goldfish(?!_done)", RegexOptions.CultureInvariant)]
    private static partial Regex GoldfishTool();

    private static Dictionary<string, string?> Adapter(bool claude, bool isolate) => new(StringComparer.Ordinal)
    {
        ["Team:Acp:Adapters:0:Id"] = "agency",
        ["Team:Acp:Adapters:0:Command"] = "node",
        ["Team:Acp:Adapters:0:Args:0"] = "--mock-adapter-fixture",
        ["Team:Acp:Adapters:0:Args:1"] = claude ? "claude-agent-acp" : "plain-agent",
        ["Team:Acp:Adapters:0:IsolateUserSettings"] = isolate ? "true" : "false",
    };

    private static async Task<string> ToolNamesAsync(Dictionary<string, string?> config, CancellationToken ct)
    {
        Persona persona = new("nova", "You are Nova.", Adapter: "agency");
        await using MockAdapterFixture fixture = await MockAdapterFixture.StartAsync(persona, config, cancellationToken: ct);
        return await fixture.AppendedSystemPromptAsync(ct);
    }

    /// <summary>A Claude Adapter that isolates user settings is offered both tools, by their prefixed names, and no question tool.</summary>
    [Fact]
    public async Task ClaudeIsolating_OffersBothTools()
    {
        string toolNames = await ToolNamesAsync(Adapter(claude: true, isolate: true), TestContext.Current.CancellationToken);

        Assert.Matches(GoldfishTool(), toolNames);
        Assert.Contains("mcp__team__goldfish_done", toolNames, StringComparison.Ordinal); // contains-ok: the exact prefixed name is the offer
        Assert.DoesNotContain("goldfish_ask", toolNames, StringComparison.Ordinal);
        Assert.DoesNotContain("goldfish_question", toolNames, StringComparison.Ordinal);
    }

    /// <summary>A Claude Adapter that shares the Human's settings is offered neither tool (shared mode is not offered).</summary>
    [Fact]
    public async Task ClaudeNotIsolating_OffersNeither()
    {
        string toolNames = await ToolNamesAsync(Adapter(claude: true, isolate: false), TestContext.Current.CancellationToken);

        Assert.DoesNotMatch(GoldfishTool(), toolNames);
        Assert.DoesNotContain("mcp__team__goldfish_done", toolNames, StringComparison.Ordinal);
    }

    /// <summary>An isolating Adapter that does not run Claude is offered neither tool, with no refusal Prompt of its own.</summary>
    [Fact]
    public async Task IsolatingNotClaude_OffersNeither()
    {
        string toolNames = await ToolNamesAsync(Adapter(claude: false, isolate: true), TestContext.Current.CancellationToken);

        Assert.DoesNotMatch(GoldfishTool(), toolNames);
        Assert.DoesNotContain("mcp__team__goldfish_done", toolNames, StringComparison.Ordinal);
    }

    /// <summary>With <c>Team:Goldfish:Enabled</c> false a Claude isolating Adapter is offered neither tool.</summary>
    [Fact]
    public async Task GoldfishDisabled_OffersNeither()
    {
        Dictionary<string, string?> config = Adapter(claude: true, isolate: true);
        config["Team:Goldfish:Enabled"] = "false";

        string toolNames = await ToolNamesAsync(config, TestContext.Current.CancellationToken);

        Assert.DoesNotMatch(GoldfishTool(), toolNames);
        Assert.DoesNotContain("mcp__team__goldfish_done", toolNames, StringComparison.Ordinal);
    }
}
