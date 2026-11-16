using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.DotAcp;
using Agency.Huddle.Acp.Hosting;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// One adapter process and one session, started together and disposed together: the launch skeleton that
/// <see cref="AdapterProcessProbeRunner"/> and <see cref="AdapterProcessHelperRunner"/> share. Disposing ends the
/// session, then the host, whether or not the Turn finished.
/// </summary>
internal sealed class ThrowawayAdapterSession : IAsyncDisposable
{
    private readonly DotAcpAgentHost host;

    private ThrowawayAdapterSession(DotAcpAgentHost host, IAgentSession session)
    {
        this.host = host;
        this.Session = session;
    }

    /// <summary>The open session.</summary>
    public IAgentSession Session { get; }

    /// <summary>Launches the adapter, does <c>initialize</c> and <c>session/new</c>, and returns both.</summary>
    /// <param name="processOptions">How to launch the adapter.</param>
    /// <param name="sessionOptions">The session to open.</param>
    /// <param name="traceWire">Whether the host traces the wire (<c>Acp.TraceWire</c>).</param>
    /// <param name="loggerFactory">Handed to the launched host and its process launcher.</param>
    /// <param name="cancellationToken">Cancels the start; a half-started host is disposed first.</param>
    public static async Task<ThrowawayAdapterSession> StartAsync(
        AgentProcessOptions processOptions,
        AgentSessionOptions sessionOptions,
        bool traceWire,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(processOptions);
        ArgumentNullException.ThrowIfNull(sessionOptions);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        AgentProcessLauncher launcher = new(loggerFactory.CreateLogger<AgentProcessLauncher>());
        DotAcpHostOptions hostOptions = new("Team.App", TraceWire: traceWire);
        DotAcpAgentHost host = new(processOptions, launcher, hostOptions, loggerFactory);
        try
        {
            await host.StartAsync(cancellationToken).ConfigureAwait(false);
            IAgentSession session = await host.StartSessionAsync(sessionOptions, cancellationToken).ConfigureAwait(false);
            return new ThrowawayAdapterSession(host, session);
        }
        catch
        {
            await host.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Reads whether the adapter reports a person signed in; see <see cref="DotAcpAgentHost.ReadSignInStatusAsync"/>.</summary>
    /// <param name="wait">The longest to wait for the adapter's first report; nothing is waited for when it never advertised one.</param>
    /// <param name="cancellationToken">Ends the wait early.</param>
    /// <returns>The status, or <see cref="AgentSignInStatus.Unknown"/> when it cannot be determined.</returns>
    public Task<AgentSignInStatus> ReadSignInStatusAsync(TimeSpan wait, CancellationToken cancellationToken)
    {
        return this.host.ReadSignInStatusAsync(wait, cancellationToken);
    }

    /// <summary>Disposes the session, then the host.</summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await this.Session.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            await this.host.DisposeAsync().ConfigureAwait(false);
        }
    }
}
