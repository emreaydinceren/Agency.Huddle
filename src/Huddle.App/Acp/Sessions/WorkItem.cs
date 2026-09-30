using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Library;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Acp.Sessions;

/// <summary>What kind of Turn a <see cref="WorkItem"/> starts.</summary>
internal enum WorkItemKind
{
    /// <summary>An ordinary Turn, triggered by a delivered Message.</summary>
    Message,

    /// <summary>The Chief of Staff's unprompted first Message to the Human (Spec §6.14). No triggering Message and no catch-up.</summary>
    Greeting,

    /// <summary>
    /// An Adapter command a Human addressed to this Teammate by Mention, for example <c>@Nova /compact</c>
    /// (Commands spec, section 6.5). The prompt is the bare command and nothing else, and the Turn
    /// neither collects File Changes nor drains Catch-up.
    /// </summary>
    Command,
}

/// <summary>One Turn's Room, sender, text, any catch-up context, its kind and its File Changes report.</summary>
/// <param name="RoomId">The Room this Turn belongs to.</param>
/// <param name="RoomName">That Room's current name, already run through <see cref="RoomLabels.Distinguish"/>.</param>
/// <param name="SenderName">The triggering Message's sender name, empty for a <see cref="WorkItemKind.Greeting"/>.</param>
/// <param name="Text">The triggering Message's text, empty for a <see cref="WorkItemKind.Greeting"/>.</param>
/// <param name="MissedMessages">Earlier Messages the Agent was not Mentioned in, carried as context only.</param>
/// <param name="Kind">Whether this is an ordinary Turn or the Greeting.</param>
/// <param name="FileChanges">The Turn's collected File Changes report, or <see langword="null"/> when File Changes is off or this is a Greeting.</param>
/// <param name="TriggerMessageId">
/// The id of the Message that started this Turn (finding P-16), set from <see cref="Agency.Huddle.Contracts.MessagePosted"/>'s
/// own <c>Message.Id</c> by the read loop. <see langword="null"/> for a <see cref="WorkItemKind.Greeting"/>, which reads no
/// Transcript because it has no triggering Message.
/// </param>
/// <param name="Transcript">
/// This Room Session's first-Turn Transcript Catch-up (RS §6.5), or <see langword="null"/> when this
/// is not that Turn, the read was refused or timed out, or the Room's first Turn ever has nothing to
/// show. When set with a non-empty <see cref="TranscriptCatchUp.Messages"/> it replaces
/// <see cref="MissedMessages"/> on this Turn only.
/// </param>
/// <param name="OwnPostLines">
/// This Agent's own earlier <c>post_message</c> calls into this Room from a Turn in another Room (RS
/// §6.7, finding P-7), drained by the read loop's own <see cref="OwnPosts.Take"/> call, beside
/// <c>TakeCatchUp</c>. <see langword="null"/> when nothing was recorded. Dropped by the Room Session
/// when <see cref="Transcript"/> is set on this Turn: that range already holds those posts.
/// </param>
/// <param name="LibraryDocuments">
/// The absolute Library document paths mentioned in this Turn's Message and Catch-up (Spec §6.14), or
/// <see langword="null"/> when none were found or the Library is off.
/// </param>
/// <param name="Command">The command to run, in the Adapter's own casing; set exactly when <paramref name="Kind"/> is <see cref="WorkItemKind.Command"/>.</param>
internal sealed record WorkItem(
    string RoomId,
    string RoomName,
    string SenderName,
    string Text,
    IReadOnlyList<CaughtUpMessage> MissedMessages,
    WorkItemKind Kind = WorkItemKind.Message,
    FileChangesReport? FileChanges = null,
    string? TriggerMessageId = null,
    TranscriptCatchUp? Transcript = null,
    IReadOnlyList<string>? OwnPostLines = null,
    LibraryDocumentsReport? LibraryDocuments = null,
    AdapterCommandCall? Command = null);

/// <summary>One earlier Message the Agent was not Mentioned in, carried as catch-up context only.</summary>
/// <param name="SenderName">Who sent it.</param>
/// <param name="Text">What it said.</param>
internal sealed record CaughtUpMessage(string SenderName, string Text);

/// <summary>
/// A Room Session's first-Turn Transcript Catch-up (RS §6.5): the range of a Room's Messages that
/// replaces the in-memory catch-up buffer on that one Turn, because a fresh or resumed session has
/// not seen them.
/// </summary>
/// <param name="Resumed">Whether this session was resumed (renders <c>turn.transcriptResumedHeader</c>) rather than opened fresh (<c>turn.transcriptHeader</c>).</param>
/// <param name="Messages">The Messages in range, oldest first, ending before the triggering Message (RS principle 4).</param>
/// <param name="Omitted">How many earlier Messages in the requested range were left out, per <see cref="Agency.Huddle.Contracts.TranscriptTail.Omitted"/>.</param>
internal sealed record TranscriptCatchUp(bool Resumed, IReadOnlyList<ChatMessage> Messages, int Omitted);

/// <summary>One queued Turn, with the sequence number a Stop compares against.</summary>
/// <param name="Sequence">This item's position in the Persona-wide arrival order (finding P-4).</param>
/// <param name="Item">The Turn to run.</param>
internal sealed record QueuedWork(long Sequence, WorkItem Item);
