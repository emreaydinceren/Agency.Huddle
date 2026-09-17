using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Conformance;

/// <summary>
/// Smoke test for <see cref="MockAdapterFixture"/> (D10, Task 10.1). Every later Phase 6
/// conformance test (Spec §15.8) builds on this much working: a real Persona, started through the
/// fixture, actually reaches a real host and session that speak to the scripted peer.
/// </summary>
public sealed class MockAdapterFixtureSmokeTests
{
    /// <summary>Starting a Persona through the fixture sends its mock ACP peer an <c>initialize</c> request.</summary>
    [Fact]
    public async Task StartAsync_StartsAPersona_PeerReceivesInitialize()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Persona persona = new("nova", "You are Nova, a test persona.");

        await using MockAdapterFixture fixture = await MockAdapterFixture.StartAsync(persona, cancellationToken: ct);

        JsonObject initializeRequest = await fixture.Agent.WaitForAsync("initialize", TimeSpan.FromSeconds(5));

        Assert.Equal("initialize", (string?)initializeRequest["method"]);
        Assert.Contains(fixture.Agent.Received, message => (string?)message["method"] == "initialize");
    }
}
