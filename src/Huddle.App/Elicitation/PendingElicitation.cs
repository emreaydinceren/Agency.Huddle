using Agency.Huddle.Acp.Abstractions;

namespace Agency.Huddle.App.Elicitation;

/// <summary>
/// One form waiting for the Human in one Room, held in memory by <see cref="ElicitationStore"/>. Public
/// because the Room view renders it directly, the same reason <c>PendingQuestions</c> is public.
/// </summary>
/// <param name="Id">This card's own id, a Guid in <c>"N"</c> format, so a tap on a card that is gone finds nothing.</param>
/// <param name="Sequence">Where this card came in, from a counter that only rises: the order the Room lists its cards in, whatever the clock does.</param>
/// <param name="RoomId">The Room this card waits in.</param>
/// <param name="AskerAgentId">The stable user id of the Agent that asked; a Name can be renamed out from under it.</param>
/// <param name="AskerName">The asker's Name when the request arrived.</param>
/// <param name="Form">The form to show.</param>
/// <param name="Completion">
/// What the request waiting on this card is waiting for: completed with the Human's answer, a skip, or a
/// cancellation. Created to run continuations asynchronously, so completing it never runs the asker's
/// code on the caller's thread.
/// </param>
public sealed record PendingElicitation(
    string Id,
    long Sequence,
    string RoomId,
    string AskerAgentId,
    string AskerName,
    ElicitationForm Form,
    TaskCompletionSource<ElicitationResult> Completion);
