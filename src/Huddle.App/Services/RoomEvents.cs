using Agency.Huddle.App.Data;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Services;

/// <summary>A Message that reached a Room's Transcript, with who was in the Room and who it names.</summary>
/// <param name="Room">The Room the Message was posted in.</param>
/// <param name="Message">The Message itself.</param>
/// <param name="Members">The Room's Members at the moment of posting.</param>
/// <param name="Mentions">The Members the Message names.</param>
/// <param name="Budget">The Room's Budget after the post.</param>
/// <param name="WithheldFromAgentId">
/// The Agent this Message is not delivered to, or <see langword="null"/> when every Agent Member
/// except the sender receives it. Set only for the Human's answer to a form that Agent opened: its
/// Turn is still open and takes the answer as the tool's own result, so a delivery would queue a
/// second Turn. Read only by <c>AgentGateway</c>; the Room view shows the Message either way.
/// </param>
public sealed record MessagePostedEvent(
    Room Room,
    ChatMessage Message,
    IReadOnlyList<User> Members,
    IReadOnlyList<User> Mentions,
    RoomBudget Budget,
    string? WithheldFromAgentId = null)
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

    /// <summary>
    /// A Room's pending <c>Proposal</c> changed - one was stored, replaced, taken (Approved or
    /// Declined), or dropped (archived, deleted, or restarted). Carries the Room id, not the
    /// Proposal itself, for the same reason <see cref="DraftChanged"/> does: a subscriber re-reads
    /// the current Proposal rather than trusting a value that may already be stale.
    /// </summary>
    public event Action<string>? ProposalChanged;

    /// <summary>
    /// The card of Questions waiting in a Room changed - one was stored, replaced, taken (answered
    /// or dismissed), or dropped (a Human Message, archive or delete). Carries the Room id, not the
    /// card, for the same reason <see cref="ProposalChanged"/> does. The Room view is the only
    /// subscriber; an Agent is never told, because the answer reaches it as a Message.
    /// </summary>
    public event Action<string>? QuestionsChanged;

    /// <summary>
    /// The forms waiting for the Human in a Room changed - one arrived, was taken (answered or skipped),
    /// or was dropped (its request was cancelled, a typed Human Message, an archive or a delete). Carries
    /// the Room id, not the cards, for the same reason <see cref="QuestionsChanged"/> does. The Room view
    /// is the only subscriber; an Agent is never told, because the answer reaches it as the form's result.
    /// </summary>
    public event Action<string>? ElicitationsChanged;

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

    /// <summary>
    /// Publishes <see cref="ProposalChanged"/> for <paramref name="roomId"/>. Called by
    /// <see cref="Teammates.ProposalStore"/> after its own lock is released, never while held.
    /// </summary>
    /// <param name="roomId">The Room whose pending Proposal changed.</param>
    internal void PublishProposalChanged(string roomId)
    {
        this.PublishRoomId(this.ProposalChanged, roomId, nameof(this.ProposalChanged));
    }

    /// <summary>
    /// Publishes <see cref="QuestionsChanged"/> for <paramref name="roomId"/>. Called by
    /// <see cref="Questions.QuestionStore"/> after its own lock is released, never while held.
    /// </summary>
    /// <param name="roomId">The Room whose waiting Questions changed.</param>
    internal void PublishQuestionsChanged(string roomId)
    {
        this.PublishRoomId(this.QuestionsChanged, roomId, nameof(this.QuestionsChanged));
    }

    /// <summary>
    /// Publishes <see cref="ElicitationsChanged"/> for <paramref name="roomId"/>. Called by
    /// <see cref="Elicitation.ElicitationStore"/> after its own lock is released, never while held.
    /// </summary>
    /// <param name="roomId">The Room whose waiting forms changed.</param>
    internal void PublishElicitationsChanged(string roomId)
    {
        this.PublishRoomId(this.ElicitationsChanged, roomId, nameof(this.ElicitationsChanged));
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