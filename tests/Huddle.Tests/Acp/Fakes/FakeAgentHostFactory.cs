using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Acp.Fakes;

/// <summary>
/// A test double for <see cref="IAgentHostFactory"/>. Returns one fixed <see cref="FakeAgentHost"/> and
/// <see cref="FakeAgentSession"/> for the whole test, so a test can reach into <see cref="Session"/> to
/// queue turn plans and inspect recorded prompts without launching a real agent process.
/// </summary>
internal sealed class FakeAgentHostFactory : IAgentHostFactory
{
    public FakeAgentHost Host { get; } = new();

    public FakeAgentSession Session { get; } = new();

    public List<(Persona Persona, string AgentId)> Calls { get; } = [];

    public Task<(IAgentHost Host, IAgentSession Session)> CreateAsync(Persona persona, string agentId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(persona);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);

        this.Calls.Add((persona, agentId));

        return Task.FromResult<(IAgentHost, IAgentSession)>((this.Host, this.Session));
    }
}