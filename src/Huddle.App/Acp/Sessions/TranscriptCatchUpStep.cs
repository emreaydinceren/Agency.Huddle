using Agency.Huddle.App.Prompts;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Acp.Sessions;

/// <summary>
/// D24, RS §6.5: the first Turn after any open of one <see cref="RoomSession"/> reads the Room's Transcript and
/// owes the fresh-start note. One instance per session, with no lock: it runs on the session's consumer exactly as
/// the fields it owns did on <see cref="RoomSession"/> (the open that calls <see cref="MarkOpened"/> happens-before
/// the consumer's <see cref="AddAsync"/>, which is the only place the flag is consulted and cleared).
/// </summary>
internal sealed class TranscriptCatchUpStep
{
    private readonly IRoomSessionOwner owner;
    private readonly AcpOptions options;
    private readonly ISessionSettings? sessionSettings;
    private readonly LastCatchUpStore? lastCatchUp;
    private readonly IPromptSource prompts;
    private readonly ILogger logger;
    private readonly string? roomId;
    private readonly Func<ChatMessage, bool> isOwn;

    // The note the current Turn owes after a fresh start; set by AddAsync and cleared by TakeNote.
    // Touched only from the single consumer thread.
    private FreshStartNote? noteOwed;

    // D24: consulted and cleared only from AddAsync, which the single consumer thread
    // always calls after the open that set them (OpenCoreAsync happens-before ProcessWorkItemAsync
    // in RunConsumerAsync's own sequence), so these need no lock of their own - the same reasoning
    // knownRoomNames documents on PersonaRunner.
    private bool firstTurnPending;
    private bool resumedOnLastOpen;
    private string? resumedAfterMessageId;

    /// <summary>Initializes a new instance of the <see cref="TranscriptCatchUpStep"/> class.</summary>
    /// <param name="owner">Reads the Transcript and names the Persona.</param>
    /// <param name="options">The Persona's ACP options, used when there are no resolved Session settings.</param>
    /// <param name="sessionSettings">The resolved Session settings, or <see langword="null"/> to use <paramref name="options"/>.</param>
    /// <param name="lastCatchUp">Receives the first ordinary Turn's prompt after a fresh start, or <see langword="null"/> to write no file.</param>
    /// <param name="prompts">Resolves every <c>turn.*</c> prompt's current text.</param>
    /// <param name="logger">Where the step logs.</param>
    /// <param name="roomId">The Room of the session, or <see langword="null"/> for a shared session.</param>
    /// <param name="isOwn">Whether a Message is this Agent's own.</param>
    public TranscriptCatchUpStep(
        IRoomSessionOwner owner,
        AcpOptions options,
        ISessionSettings? sessionSettings,
        LastCatchUpStore? lastCatchUp,
        IPromptSource prompts,
        ILogger logger,
        string? roomId,
        Func<ChatMessage, bool> isOwn)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(prompts);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(isOwn);

        this.owner = owner;
        this.options = options;
        this.sessionSettings = sessionSettings;
        this.lastCatchUp = lastCatchUp;
        this.prompts = prompts;
        this.logger = logger;
        this.roomId = roomId;
        this.isOwn = isOwn;
    }

    /// <summary>Records that the session opened: its next ordinary Turn is the first after an open.</summary>
    /// <param name="resumed">Whether this open resumed a stored session rather than starting a fresh one.</param>
    /// <param name="resumedAfterId">The Message id the resumed session had seen up to, or <see langword="null"/>.</param>
    public void MarkOpened(bool resumed, string? resumedAfterId)
    {
        this.firstTurnPending = true;
        this.resumedOnLastOpen = resumed;
        this.resumedAfterMessageId = resumedAfterId;
    }

    /// <summary>Returns the fresh-start note the finished Turn owed, if any, and clears it.</summary>
    /// <returns>The note, or <see langword="null"/> when none is owed.</returns>
    public FreshStartNote? TakeNote()
    {
        FreshStartNote? note = this.noteOwed;
        this.noteOwed = null;
        return note;
    }

    /// <summary>
    /// D24, RS §6.5: the first Turn after any open (per-Room only) reads the Room's Transcript,
    /// after the File Changes collect and before the locked check-and-publish (D22 correction 3).
    /// </summary>
    /// <remarks>
    /// Open and this read must both finish before the watchdog is armed, so neither counts as
    /// Adapter silence. The first-Turn flag is cleared here regardless of outcome: RS §9 E-3
    /// (refused or timed out) and a Greeting or a Turn with no TriggerMessageId all still consume
    /// this Turn's "first after open" opportunity (D24 correction 19) without ever reading anything.
    /// A Command Turn sends only the bare command, so it reads no Transcript and must not use the
    /// opportunity up: the Messages this session has not seen are still unseen, and the next
    /// ordinary Turn is the one that needs them (Commands spec, section 6.6).
    /// </remarks>
    /// <param name="item">The item about to be prompted.</param>
    /// <param name="freshStartNotePending">Whether the session owes a fresh-start note on its first ordinary Turn.</param>
    /// <param name="ct">The run's own cancellation token.</param>
    /// <returns>The item, with its Transcript Catch-up set when one was read.</returns>
    public async Task<WorkItem> AddAsync(WorkItem item, bool freshStartNotePending, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (this.firstTurnPending && item.Kind != WorkItemKind.Command)
        {
            this.firstTurnPending = false;
            if (freshStartNotePending)
            {
                this.noteOwed = new FreshStartNote(item.RoomId, "asked", 0, 0, false);
            }

            if (this.roomId is not null && item.Kind == WorkItemKind.Message && item.TriggerMessageId is { } triggerId)
            {
                SessionSettings settings = this.sessionSettings?.Current ?? new SessionSettings(this.options.TranscriptCatchUpMessages, this.options.TranscriptCatchUpTokens);
                TranscriptTail? tail = null;
                try
                {
                    tail = await this.owner.ReadTranscriptAsync(
                        item.RoomId,
                        this.resumedOnLastOpen ? this.resumedAfterMessageId : null,
                        triggerId,
                        settings.CatchUpMessages,
                        ct).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    this.logger.LogWarning(
                        ex, "Persona '{PersonaName}' Transcript read for room {RoomId} failed.", this.owner.PersonaName, item.RoomId);
                }

                if (tail is not null)
                {
                    // D27 correction 11: own-post lines are dropped only when the Transcript is
                    // actually set on this Turn's WorkItem - that range already holds those posts,
                    // whether or not it turned out to hold any Messages. A refused or timed-out read
                    // (RS §9 E-3, tail null) leaves OwnPostLines untouched, so they still render
                    // through the ordinary catch-up path below.
                    item = item with
                    {
                        Transcript = this.BuildCatchUp(tail, settings),
                        OwnPostLines = null,
                    };
                }
                else if (this.noteOwed is not null)
                {
                    this.noteOwed = this.noteOwed with { ReadFailed = true };
                }
            }
        }

        return item;
    }

    /// <summary>Trims a Transcript tail to the token budget (LR-R11) and counts what the trim dropped on top of the tail's own omitted count.</summary>
    /// <param name="tail">The tail the owner returned.</param>
    /// <param name="settings">The settings snapshot that set the Message cap for this read.</param>
    /// <returns>The catch-up for the Turn.</returns>
    public TranscriptCatchUp BuildCatchUp(TranscriptTail tail, SessionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(tail);
        ArgumentNullException.ThrowIfNull(settings);

        CatchUpTrimResult result = CatchUpTrim.Apply(tail.Messages, this.isOwn, settings.CatchUpTokens, this.options.CatchUpKeepOwnMessages);
        if (this.noteOwed is not null)
        {
            this.noteOwed = this.noteOwed with { MessageCount = result.Kept.Count, EstimatedTokens = result.EstimatedTokens };
        }

        return new TranscriptCatchUp(this.resumedOnLastOpen, result.Kept, tail.Omitted + result.Dropped, result.DroppedOwn);
    }

    /// <summary>Writes the whole prompt text of the first ordinary Turn after a fresh start to the last-catch-up file (C3). A failure is logged and never fails the Turn.</summary>
    /// <param name="item">The item about to be prompted, fully enriched.</param>
    public void WriteLastCatchUp(WorkItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (this.noteOwed is null || this.lastCatchUp is null)
        {
            return;
        }

        try
        {
            this.lastCatchUp.Write(this.owner.PersonaName, item.RoomId, TurnPromptFramer.BuildPrompt(item, this.prompts));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            this.logger.LogWarning(
                ex, "Persona '{PersonaName}' could not write the last catch-up for room {RoomId}.", this.owner.PersonaName, item.RoomId);
        }
    }
}
