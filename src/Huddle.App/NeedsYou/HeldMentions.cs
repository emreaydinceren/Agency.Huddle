using Agency.Huddle.App.Data;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Agency.Huddle.App.NeedsYou;

/// <summary>
/// Keeps the requests an Agent's Message made of another Agent while its Room was paused (#428). The refused Turn is posted as one
/// Notification per Room and Agent; it is kept across a restart while it still applies; the Human's Wake button starts exactly one Turn.
/// Nothing is ever woken on startup: the pause is a cost guard.
/// </summary>
internal sealed class HeldMentions : BackgroundService, IHeldMentionWake
{
    private const string WakeKey = "turn.wake.restart";

    private readonly IFeedPublisher feed;
    private readonly FeedStore feedStore;
    private readonly ChatService chat;
    private readonly ITeamDirectory directory;
    private readonly IChatStore chatStore;
    private readonly Func<string, bool> agentExists;
    private readonly IPromptSource prompts;
    private readonly TeamOptions options;
    private readonly ILogger<HeldMentions> logger;
    private readonly ConcurrentDictionary<string, bool> woken = new(StringComparer.Ordinal);

    /// <summary>Creates the keeper and subscribes it to the Room events it works from.</summary>
    /// <param name="events">The hub whose Message events drive it.</param>
    /// <param name="feed">Receives the Notifications and their resolutions.</param>
    /// <param name="feedStore">Read for the Notifications still open.</param>
    /// <param name="chat">Posts the Wake Message and reports whether the Room is paused.</param>
    /// <param name="directory">Reads the Room and its Members.</param>
    /// <param name="chatStore">Reads the Transcript at startup.</param>
    /// <param name="agentExists">Tells whether an Agent by this Name still exists.</param>
    /// <param name="prompts">Renders the Wake Message.</param>
    /// <param name="options">The wake-ups switches.</param>
    /// <param name="logger">Receives a failed pass or wake.</param>
    public HeldMentions(
        RoomEvents events,
        IFeedPublisher feed,
        FeedStore feedStore,
        ChatService chat,
        ITeamDirectory directory,
        IChatStore chatStore,
        Func<string, bool> agentExists,
        IPromptSource prompts,
        IOptions<TeamOptions> options,
        ILogger<HeldMentions> logger)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(options);

        this.feed = feed;
        this.feedStore = feedStore;
        this.chat = chat;
        this.directory = directory;
        this.chatStore = chatStore;
        this.agentExists = agentExists;
        this.prompts = prompts;
        this.options = options.Value;
        this.logger = logger;
        events.MessagePosted += this.OnMessagePosted;
        events.MessageRedelivered += this.OnMessageRedelivered;
    }

    /// <inheritdoc />
    public bool IsHeld(string roomId) => this.chat.GetBudget(roomId).Exhausted;

    /// <inheritdoc />
    public async Task<string?> WakeAsync(string roomId, string agentId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);

        string key = KeyOf(roomId, agentId);
        RoomMentionHeldPayload? held = this.Open().Where(item => string.Equals(item.TargetKey, key, StringComparison.Ordinal))
            .Select(item => item.Payload).OfType<RoomMentionHeldPayload>().FirstOrDefault();
        if (held is null || this.IsHeld(roomId) || this.woken.ContainsKey(key))
        {
            return null;
        }

        Room? room = await this.directory.GetRoomAsync(roomId, ct).ConfigureAwait(false);
        IReadOnlyList<User> members = room is null ? [] : await this.directory.GetRoomMembersAsync(roomId, ct).ConfigureAwait(false);
        User? agent = members.FirstOrDefault(member => member.Kind == UserKind.Agent && string.Equals(member.Id, agentId, StringComparison.Ordinal));
        if (room is null || room.Archived || agent is null || !this.agentExists(agent.Name))
        {
            this.Close(key);
            return null;
        }

        if (!this.options.Tasks.Enabled || !this.options.Tasks.WakeEnabled || !this.woken.TryAdd(key, true))
        {
            return null;
        }

        Dictionary<string, string> values = new(StringComparer.Ordinal)
        {
            ["{{agent}}"] = "@" + agent.Name,
            ["{{messageId}}"] = held.MessageId,
        };
        try
        {
            _ = await this.chat.PostAsync(roomId, KnownIds.Human, this.prompts.Render(WakeKey, values), ct: ct).ConfigureAwait(false);
        }
        catch (ChatException ex)
        {
            _ = this.woken.TryRemove(key, out _);
            this.logger.LogWarning(ex, "The wake for {Agent} in Room {RoomId} was refused.", agent.Name, roomId);
            return null;
        }

        this.Close(key);
        return agent.Name;
    }

    /// <summary>Keeps each open Notification only while it still applies; closes the rest. It posts nothing and wakes no Agent.</summary>
    /// <param name="stoppingToken">Stops the pass when the host shuts down.</param>
    /// <returns>A task that completes when every open Notification has been checked.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        try
        {
            await this.ReconcileAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            this.logger.LogWarning(ex, "The Feed could not reconcile its held requests at startup.");
        }
    }

    /// <summary>
    /// Closes each open Notification whose Room or Agent is gone, whose Room is archived, or whose Agent has posted in the Room since
    /// the held Message. Only closes; nothing is posted and no Agent is woken.
    /// </summary>
    /// <param name="ct">Cancels the pass.</param>
    /// <returns>A task that completes when every open Notification has been checked.</returns>
    internal async Task ReconcileAsync(CancellationToken ct)
    {
        foreach (FeedItem item in this.Open())
        {
            if (item.Payload is not RoomMentionHeldPayload held || !await this.StillAppliesAsync(held, ct).ConfigureAwait(false))
            {
                this.Close(item.TargetKey);
            }
        }
    }

    private static string KeyOf(string roomId, string agentId) => roomId + "|" + agentId;

    private IReadOnlyList<FeedItem> Open() => this.feedStore.Unresolved(FeedSourceType.RoomMentionHeld);

    private void Close(string key) => this.feed.Resolve(key, FeedSourceType.RoomMentionHeld);

    private async Task<bool> StillAppliesAsync(RoomMentionHeldPayload held, CancellationToken ct)
    {
        Room? room = await this.directory.GetRoomAsync(held.RoomId, ct).ConfigureAwait(false);
        if (room is null || room.Archived)
        {
            return false;
        }

        IReadOnlyList<User> members = await this.directory.GetRoomMembersAsync(held.RoomId, ct).ConfigureAwait(false);
        User? agent = members.FirstOrDefault(member => member.Kind == UserKind.Agent && string.Equals(member.Id, held.AgentId, StringComparison.Ordinal));
        if (agent is null || !this.agentExists(agent.Name))
        {
            return false;
        }

        IReadOnlyList<ChatMessage> history = await this.chatStore.ReadAllAsync(held.RoomId, ct).ConfigureAwait(false);
        int at = history.ToList().FindIndex(message => string.Equals(message.Id, held.MessageId, StringComparison.Ordinal));
        return at >= 0 && !history.Skip(at + 1).Any(message => string.Equals(message.SenderId, held.AgentId, StringComparison.Ordinal));
    }

    private void OnMessagePosted(MessagePostedEvent e)
    {
        string roomId = e.Room.Id;
        string sender = e.Message.SenderId;
        if (string.Equals(sender, KnownIds.Huddle, StringComparison.Ordinal))
        {
            return;
        }

        IEnumerable<User> agents = e.Mentions.Where(user => user.Kind == UserKind.Agent && !string.Equals(user.Id, sender, StringComparison.Ordinal));
        if (string.Equals(sender, KnownIds.Human, StringComparison.Ordinal))
        {
            // The Human mentioned the Agent, which starts a Turn: the request is no longer waiting.
            foreach (User agent in agents)
            {
                this.Close(KeyOf(roomId, agent.Id));
            }

            return;
        }

        // An Agent that posts has taken a Turn since, so its own held request is moot.
        this.Close(KeyOf(roomId, sender));
        if (!e.Budget.Exhausted)
        {
            return;
        }

        foreach (User agent in agents.DistinctBy(user => user.Id, StringComparer.Ordinal))
        {
            this.Hold(e, agent);
        }
    }

    private void Hold(MessagePostedEvent e, User agent)
    {
        string key = KeyOf(e.Room.Id, agent.Id);
        _ = this.woken.TryRemove(key, out _);
        if (this.Open().Any(item => string.Equals(item.TargetKey, key, StringComparison.Ordinal)))
        {
            return;
        }

        this.feed.Post(new FeedMessage(
            FeedSourceType.RoomMentionHeld,
            key,
            FeedActor.Huddle,
            true,
            new RoomMentionHeldPayload(e.Room.Id, e.Room.Name, e.Room.Team, agent.Name, agent.Id, e.Message.Id)));
    }

    private void OnMessageRedelivered(MessagePostedEvent e)
    {
        if (e.Budget.Exhausted)
        {
            return;
        }

        foreach (FeedItem item in this.Open())
        {
            if (item.Payload is RoomMentionHeldPayload held && string.Equals(held.RoomId, e.Room.Id, StringComparison.Ordinal))
            {
                this.Close(item.TargetKey);
            }
        }
    }
}
