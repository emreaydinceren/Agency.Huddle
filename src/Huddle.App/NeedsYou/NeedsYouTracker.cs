using Agency.Huddle.App.Data;
using Agency.Huddle.App.Elicitation;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.Services;

namespace Agency.Huddle.App.NeedsYou;

/// <summary>
/// Which Rooms wait on the Human, and why, held in memory (Needs You spec §6.2, ADR-0050). It
/// observes <see cref="RoomEvents"/> and the Question and Form stores; none of them reports to it.
/// <para>
/// The tracker owns the Mention and Budget reasons and mirrors the Question and Form reasons from
/// their stores. <see cref="Changed"/> is raised after the lock is released, and only when the
/// visible entry for a Room actually differs.
/// </para>
/// </summary>
internal sealed class NeedsYouTracker
{
    private static readonly NeedsYouReason[] AllReasons = [NeedsYouReason.Mention, NeedsYouReason.Question, NeedsYouReason.Form, NeedsYouReason.Budget];

    private readonly Lock gate = new();
    private readonly IFeedPublisher? feed;
    private readonly Dictionary<string, RoomState> rooms = new(StringComparer.Ordinal);
    private readonly HashSet<string> heard = new(StringComparer.Ordinal);
    private readonly QuestionStore questions;
    private readonly ElicitationStore forms;
    private readonly ITeamDirectory teamDirectory;
    private readonly ILogger<NeedsYouTracker> logger;
    private readonly TimeProvider timeProvider;

    /// <summary>Creates the tracker and subscribes it to the events it works from.</summary>
    /// <param name="events">The hub whose events drive the tracker.</param>
    /// <param name="questions">The store the Question reason is mirrored from.</param>
    /// <param name="forms">The store the Form reason is mirrored from.</param>
    /// <param name="teamDirectory">Read when a Room is deleted, to drop its entry.</param>
    /// <param name="logger">Receives a failed prune.</param>
    /// <param name="timeProvider">The clock for reasons that carry no time of their own; defaults to the system clock.</param>
    /// <param name="feed">Told when a reason begins or ends (Feed spec §6.5.2); <see langword="null"/> posts nothing.</param>
    public NeedsYouTracker(
        RoomEvents events,
        QuestionStore questions,
        ElicitationStore forms,
        ITeamDirectory teamDirectory,
        ILogger<NeedsYouTracker> logger,
        TimeProvider? timeProvider = null,
        IFeedPublisher? feed = null)
    {
        this.feed = feed;
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(questions);
        ArgumentNullException.ThrowIfNull(forms);
        ArgumentNullException.ThrowIfNull(teamDirectory);
        ArgumentNullException.ThrowIfNull(logger);

        this.questions = questions;
        this.forms = forms;
        this.teamDirectory = teamDirectory;
        this.logger = logger;
        this.timeProvider = timeProvider ?? TimeProvider.System;

        events.MessagePosted += this.OnMessagePosted;
        events.MessageRedelivered += this.OnMessageRedelivered;
        events.QuestionsChanged += this.OnQuestionsChanged;
        events.ElicitationsChanged += this.OnElicitationsChanged;
        events.RoomsChanged += this.OnRoomsChanged;
    }

    /// <summary>The visible entries changed: one appeared, went away or differs. Raised outside the lock.</summary>
    public event Action? Changed;

    /// <summary>The Rooms waiting on the Human, longest wait first. Dismissed Rooms are left out.</summary>
    /// <returns>A snapshot; later changes do not alter it.</returns>
    public IReadOnlyList<NeedsYouEntry> Snapshot()
    {
        lock (this.gate)
        {
            return [.. this.rooms
                .Select(pair => NeedsYouTracker.ToEntry(pair.Key, pair.Value))
                .OfType<NeedsYouEntry>()
                .OrderBy(entry => entry.Since)
                .ThenBy(entry => entry.RoomId, StringComparer.Ordinal)];
        }
    }

    /// <summary>The reasons the Room holds now, whether or not the Human has dismissed it.</summary>
    /// <param name="roomId">The Room.</param>
    /// <returns>The reasons, or <see cref="NeedsYouReason.None"/>.</returns>
    internal NeedsYouReason ReasonsHeld(string roomId)
    {
        lock (this.gate)
        {
            return this.rooms.TryGetValue(roomId, out RoomState? state)
                ? state.Reasons.Keys.Aggregate(NeedsYouReason.None, static (flags, reason) => flags | reason)
                : NeedsYouReason.None;
        }
    }

    /// <summary>
    /// Hides a Room until something new makes it wait again. Drops the Mention reason, which the tracker
    /// owns; the Question, Form and Budget reasons stay true in their stores, and the card or Form in the
    /// Room is not touched: dismissing the reminder is not answering.
    /// </summary>
    /// <param name="roomId">The Room to hide.</param>
    public void Dismiss(string roomId)
    {
        this.Apply(roomId, state =>
        {
            state.Remove(NeedsYouReason.Mention);
            state.Dismissed = true;
        });
    }

    /// <summary>
    /// Records an unanswered Mention found in a Transcript at startup. Ignored when a live event has
    /// already been heard for the Room: that event is newer than the Transcript the scan read.
    /// </summary>
    /// <param name="roomId">The Room the Mention was found in.</param>
    /// <param name="since">When the oldest unanswered Mention was posted.</param>
    /// <param name="askedBy">The Name of whoever posted it.</param>
    internal void Seed(string roomId, DateTimeOffset since, string askedBy)
    {
        this.Apply(roomId, state => state.Set(NeedsYouReason.Mention, since, askedBy), live: false);
    }

    /// <summary>Applies the table in spec §6.2 to a posted Message.</summary>
    /// <param name="e">The Message and the Room's Budget after it.</param>
    private void OnMessagePosted(MessagePostedEvent e)
    {
        string roomId = e.Room.Id;
        if (string.Equals(e.Message.SenderId, KnownIds.Huddle, StringComparison.Ordinal))
        {
            // Huddle is neither the Human, whose post answers, nor an Agent, whose post asks (ADR-0065): it changes nothing here.
            return;
        }

        if (string.Equals(e.Message.SenderId, KnownIds.Human, StringComparison.Ordinal))
        {
            this.Apply(roomId, state =>
            {
                state.Remove(NeedsYouReason.Mention);
                state.Remove(NeedsYouReason.Budget);
            }, room: e.Room);

            return;
        }

        bool mentionsHuman = e.Mentions.Any(user => string.Equals(user.Id, KnownIds.Human, StringComparison.Ordinal));
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        this.Apply(roomId, state =>
        {
            if (mentionsHuman && !state.Has(NeedsYouReason.Mention))
            {
                state.Set(NeedsYouReason.Mention, e.Message.Timestamp, e.Message.SenderName);
                state.Dismissed = false;
            }

            if (e.Budget.Exhausted && !state.Has(NeedsYouReason.Budget))
            {
                state.Set(NeedsYouReason.Budget, now, e.Message.SenderName);
                state.Dismissed = false;
            }
        }, room: e.Room);
    }

    /// <summary>A Continue granted more Budget, so the Budget reason ends. A Mention does not: Continue is not a reply.</summary>
    /// <param name="e">The Message delivered again, with the extended Budget.</param>
    private void OnMessageRedelivered(MessagePostedEvent e)
    {
        if (!e.Budget.Exhausted)
        {
            this.Apply(e.Room.Id, state => state.Remove(NeedsYouReason.Budget));
        }
    }

    /// <summary>Mirrors the Room's Question card: set on a store or a replace, cleared when it is gone.</summary>
    /// <param name="roomId">The Room whose card changed.</param>
    private void OnQuestionsChanged(string roomId)
    {
        PendingQuestions? card = this.questions.Get(roomId);
        this.Apply(roomId, state =>
        {
            if (card is null)
            {
                state.Remove(NeedsYouReason.Question);
                return;
            }

            state.Set(NeedsYouReason.Question, card.AskedAt, card.AskerName);
            state.Dismissed = false;
        });
    }

    /// <summary>Mirrors the Room's waiting Forms: a rising count is a new ask, a falling one is not.</summary>
    /// <param name="roomId">The Room whose Forms changed.</param>
    private void OnElicitationsChanged(string roomId)
    {
        IReadOnlyList<PendingElicitation> waiting = this.forms.Get(roomId);
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        this.Apply(roomId, state =>
        {
            if (waiting.Count == 0)
            {
                state.Remove(NeedsYouReason.Form);
                state.FormCount = 0;
                return;
            }

            if (!state.Has(NeedsYouReason.Form))
            {
                state.Set(NeedsYouReason.Form, now, waiting[0].AskerName);
            }

            if (waiting.Count > state.FormCount)
            {
                state.Dismissed = false;
            }

            state.FormCount = waiting.Count;
        });
    }

    /// <summary>A Room may have been deleted; the directory read is asynchronous, so it runs apart from the event.</summary>
    private void OnRoomsChanged()
    {
        _ = this.PruneAsync();
    }

    /// <summary>Removes every entry whose Room the directory no longer lists.</summary>
    /// <returns>A task that completes when the prune has been applied or has failed and been logged.</returns>
    private async Task PruneAsync()
    {
        try
        {
            IReadOnlyList<Room> current = await this.teamDirectory.GetRoomsAsync().ConfigureAwait(false);
            HashSet<string> alive = [.. current.Select(room => room.Id)];
            string[] gone;
            lock (this.gate)
            {
                gone = [.. this.rooms.Keys.Where(id => !alive.Contains(id))];
                foreach (string id in gone)
                {
                    _ = this.rooms.Remove(id);
                }
            }

            if (gone.Length > 0)
            {
                EventRaise.Isolated(this.Changed, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.Changed)));
                this.ResolveAll(gone);
            }
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Needs You could not prune the entries of deleted Rooms.");
        }
    }

    /// <summary>
    /// Runs <paramref name="mutate"/> on the Room's state under the lock, drops the state when it ends with
    /// no reasons, and raises <see cref="Changed"/> after the lock is released if the visible entry differs.
    /// </summary>
    /// <param name="roomId">The Room to change.</param>
    /// <param name="mutate">The change.</param>
    /// <param name="live">Whether this is a live event, which stops a later startup <see cref="Seed"/> for the Room.</param>
    /// <param name="room">The Room, when the event that caused the change carried it.</param>
    private void Apply(string roomId, Action<RoomState> mutate, bool live = true, Room? room = null)
    {
        bool changed;
        List<FeedMessage> arrived = [];
        List<NeedsYouReason> left = [];
        lock (this.gate)
        {
            if (!live && this.heard.Contains(roomId))
            {
                return;
            }

            if (live)
            {
                _ = this.heard.Add(roomId);
            }

            RoomState state = this.rooms.TryGetValue(roomId, out RoomState? existing) ? existing : new RoomState();
            NeedsYouEntry? before = NeedsYouTracker.ToEntry(roomId, state);
            Dictionary<NeedsYouReason, ReasonInfo> held = new(state.Reasons);
            mutate(state);
            NeedsYouEntry? after = NeedsYouTracker.ToEntry(roomId, state);

            if (state.IsEmpty)
            {
                _ = this.rooms.Remove(roomId);
            }
            else
            {
                this.rooms[roomId] = state;
            }

            changed = !Equals(before, after);
            if (live)
            {
                arrived = [.. state.Reasons.Where(pair => !held.ContainsKey(pair.Key)).Select(pair => NeedsYouTracker.ToFeedMessage(roomId, room, pair.Key, pair.Value))];
                left = [.. held.Keys.Where(reason => !state.Reasons.ContainsKey(reason))];
            }
        }

        if (changed)
        {
            EventRaise.Isolated(this.Changed, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.Changed)));
        }

        this.PublishFeed(roomId, arrived, left);
    }

    /// <summary>Posts the reasons that arrived and resolves the ones that left. The Feed never decides; it is told after the lock is released.</summary>
    /// <param name="roomId">The Room.</param>
    /// <param name="arrived">The items for reasons the Room did not hold before.</param>
    /// <param name="left">The reasons the Room no longer holds.</param>
    private void PublishFeed(string roomId, List<FeedMessage> arrived, List<NeedsYouReason> left)
    {
        if (this.feed is null)
        {
            return;
        }

        foreach (FeedMessage message in arrived)
        {
            this.feed.Post(message);
        }

        foreach (NeedsYouReason reason in left)
        {
            this.feed.Resolve(roomId, NeedsYouTracker.SourceTypeOf(reason));
        }
    }

    /// <summary>Resolves all four Room reasons for each Room that no longer exists.</summary>
    /// <param name="roomIds">The deleted Rooms.</param>
    private void ResolveAll(IEnumerable<string> roomIds)
    {
        if (this.feed is null)
        {
            return;
        }

        foreach (string roomId in roomIds)
        {
            foreach (NeedsYouReason reason in AllReasons)
            {
                this.feed.Resolve(roomId, NeedsYouTracker.SourceTypeOf(reason));
            }
        }
    }

    /// <summary>The Feed source type that records a reason.</summary>
    /// <param name="reason">A single reason.</param>
    private static FeedSourceType SourceTypeOf(NeedsYouReason reason) => reason switch
    {
        NeedsYouReason.Mention => FeedSourceType.RoomMention,
        NeedsYouReason.Question => FeedSourceType.RoomQuestion,
        NeedsYouReason.Form => FeedSourceType.RoomForm,
        _ => FeedSourceType.RoomBudget,
    };

    /// <summary>Builds the Notification for a reason that has just begun. A Budget pause is Huddle's; the others are the Agent's.</summary>
    /// <param name="roomId">The Room.</param>
    /// <param name="room">The Room, when the event carried it; its Name and Team ride along.</param>
    /// <param name="reason">The reason.</param>
    /// <param name="info">Who began it.</param>
    private static FeedMessage ToFeedMessage(string roomId, Room? room, NeedsYouReason reason, ReasonInfo info) => new(
        NeedsYouTracker.SourceTypeOf(reason),
        roomId,
        reason == NeedsYouReason.Budget ? FeedActor.Huddle : new FeedActor(FeedActorKind.Agent, info.By),
        true,
        new RoomWaitPayload(roomId, room?.Name, room?.Team));

    /// <summary>The entry a Room shows, or <see langword="null"/> when it has no reasons or is dismissed.</summary>
    /// <param name="roomId">The Room's id.</param>
    /// <param name="state">The Room's state.</param>
    /// <returns>The entry, or <see langword="null"/>.</returns>
    private static NeedsYouEntry? ToEntry(string roomId, RoomState state)
    {
        if (state.IsEmpty || state.Dismissed)
        {
            return null;
        }

        KeyValuePair<NeedsYouReason, ReasonInfo> earliest = state.Reasons.MinBy(pair => pair.Value.Since);
        NeedsYouReason all = state.Reasons.Keys.Aggregate(NeedsYouReason.None, static (flags, reason) => flags | reason);
        return new NeedsYouEntry(roomId, all, earliest.Value.Since, earliest.Value.By);
    }

    /// <summary>When a reason began, and who began it.</summary>
    /// <param name="Since">When it began.</param>
    /// <param name="By">Who began it.</param>
    private sealed record ReasonInfo(DateTimeOffset Since, string By);

    /// <summary>What the tracker knows about one Room. Only touched under the tracker's lock.</summary>
    private sealed class RoomState
    {
        public Dictionary<NeedsYouReason, ReasonInfo> Reasons { get; } = [];

        public bool Dismissed { get; set; }

        public int FormCount { get; set; }

        public bool IsEmpty => this.Reasons.Count == 0;

        public bool Has(NeedsYouReason reason) => this.Reasons.ContainsKey(reason);

        public void Set(NeedsYouReason reason, DateTimeOffset since, string by)
        {
            this.Reasons[reason] = new ReasonInfo(since, by);
        }

        public void Remove(NeedsYouReason reason)
        {
            _ = this.Reasons.Remove(reason);
        }
    }
}
