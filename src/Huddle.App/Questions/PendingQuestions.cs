namespace Agency.Huddle.App.Questions;

/// <summary>
/// The one to three Questions waiting on one card in one Room, held in memory by
/// <see cref="QuestionStore"/>. Public because <c>QuestionCard.razor</c> renders it directly, the
/// same reason <c>Proposal</c> is public.
/// </summary>
/// <param name="Id">This card's own id, a Guid in <c>"N"</c> format, so a tap on a replaced card finds nothing.</param>
/// <param name="RoomId">The Room this card is waiting in - the store's key.</param>
/// <param name="AskerAgentId">The stable user id of the Agent that asked; a Name can be renamed out from under it.</param>
/// <param name="AskerName">The asker's Name at ask time. The posted Mention re-resolves it.</param>
/// <param name="Questions">One to three Questions.</param>
/// <param name="AskedAt">When they were asked, from the injected <see cref="TimeProvider"/>.</param>
public sealed record PendingQuestions(
    string Id,
    string RoomId,
    string AskerAgentId,
    string AskerName,
    IReadOnlyList<Question> Questions,
    DateTimeOffset AskedAt);
