using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.App.Acp.Sessions;

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
/// <param name="Team">The Team the Room is scoped to, or <see langword="null"/> for a root Room; it names the Team in the Room label.</param>
/// <param name="ReferencedItems">The Referenced items header built from the triggering Message's references (spec section 9.1), or <see langword="null"/> when there is none; it follows the Library documents block.</param>
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
    AdapterCommandCall? Command = null,
    string? Team = null,
    string? ReferencedItems = null);
