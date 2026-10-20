using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Feed;

/// <summary>
/// The producers' door to the Feed (spec 6.2): a bounded FIFO <see cref="Channel{T}"/> with one reader, the
/// <see cref="FeedWriter"/>. Every method only enqueues and never throws; a full queue or a payload that does
/// not match its source type is logged and dropped.
/// </summary>
internal sealed class FeedQueue : IFeedPublisher
{
    private readonly Channel<FeedCommand> channel;
    private readonly ILogger<FeedQueue> logger;
    private readonly TimeProvider timeProvider;

    /// <summary>Creates the queue with the capacity from <c>Team:Feed:QueueCapacity</c>.</summary>
    /// <param name="options">Supplies the Feed options.</param>
    /// <param name="logger">Receives the warnings for dropped commands.</param>
    /// <param name="timeProvider">The clock a command's time is taken from; the system clock when omitted.</param>
    public FeedQueue(IOptions<TeamOptions> options, ILogger<FeedQueue> logger, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.logger = logger;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.channel = Channel.CreateBounded<FeedCommand>(
            new BoundedChannelOptions(Math.Max(1, options.Value.Feed.QueueCapacity))
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true,
            },
            this.LogDropped);
    }

    /// <summary>The end the <see cref="FeedWriter"/> reads from.</summary>
    public ChannelReader<FeedCommand> Reader => this.channel.Reader;

    /// <inheritdoc/>
    public void Post(FeedMessage message)
    {
        if (!FeedPayloads.Matches(message.SourceType, message.Payload))
        {
            this.logger.LogWarning(
                "Feed item dropped: a {PayloadType} payload does not match source type {SourceType}.",
                message.Payload.GetType().Name,
                FeedSourceTypes.ToWire(message.SourceType));
            return;
        }

        _ = this.channel.Writer.TryWrite(new PostCommand(message, this.timeProvider.GetUtcNow()));
    }

    /// <inheritdoc/>
    public void Resolve(string targetKey, FeedSourceType type) =>
        _ = this.channel.Writer.TryWrite(new ResolveCommand(targetKey, type, this.timeProvider.GetUtcNow()));

    /// <summary>Queues marking every Notification up to an id as seen by the bell.</summary>
    /// <param name="upToId">The highest id to mark.</param>
    internal void MarkSeen(long upToId) =>
        _ = this.channel.Writer.TryWrite(new MarkSeenCommand(upToId, this.timeProvider.GetUtcNow()));

    /// <summary>Queues dismissing one Notification.</summary>
    /// <param name="id">The Notification's id.</param>
    internal void Dismiss(long id) =>
        _ = this.channel.Writer.TryWrite(new DismissCommand(id, this.timeProvider.GetUtcNow()));

    /// <summary>Queues moving the Feed Tab's seen mark forward.</summary>
    /// <param name="upToId">The highest Feed item id the Tab has shown.</param>
    internal void MarkTabSeen(long upToId) =>
        _ = this.channel.Writer.TryWrite(new MarkTabSeenCommand(upToId, this.timeProvider.GetUtcNow()));

    private void LogDropped(FeedCommand command) =>
        this.logger.LogWarning("Feed queue is full; dropped a {CommandType} command.", command.GetType().Name);
}
