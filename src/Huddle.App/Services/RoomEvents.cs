namespace Agency.Huddle.App.Services;

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
    /// the Human extended the Room's Budget. <c>AgentGateway</c> is the subscriber that delivers it, and no
    /// Blazor component may subscribe: it would render the Message a second time. <c>NeedsYouTracker</c> also
    /// subscribes, to end a Room's Budget reason; it draws nothing, so it cannot render twice.
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
    /// A Room's pause on its agent-message Budget ended: the Human extended it with Continue, or the Human spoke in a Room that was
    /// spent. Carries the Room id. <c>TurnRedelivery</c> subscribes, to send the Turns held for the Room again (Turn Queue spec, section 5.4).
    /// </summary>
    public event Action<string>? BudgetUnpaused;

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
    /// A Fresh Start note in a Room was stored or dismissed. Carries the Room id, not the note, for the same
    /// reason <see cref="QuestionsChanged"/> does: a subscriber rereads the current notes.
    /// </summary>
    public event Action<string>? FreshStartNotesChanged;

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
        EventRaise.Isolated(handlers, e, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", eventName));
    }

    public void PublishRoomsChanged()
    {
        EventRaise.Isolated(this.RoomsChanged, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.RoomsChanged)));
    }

    /// <summary>Publishes <see cref="BudgetUnpaused"/> for <paramref name="roomId"/>.</summary>
    /// <param name="roomId">The Room whose pause ended.</param>
    public void PublishBudgetUnpaused(string roomId)
    {
        this.PublishRoomId(this.BudgetUnpaused, roomId, nameof(this.BudgetUnpaused));
    }

    /// <summary>Publishes <see cref="DraftChanged"/> for <paramref name="roomId"/>.</summary>
    /// <param name="roomId">The Room whose Drafts changed.</param>
    public void PublishDraftChanged(string roomId)
    {
        this.PublishRoomId(this.DraftChanged, roomId, nameof(this.DraftChanged));
    }

    /// <summary>
    /// Publishes <see cref="ProposalChanged"/> for <paramref name="roomId"/>. Called by
    /// <see cref="Agents.ProposalStore"/> after its own lock is released, never while held.
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

    /// <summary>Publishes <see cref="FreshStartNotesChanged"/> for <paramref name="roomId"/>.</summary>
    /// <param name="roomId">The Room whose Fresh Start notes changed.</param>
    internal void PublishFreshStartNotesChanged(string roomId)
    {
        this.PublishRoomId(this.FreshStartNotesChanged, roomId, nameof(this.FreshStartNotesChanged));
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
        EventRaise.Isolated(handlers, roomId, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", eventName));
    }
}