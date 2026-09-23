using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Conformance;

/// <summary>
/// RS §6.10 "Recommended" (P0-4), gated by finding P-11's <see cref="AdapterProfile.IsolateUserSettings"/>
/// flag (Task 14.2): whether the isolation <c>_meta.claudeCode.options</c> reaches a real composed
/// <c>session/new</c> request, driven the same way <see cref="ToolPrefixTests"/> drives its tool list.
/// </summary>
public sealed class IsolationMetaTests
{
    /// <summary>With the resolved Adapter Profile's <c>IsolateUserSettings</c> true (the legacy default), <c>session/new</c>'s <c>_meta.claudeCode.options</c> carries the isolation settings verbatim.</summary>
    [Fact]
    public async Task IsolateUserSettingsTrue_SessionNewCarriesSettingSourcesAndAutoMemoryOff()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Persona persona = new("nova", "You are Nova.");

        await using MockAdapterFixture fixture = await MockAdapterFixture.StartAsync(persona, cancellationToken: ct);

        JsonObject sessionNewRequest = await fixture.Agent.WaitForAsync("session/new", TimeSpan.FromSeconds(5));
        JsonObject? parameters = sessionNewRequest["params"] as JsonObject;
        JsonObject? meta = parameters?["_meta"] as JsonObject;
        JsonObject? claudeCode = meta?["claudeCode"] as JsonObject;
        JsonObject? options = claudeCode?["options"] as JsonObject;

        Assert.NotNull(options);

        JsonNode expected = JsonNode.Parse("""{"settingSources":["project","local"],"settings":{"autoMemoryEnabled":false}}""")!;
        Assert.True(JsonNode.DeepEquals(expected, options), $"Expected {expected} but got {options}.");
    }

    /// <summary>With the resolved Adapter Profile's <c>IsolateUserSettings</c> false, <c>session/new</c> carries no <c>claudeCode</c> entry at all.</summary>
    [Fact]
    public async Task IsolateUserSettingsFalse_NoClaudeCodeMeta()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Persona persona = new("nova", "You are Nova.", Adapter: "agency");
        Dictionary<string, string?> additionalConfig = new(StringComparer.Ordinal)
        {
            ["Team:Acp:Adapters:0:Id"] = "agency",
            ["Team:Acp:Adapters:0:Command"] = "node",
            ["Team:Acp:Adapters:0:Args:0"] = "--mock-adapter-fixture",
        };

        await using MockAdapterFixture fixture =
            await MockAdapterFixture.StartAsync(persona, additionalConfig, ct);

        JsonObject sessionNewRequest = await fixture.Agent.WaitForAsync("session/new", TimeSpan.FromSeconds(5));
        JsonObject? parameters = sessionNewRequest["params"] as JsonObject;
        JsonObject? meta = parameters?["_meta"] as JsonObject;

        Assert.False(meta?.ContainsKey("claudeCode") ?? false);
    }
}
