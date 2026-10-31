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
