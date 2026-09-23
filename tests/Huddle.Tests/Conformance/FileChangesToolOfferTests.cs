using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Conformance;

/// <summary>
/// FC §6.9, §6.11 (Task 10.2): whether <c>watch_folder</c> and <c>unwatch_folder</c> reach a real
/// composed system prompt at all, driven the same way <see cref="ToolPrefixTests"/> drives its tool
/// list - a real <see cref="Agency.Huddle.App.Acp.DotAcpAgentHostFactory"/> against
/// <see cref="MockAdapterFixture"/>, inspecting the appended system prompt's own <c>tools</c> list.
/// </summary>
public sealed class FileChangesToolOfferTests
{
    /// <summary>With the resolved Adapter Profile's <c>ReadsFiles</c> true (the default), both tools are offered by their prefixed model-facing name.</summary>
    [Fact]
    public async Task ReadsFilesTrue_OffersWatchTools()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Persona persona = new("nova", "You are Nova.");

        await using MockAdapterFixture fixture = await MockAdapterFixture.StartAsync(persona, cancellationToken: ct);

        string toolNames = await FileChangesToolOfferTests.GetToolNamesTextAsync(fixture, ct);

        Assert.Contains("mcp__team__watch_folder", toolNames, StringComparison.Ordinal);
        Assert.Contains("mcp__team__unwatch_folder", toolNames, StringComparison.Ordinal);
    }

    /// <summary>With the resolved Adapter Profile's <c>ReadsFiles</c> false, neither tool is offered - FC §6.11.</summary>
    [Fact]
    public async Task ReadsFilesFalse_OffersNeither()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Persona persona = new("nova", "You are Nova.", Adapter: "agency");
        Dictionary<string, string?> additionalConfig = new(StringComparer.Ordinal)
        {
            ["Team:Acp:Adapters:0:Id"] = "agency",
            ["Team:Acp:Adapters:0:Command"] = "node",
            ["Team:Acp:Adapters:0:Args:0"] = "--mock-adapter-fixture",
            ["Team:Acp:Adapters:0:ReadsFiles"] = "false",
        };

        await using MockAdapterFixture fixture =
            await MockAdapterFixture.StartAsync(persona, additionalConfig, ct);

        string toolNames = await FileChangesToolOfferTests.GetToolNamesTextAsync(fixture, ct);

        Assert.DoesNotContain("watch_folder", toolNames, StringComparison.Ordinal);
    }

    /// <summary>With <c>Team:FileChanges:Enabled</c> false, neither tool is offered even on an Adapter that reads files.</summary>
    [Fact]
    public async Task FileChangesDisabled_OffersNeither()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Persona persona = new("nova", "You are Nova.");
        Dictionary<string, string?> additionalConfig = new(StringComparer.Ordinal)
        {
            ["Team:FileChanges:Enabled"] = "false",
        };

        await using MockAdapterFixture fixture =
            await MockAdapterFixture.StartAsync(persona, additionalConfig, ct);

        string toolNames = await FileChangesToolOfferTests.GetToolNamesTextAsync(fixture, ct);

        Assert.DoesNotContain("watch_folder", toolNames, StringComparison.Ordinal);
    }

    /// <summary>Waits for <c>session/new</c> and returns the composed system prompt's own tool-list text, where every offered tool's prefixed name is named (matching <see cref="SystemPromptComposer"/>'s <c>systemPrompt.tools</c> rendering).</summary>
    private static async Task<string> GetToolNamesTextAsync(MockAdapterFixture fixture, CancellationToken cancellationToken)
    {
        JsonObject sessionNewRequest = await fixture.Agent.WaitForAsync("session/new", TimeSpan.FromSeconds(5));
        cancellationToken.ThrowIfCancellationRequested();

        JsonObject? parameters = sessionNewRequest["params"] as JsonObject;
        JsonObject? meta = parameters?["_meta"] as JsonObject;
        JsonObject? systemPrompt = meta?["systemPrompt"] as JsonObject;
        string? appendedPrompt = (string?)systemPrompt?["append"];

        Assert.NotNull(appendedPrompt);
        return appendedPrompt;
    }
}
