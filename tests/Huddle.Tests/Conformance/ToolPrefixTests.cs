using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Conformance;

/// <summary>
/// Task 10.3 (Spec §15.8, T-23; Spec §1.3, O-5). The spec calls this the strongest test in the
/// suite: a wrong tool-name prefix does not make a Persona error out, it makes the Persona look
/// broken — the model reports that no such tool exists and burns a Turn finding that out. These
/// tests drive a real Persona through <see cref="MockAdapterFixture"/> under each kind of Adapter
/// profile and inspect the composed prompt the mock peer actually received.
/// </summary>
public sealed class ToolPrefixTests
{
    /// <summary>
    /// Under a profile whose <see cref="AdapterProfile.UsesToolNamePrefix"/> is <see langword="false"/>,
    /// the delivered prompt names the tool <c>get_help</c> and carries no <c>mcp__</c> prefix anywhere.
    /// </summary>
    [Fact]
    public async Task StartAsync_UnprefixedAdapterProfile_PromptNamesBareGetHelpWithNoMcpPrefix()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Persona persona = new("nova", "You are Nova.", Adapter: "agency");
        Dictionary<string, string?> additionalConfig = new(StringComparer.Ordinal)
        {
            ["Team:Acp:Adapters:0:Id"] = "agency",
            ["Team:Acp:Adapters:0:Command"] = "node",
            ["Team:Acp:Adapters:0:Args:0"] = "--mock-adapter-fixture",
            ["Team:Acp:Adapters:0:UsesToolNamePrefix"] = "false",
        };

        await using MockAdapterFixture fixture =
            await MockAdapterFixture.StartAsync(persona, additionalConfig, ct);

        string appendedPrompt = await ToolPrefixTests.GetAppendedSystemPromptAsync(fixture, ct);

        Assert.Contains("get_help", appendedPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("mcp__", appendedPrompt, StringComparison.Ordinal);
    }

    /// <summary>
    /// Under a profile whose <see cref="AdapterProfile.UsesToolNamePrefix"/> is <see langword="true"/>,
    /// the delivered prompt names the tool with its full <c>mcp__team__get_help</c> prefix.
    /// </summary>
    [Fact]
    public async Task StartAsync_PrefixedAdapterProfile_PromptNamesFullyPrefixedGetHelp()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Persona persona = new("nova", "You are Nova.", Adapter: "claude");
        Dictionary<string, string?> additionalConfig = new(StringComparer.Ordinal)
        {
            ["Team:Acp:Adapters:0:Id"] = "claude",
            ["Team:Acp:Adapters:0:Command"] = "node",
            ["Team:Acp:Adapters:0:Args:0"] = "--mock-adapter-fixture",
            ["Team:Acp:Adapters:0:UsesToolNamePrefix"] = "true",
        };

        await using MockAdapterFixture fixture =
            await MockAdapterFixture.StartAsync(persona, additionalConfig, ct);

        string appendedPrompt = await ToolPrefixTests.GetAppendedSystemPromptAsync(fixture, ct);

        Assert.Contains("mcp__team__get_help", appendedPrompt, StringComparison.Ordinal);
    }

    /// <summary>Waits for <c>session/new</c> and pulls the appended system prompt text out of its <c>_meta</c> payload.</summary>
    private static async Task<string> GetAppendedSystemPromptAsync(MockAdapterFixture fixture, CancellationToken cancellationToken)
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
