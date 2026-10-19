using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Acp.Sessions;

/// <summary>
/// A Room Session's first-Turn Transcript Catch-up (RS §6.5): the range of a Room's Messages that
/// replaces the in-memory catch-up buffer on that one Turn, because a fresh or resumed session has
/// not seen them.
/// </summary>
/// <param name="Resumed">Whether this session was resumed (renders <c>turn.transcriptResumedHeader</c>) rather than opened fresh (<c>turn.transcriptHeader</c>).</param>
/// <param name="Messages">The Messages in range, oldest first, ending before the triggering Message (RS principle 4).</param>
/// <param name="Omitted">How many earlier Messages in the requested range were left out, per <see cref="Agency.Huddle.Contracts.TranscriptTail.Omitted"/>.</param>
internal sealed record TranscriptCatchUp(bool Resumed, IReadOnlyList<ChatMessage> Messages, int Omitted);
