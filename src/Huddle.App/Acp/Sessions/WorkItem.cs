using Agency.Huddle.App.FileChanges;

namespace Agency.Huddle.App.Acp.Sessions;

/// <summary>What kind of Turn a <see cref="WorkItem"/> starts.</summary>
internal enum WorkItemKind
{
    /// <summary>An ordinary Turn, triggered by a delivered Message.</summary>
    Message,

    /// <summary>The Chief of Staff's unprompted first Message to the Human (Spec §6.14). No triggering Message and no catch-up.</summary>
    Greeting,
}

/// <summary>One Turn's Room, sender, text, any catch-up context, its kind and its File Changes report.</summary>
/// <param name="RoomId">The Room this Turn belongs to.</param>
/// <param name="RoomName">That Room's current name, already run through <see cref="RoomLabels.Distinguish"/>.</param>
/// <param name="SenderName">The triggering Message's sender name, empty for a <see cref="WorkItemKind.Greeting"/>.</param>
/// <param name="Text">The triggering Message's text, empty for a <see cref="WorkItemKind.Greeting"/>.</param>
/// <param name="MissedMessages">Earlier Messages the Agent was not Mentioned in, carried as context only.</param>
/// <param name="Kind">Whether this is an ordinary Turn or the Greeting.</param>
/// <param name="FileChanges">The Turn's collected File Changes report, or <see langword="null"/> when File Changes is off or this is a Greeting.</param>
internal sealed record WorkItem(
    string RoomId,
    string RoomName,
    string SenderName,
    string Text,
    IReadOnlyList<CaughtUpMessage> MissedMessages,
    WorkItemKind Kind = WorkItemKind.Message,
    FileChangesReport? FileChanges = null);

/// <summary>One earlier Message the Agent was not Mentioned in, carried as catch-up context only.</summary>
/// <param name="SenderName">Who sent it.</param>
/// <param name="Text">What it said.</param>
internal sealed record CaughtUpMessage(string SenderName, string Text);

/// <summary>One queued Turn, with the sequence number a Stop compares against.</summary>
/// <param name="Sequence">This item's position in the Persona-wide arrival order (finding P-4).</param>
/// <param name="Item">The Turn to run.</param>
internal sealed record QueuedWork(long Sequence, WorkItem Item);
