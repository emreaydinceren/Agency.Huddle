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
    private readonly TeamOptions options;
    private readonly ITeamDirectory teamDirectory;
    private readonly ChatService chat;
    private readonly IChatStore chatStore;
    private readonly AgentGateway gateway;
    private readonly Drafts drafts;
    private readonly RoomEvents roomEvents;
    private readonly FreshStartNotes freshStartNotes;
    private readonly ITurnAcks acks;
    private readonly ITurnQueueControl control;
    private readonly ILoggerFactory loggerFactory;
    private readonly ILogger<PipeServer> logger;

    public PipeServer(
        IOptions<TeamOptions> options,
        ITeamDirectory teamDirectory,
        ChatService chat,
        IChatStore chatStore,
        AgentGateway gateway,
        Drafts drafts,
        RoomEvents roomEvents,
        FreshStartNotes freshStartNotes,
        ITurnAcks acks,
        ITurnQueueControl control,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(freshStartNotes);
        ArgumentNullException.ThrowIfNull(acks);
        ArgumentNullException.ThrowIfNull(control);
        this.freshStartNotes = freshStartNotes;
        this.acks = acks;
        this.control = control;
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(teamDirectory);
        ArgumentNullException.ThrowIfNull(chat);
        ArgumentNullException.ThrowIfNull(chatStore);
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(drafts);
        ArgumentNullException.ThrowIfNull(roomEvents);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        this.options = options.Value;
        this.teamDirectory = teamDirectory;
        this.chat = chat;
        this.chatStore = chatStore;
        this.gateway = gateway;
        this.drafts = drafts;
        this.roomEvents = roomEvents;
        this.loggerFactory = loggerFactory;
        this.logger = loggerFactory.CreateLogger<PipeServer>();
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // The loop puts the next listening instance in place before it calls the handler, so the
        // connection may start I/O at once - RunAsync does, synchronously, up to its first await
        // that does not complete. See PipeAcceptLoop for why that ordering matters on Linux.
        PipeAcceptLoop loop = new(this.options.PipeName, server => this.StartConnection(server, stoppingToken), this.logger);
        return loop.RunAsync(stoppingToken);
    }

    /// <summary>Hands one connected pipe instance to a detached <see cref="AgentConnection"/>, which owns it from here.</summary>
    /// <param name="server">The connected instance.</param>
    /// <param name="stoppingToken">Ends the connection when the host stops.</param>
    private void StartConnection(NamedPipeServerStream server, CancellationToken stoppingToken)
    {
        var connection = new AgentConnection(
            server,
            this.teamDirectory,
            this.chat,
            this.chatStore,
            this.gateway,
            this.drafts,
            this.roomEvents,
            this.freshStartNotes,
            this.acks,
            this.control,
            this.loggerFactory.CreateLogger<AgentConnection>());
        _ = connection.RunAsync(stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        await this.gateway.CloseAllAsync().ConfigureAwait(false);
    }
}