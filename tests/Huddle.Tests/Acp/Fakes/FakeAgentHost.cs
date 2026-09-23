using Agency.Huddle.Acp.Abstractions;

namespace Agency.Huddle.Tests.Acp.Fakes;

/// <summary>A test double for <see cref="IAgentHost"/> that never launches a real process.</summary>
internal sealed class FakeAgentHost : IAgentHost
{
    public AgentHostInfo Info { get; } = new("fake-agent", "0.0.0", 1, [], SupportsLoadSession: false);

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task<IAgentSession> StartSessionAsync(AgentSessionOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        return Task.FromResult<IAgentSession>(new FakeAgentSession());
    }

    // D19 (RS §6.3) is what wires resume up on the chat-surface side, through IPersonaHost /
    // FakePersonaHost instead of this type. Nothing calls this yet.
    public Task<IAgentSession> ResumeSessionAsync(string sessionId, AgentSessionOptions options, CancellationToken cancellationToken)
    {
        throw new NotSupportedException("Resume is not wired up until D19 (RS §6.3) replaces this fake's caller.");
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}