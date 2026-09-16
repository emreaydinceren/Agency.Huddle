using Microsoft.Extensions.Options;
using System.IO.Pipes;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;

namespace Agency.Huddle.App.Pipes;

/// <summary>
/// Accepts Agent connections on the configured named pipe and hands each one to a detached
/// <see cref="AgentConnection"/>. See Team-Specifications.md §6.2 and §10 for the accept-loop and
/// sequencing constraints (notably: create the next server instance before touching the accepted one).
/// </summary>
internal sealed class PipeServer : BackgroundService
{
    /// <summary>
    /// In and out buffer size for every accepted pipe instance, in bytes. Sized to hold many whole
    /// protocol envelopes rather than to any measured figure - the requirement is only that a normal
    /// write never has to wait for the peer to read, and an envelope is a JSON line. Matches the 64 KB
    /// the <c>FileSystemWatcher</c> in <c>PersonaStore</c> uses, for the same class of reason.
    /// </summary>
    private const int PipeBufferBytes = 64 * 1024;

    private readonly TeamOptions options;
    private readonly ITeamDirectory teamDirectory;
    private readonly ChatService chat;
    private readonly AgentGateway gateway;
    private readonly Drafts drafts;
    private readonly RoomEvents roomEvents;
    private readonly ILoggerFactory loggerFactory;
    private readonly ILogger<PipeServer> logger;

    public PipeServer(
        IOptions<TeamOptions> options,
        ITeamDirectory teamDirectory,
        ChatService chat,
        AgentGateway gateway,
        Drafts drafts,
        RoomEvents roomEvents,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(teamDirectory);
        ArgumentNullException.ThrowIfNull(chat);
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(drafts);
        ArgumentNullException.ThrowIfNull(roomEvents);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        this.options = options.Value;
        this.teamDirectory = teamDirectory;
        this.chat = chat;
        this.gateway = gateway;
        this.drafts = drafts;
        this.roomEvents = roomEvents;
        this.loggerFactory = loggerFactory;
        this.logger = loggerFactory.CreateLogger<PipeServer>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // The buffer sizes are load-bearing, and their absence deadlocked an Agent permanently.
            // Left unspecified they default to 0, which on Windows means every write blocks until the
            // peer reads it - a lock-step handshake. AgentConnection answers a refused post (a spent
            // Room Budget, most often) by writing a ProtocolError from INSIDE its read loop, so a
            // client that had sent its post and gone straight on to its next envelope - which is
            // well-behaved, and what DemoAgentHost and PersonaRunner both do - met a server blocked
            // writing while it was blocked writing. Neither side read again.
            //
            // Worse than a stalled turn: JsonLineStream serialises writes behind one semaphore, so
            // the stuck read-loop write also held the lock that SendAsync needs, and that Agent stopped
            // receiving messages in every Room while its pipe stayed open and its tile stayed green.
            // A buffer lets an envelope-sized write complete into the OS and release the lock.
            var server = new NamedPipeServerStream(
                this.options.PipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                inBufferSize: PipeBufferBytes,
                outBufferSize: PipeBufferBytes);

            using var registration = stoppingToken.Register(server.Dispose);

            try
            {
                await server.WaitForConnectionAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                server.Dispose();
                break;
            }
            catch (IOException ex)
            {
                this.logger.LogWarning(ex, "Failed to accept a connection on pipe {PipeName}.", this.options.PipeName);
                server.Dispose();
                await Task.Delay(TimeSpan.FromMilliseconds(250), stoppingToken);
                continue;
            }

            var connection = new AgentConnection(
                server,
                this.teamDirectory,
                this.chat,
                this.gateway,
                this.drafts,
                this.roomEvents,
                this.loggerFactory.CreateLogger<AgentConnection>());
            _ = connection.RunAsync(stoppingToken);

            // Loop immediately: the next server instance must be created before any I/O happens on
            // the one just handed off, or the next client's connect attempt fails as "all pipe
            // instances are busy".
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await this.gateway.CloseAllAsync();
    }
}