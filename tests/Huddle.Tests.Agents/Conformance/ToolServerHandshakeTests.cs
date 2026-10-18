using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Conformance;

/// <summary>
/// Task 10.4 (Spec §15.8, T-24): proves the <c>AppToolServer</c> handshake reaches the peer,
/// authenticated, by driving a real Persona through <see cref="MockAdapterFixture"/> and inspecting
/// the <c>mcpServers</c> entry the mock's <c>session/new</c> actually received.
/// </summary>
public sealed class ToolServerHandshakeTests
{
    /// <summary>
    /// The <c>session/new</c> request carries exactly one <c>mcpServers</c> entry, pointing at a
    /// <c>127.0.0.1</c> URL ending <c>/mcp</c> and carrying an <c>Authorization</c> header whose
    /// value starts <c>Bearer </c>. The token's own value is minted per session
    /// (<c>RandomNumberGenerator.GetBytes(32)</c>) and is deliberately never asserted here.
    /// </summary>
    [Fact]
    public async Task StartAsync_RealSession_SessionNewCarriesOneAuthenticatedLoopbackMcpServer()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Persona persona = new("nova", "You are Nova.");

        await using MockAdapterFixture fixture = await MockAdapterFixture.StartAsync(persona, cancellationToken: ct);

        JsonObject sessionNewRequest = await fixture.Agent.WaitForAsync("session/new", TimeSpan.FromSeconds(5));

        JsonObject? parameters = sessionNewRequest["params"] as JsonObject;
        JsonArray? mcpServers = parameters?["mcpServers"] as JsonArray;
        Assert.NotNull(mcpServers);
        JsonNode? mcpServerNode = Assert.Single(mcpServers);
        Assert.NotNull(mcpServerNode);
        JsonObject mcpServer = mcpServerNode.AsObject();

        string? url = (string?)mcpServer["url"];
        Assert.NotNull(url);
        Assert.StartsWith("http://127.0.0.1:", url, StringComparison.Ordinal);
        Assert.EndsWith("/mcp", url, StringComparison.Ordinal);

        JsonArray? headers = mcpServer["headers"] as JsonArray;
        Assert.NotNull(headers);
        JsonObject? authorizationHeader = headers
            .Select(header => header as JsonObject)
            .FirstOrDefault(header => string.Equals((string?)header?["name"], "Authorization", StringComparison.Ordinal));

        Assert.NotNull(authorizationHeader);
        string? authorizationValue = (string?)authorizationHeader["value"];
        Assert.NotNull(authorizationValue);
        Assert.StartsWith("Bearer ", authorizationValue, StringComparison.Ordinal);
    }
}
