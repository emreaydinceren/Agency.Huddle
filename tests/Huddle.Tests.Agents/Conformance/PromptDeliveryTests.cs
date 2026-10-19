using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Conformance;

/// <summary>
/// Task 10.2 (Spec §15.8, T-22): a golden test proves <see cref="SystemPromptComposer"/> composes
/// the right string, but only a real session start against a real ACP peer proves that string
/// actually arrives on the wire. This drives a real Persona through <see cref="MockAdapterFixture"/>
/// and inspects the <c>session/new</c> request the mock peer received.
/// </summary>
public sealed class PromptDeliveryTests
{
    /// <summary>
    /// The <c>session/new</c> request the mock peer receives carries the Persona's own text inside
    /// its <c>_meta.systemPrompt.append</c> payload — the shape <c>DotAcpAgentHost.StartSessionAsync</c>
    /// sends a non-<see cref="Agency.Huddle.Acp.Abstractions.SystemPromptMode.Replace"/> prompt in.
    /// </summary>
    [Fact]
    public async Task StartAsync_RealSession_SessionNewCarriesPersonaTextInMeta()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        const string personaText = "You are Nova, a persona whose exact words must reach the wire.";
        Persona persona = new("nova", personaText);

        await using MockAdapterFixture fixture = await MockAdapterFixture.StartAsync(persona, cancellationToken: ct);

        string appendedPrompt = await fixture.AppendedSystemPromptAsync(ct);

        Assert.Contains(personaText, appendedPrompt, StringComparison.Ordinal);
    }
}
