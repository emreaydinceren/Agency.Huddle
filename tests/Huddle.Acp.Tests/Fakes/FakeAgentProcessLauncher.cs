namespace Agency.Huddle.Acp.Tests.Fakes;

using System.IO;
using System.Threading;
using Nerdbank.Streams;
using Agency.Huddle.Acp.Hosting;

/// <summary>
/// An <see cref="IAgentProcessLauncher"/> that hands back a <see cref="FakeAgentProcess"/> wired
/// to a <see cref="FakeAcpAgent"/> over an in-memory duplex stream pair, and starts the fake
/// agent's read loop as part of <see cref="Launch"/>, mirroring how the real launcher starts a
/// process.
/// </summary>
internal sealed class FakeAgentProcessLauncher : IAgentProcessLauncher
{
    internal FakeAgentProcessLauncher()
    {
        (Stream processEnd, Stream agentEnd) = FullDuplexStream.CreatePair();
        this.Process = new FakeAgentProcess(processEnd);
        this.Agent = new FakeAcpAgent(agentEnd);
    }

    internal FakeAcpAgent Agent { get; }

    internal FakeAgentProcess Process { get; }

    internal AgentProcessOptions? LastOptions { get; private set; }

    public IAgentProcess Launch(AgentProcessOptions options)
    {
        this.LastOptions = options;
        _ = this.Agent.RunAsync(CancellationToken.None);
        return this.Process;
    }
}
