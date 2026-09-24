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

    /// <summary>
    /// Gets a value indicating whether the fake process keeps running after the agent's read
    /// loop ends. By default the process exits when its standard input closes, as a real agent
    /// does; set this to exercise the host's kill-after-grace-period path.
    /// </summary>
    internal bool IgnoresInputClose { get; init; }

    public IAgentProcess Launch(AgentProcessOptions options)
    {
        this.LastOptions = options;
        _ = this.RunAgentAsync();
        return this.Process;
    }

    /// <summary>Runs the fake agent's read loop and, unless told otherwise, exits the process when it ends.</summary>
    private async Task RunAgentAsync()
    {
        try
        {
            await this.Agent.RunAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            if (!this.IgnoresInputClose)
            {
                this.Process.ExitSource.TrySetResult(0);
            }
        }
    }
}
