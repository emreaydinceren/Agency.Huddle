using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Conformance;

/// <summary>
/// Whether <see cref="Persona.Connectors"/> reaches a real composed <c>session/new</c> request through
/// <c>DotAcpAgentHostFactory</c> (docs/Huddle.AgentConnectors-Specifications.md section 8.1), driven the same way
/// <see cref="IsolationMetaTests"/> drives the isolation <c>_meta</c>.
/// </summary>
public sealed class ConnectorsMetaTests
{
    private static JsonObject? EnvOf(JsonObject sessionNewRequest)
    {
        JsonObject? meta = MockAdapterFixture.SessionNewMeta(sessionNewRequest);
        JsonObject? claudeCode = meta?["claudeCode"] as JsonObject;
        JsonObject? options = claudeCode?["options"] as JsonObject;
        Assert.NotNull(options);
        return options["env"] as JsonObject;
    }

    /// <summary>An Agent whose connectors are off (the default) starts a session whose <c>_meta</c> switches the account connectors off.</summary>
    [Fact]
    public async Task ConnectorsOff_SessionNewCarriesEnableClaudeAiMcpServersFalse()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Persona persona = new("nova", "You are Nova.", WorkMode: "default", Connectors: false);

        await using MockAdapterFixture fixture = await MockAdapterFixture.StartAsync(persona, cancellationToken: ct);

        JsonObject? env = EnvOf(await fixture.SessionNewAsync(ct));
        Assert.NotNull(env);
        Assert.Equal("false", env["ENABLE_CLAUDEAI_MCP_SERVERS"]?.GetValue<string>());
    }

    /// <summary>An Agent whose connectors are on, in Manual mode on an isolating Adapter, starts a session with no such key.</summary>
    [Fact]
    public async Task ConnectorsOn_ManualMode_SessionNewCarriesNoEnableClaudeAiMcpServersKey()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Persona persona = new("nova", "You are Nova.", WorkMode: "default", Connectors: true);

        await using MockAdapterFixture fixture = await MockAdapterFixture.StartAsync(persona, cancellationToken: ct);

        JsonObject? env = EnvOf(await fixture.SessionNewAsync(ct));
        Assert.False(env?.ContainsKey("ENABLE_CLAUDEAI_MCP_SERVERS") ?? false);
    }
}
