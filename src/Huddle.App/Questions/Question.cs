namespace Agency.Huddle.App.Questions;

/// <summary>
/// How a <see cref="Question"/> is answered (Questions spec §6.1). The wire values
/// <c>single_select</c>, <c>multi_select</c> and <c>rank_priorities</c> are parsed into this once,
/// in <c>AskHumanTool</c>, so nothing downstream handles a string.
/// </summary>
public enum QuestionKind
{
    /// <summary>Exactly one option is chosen.</summary>
    SingleSelect,

    /// <summary>One or more options are chosen.</summary>
    MultiSelect,

    /// <summary>Every option is placed in an order, most important first.</summary>
    RankPriorities,
}

/// <summary>One multiple-choice Question an Agent puts to the Human.</summary>
/// <param name="Text">The question, as the Agent wrote it.</param>
/// <param name="Options">Two to four short options.</param>
/// <param name="Kind">How the Human answers it.</param>
public sealed record Question(string Text, IReadOnlyList<string> Options, QuestionKind Kind);

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

/// <summary>The Human's answer to one Question: option indexes, in the order that matters.</summary>
/// <param name="Chosen">The chosen option indexes. For a ranking, every index, most important first.</param>
public sealed record QuestionAnswer(IReadOnlyList<int> Chosen);

/// <summary>The outcome of <see cref="QuestionStore.TryPut"/> against Questions spec §8.1.</summary>
internal enum QuestionPutResult
{
    /// <summary>No card was waiting in the Room; the new one is stored.</summary>
    Stored,

    /// <summary>The same Agent's earlier card in this Room was replaced.</summary>
    Replaced,

    /// <summary>Another Agent's card is already waiting in this Room; the new one was refused.</summary>
    Refused,
}

/// <summary>What <see cref="QuestionStore.TryPut"/> did, and, only when refused, the card already waiting.</summary>
/// <param name="Result">Which of the three outcomes occurred.</param>
/// <param name="Existing">The card already waiting when <paramref name="Result"/> is <see cref="QuestionPutResult.Refused"/>; otherwise <see langword="null"/>.</param>
internal sealed record QuestionPut(QuestionPutResult Result, PendingQuestions? Existing);
