using Agency.Huddle.App.Data;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Services;

public sealed record MessagePostedEvent(
    Room Room,
    ChatMessage Message,
    IReadOnlyList<User> Members,
    IReadOnlyList<User> Mentions,
    RoomBudget Budget)
{
    /// <summary>
    /// Whether <paramref name="member"/> is an Agent this Message is delivered to. An Agent never
    /// receives its own Message - structural, and what stops naive clients echo-looping.
    /// </summary>
    /// <param name="member">The candidate recipient.</param>
    /// <param name="senderId">The id of the User who sent the Message.</param>
    /// <returns><see langword="true"/> when this Message is delivered to <paramref name="member"/>.</returns>
    internal static bool IsRecipient(User member, string senderId) =>
        member.Kind == UserKind.Agent && !string.Equals(member.Id, senderId, StringComparison.Ordinal);
}

public sealed class RoomEvents
{
    private readonly ILogger<RoomEvents> logger;

    public RoomEvents(ILogger<RoomEvents> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        this.logger = logger;
    }

    public event Action<MessagePostedEvent>? MessagePosted;

    /// <summary>
    /// A Message already posted and already on screen, being delivered to the Agents again because
    /// the Human extended the Room's Budget. <c>AgentGateway</c> is the only subscriber, and must
    /// stay so: a Blazor component that handled this would render the Message a second time.
    /// </summary>
    public event Action<MessagePostedEvent>? MessageRedelivered;

    public event Action? RoomsChanged;

    /// <summary>
    /// A Room's Drafts changed — one grew, one's tool activity changed, or one was removed. Carries
    /// the Room id, not the Draft itself, so a subscriber re-reads <c>Drafts.ForRoom</c> for the
    /// current picture rather than trusting a value that may already be stale by the time it runs.
    /// <para>
    /// The inverse of <see cref="MessageRedelivered"/>'s warning above: the Room view is the only
    /// subscriber, and must stay so. <c>AgentGateway</c> must never deliver a Draft to an Agent — an
    /// Agent reacting to another Agent's half-finished text would be reacting to something that was
    /// never said, a strictly worse version of the echo loop that rules.md's "An Agent never receives
    /// its own Message" exists to prevent.
    /// </para>
    /// </summary>
    public event Action<string>? DraftChanged;

    public void PublishMessagePosted(MessagePostedEvent e)
    {
        this.Publish(this.MessagePosted, e, nameof(this.MessagePosted));
    }

    public void PublishMessageRedelivered(MessagePostedEvent e)
    {
        this.Publish(this.MessageRedelivered, e, nameof(this.MessageRedelivered));
    }

    private void Publish(Action<MessagePostedEvent>? handlers, MessagePostedEvent e, string eventName)
    {
        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList())
        {
            try
            {
                ((Action<MessagePostedEvent>)handler).Invoke(e);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "A {Event} handler threw and was skipped.", eventName);
            }
        }
    }

    public void PublishRoomsChanged()
    {
        if (this.RoomsChanged is not { } handlers)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList())
        {
            try
            {
                ((Action)handler).Invoke();
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.RoomsChanged));
            }
        }
    }

    /// <summary>Publishes <see cref="DraftChanged"/> for <paramref name="roomId"/>.</summary>
    /// <param name="roomId">The Room whose Drafts changed.</param>
    public void PublishDraftChanged(string roomId)
    {
        this.PublishRoomId(this.DraftChanged, roomId, nameof(this.DraftChanged));
    }

    private void PublishRoomId(Action<string>? handlers, string roomId, string eventName)
    {
        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList())
        {
            try
            {
                ((Action<string>)handler).Invoke(roomId);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "A {Event} handler threw and was skipped.", eventName);
            }
        }
    }
}