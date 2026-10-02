using System.Globalization;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;

namespace Agency.Huddle.App.Questions;

/// <summary>
/// Turns the Human's answer to a waiting card into a Message, exactly once (Questions spec §6.4).
/// <see cref="QuestionStore.TryTake"/> is the "exactly once" gate: a second caller racing the same
/// card id gets <see langword="null"/> back and nothing is posted. The store has already raised
/// <see cref="RoomEvents.QuestionsChanged"/> for that take, so this class must never raise it
/// again. The answer is Human-authored, so its wording is interface copy kept here as a constant
/// format, as the Proposal outcomes are, and not a Prompt.
/// </summary>
/// <param name="questions">Holds the waiting card this call takes.</param>
/// <param name="chat">
/// Posts the answer as a Message from the Human, which is what wakes the asker through the
/// ordinary Reply Gate - no bespoke delivery of this class's own.
/// </param>
/// <param name="directory">Resolves the Human, and the asker's current Name, at post time.</param>
/// <param name="logger">Used to log what an answer or a dismissal did.</param>
internal sealed partial class QuestionService(
    QuestionStore questions,
    ChatService chat,
    ITeamDirectory directory,
    ILogger<QuestionService> logger)
{
    /// <summary>
    /// Answers the card waiting in <paramref name="roomId"/>: takes it, composes the answer and posts
    /// it as the Human. A failure to post (the Room was deleted mid-tap) is logged and returns
    /// <see langword="null"/>; the card is already gone, which is right, because the Room it belonged
    /// to may be too.
    /// </summary>
    /// <param name="roomId">The Room the card waits in.</param>
    /// <param name="id">The card id the caller expects to still be waiting.</param>
    /// <param name="answers">One answer per Question, as the card built them.</param>
    /// <param name="ct">Cancels the lookups and the post.</param>
    /// <returns>The Message text that was posted, or <see langword="null"/> when nothing was posted.</returns>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="answers"/> do not fit the card. The card builds them and the Human cannot
    /// make them, so this is a UI bug, not input to be reported.
    /// </exception>
    internal async Task<string?> AnswerAsync(string roomId, string id, IReadOnlyList<QuestionAnswer> answers, CancellationToken ct)
    {
        PendingQuestions? pending = questions.TryTake(roomId, id);
        if (pending is null)
        {
            LogGone(logger, roomId, id);
            return null;
        }

        CheckAnswers(pending, answers);

        User? asker = await directory.GetUserAsync(pending.AskerAgentId, ct);
        string text = Compose(pending, answers, asker?.Name);

        User human = await directory.GetHumanAsync(ct);
        try
        {
            _ = await chat.PostAsync(roomId, human.Id, text, ct: ct);
        }
        catch (ChatException ex)
        {
            LogPostFailed(logger, roomId, ex.Message);
            return null;
        }

        LogAnswered(logger, roomId, pending.Id, pending.Questions.Count);
        return text;
    }

    /// <summary>Dismisses the card: takes it and posts nothing, so the asker is not woken. The Human can still type.</summary>
    /// <param name="roomId">The Room the card waits in.</param>
    /// <param name="id">The card id the caller expects to still be waiting.</param>
    internal void Dismiss(string roomId, string id)
    {
        if (questions.TryTake(roomId, id) is not null)
        {
            LogDismissed(logger, roomId, id);
        }
    }

    /// <summary>
    /// Checks <paramref name="answers"/> against §6.4's table: one answer per Question; a single
    /// select has exactly one valid index; a multi select has one or more distinct valid indexes in
    /// option order; a ranking has every index exactly once.
    /// </summary>
    /// <param name="pending">The card that was answered.</param>
    /// <param name="answers">The answers to check.</param>
    private static void CheckAnswers(PendingQuestions pending, IReadOnlyList<QuestionAnswer> answers)
    {
        if (answers.Count != pending.Questions.Count)
        {
            throw new InvalidOperationException(
                $"The card holds {pending.Questions.Count} questions but {answers.Count} answers were given.");
        }

        for (int index = 0; index < answers.Count; index++)
        {
            Question question = pending.Questions[index];
            IReadOnlyList<int> chosen = answers[index].Chosen;
            bool valid = question.Kind switch
            {
                QuestionKind.SingleSelect => chosen.Count == 1 && InRange(chosen[0], question),
                QuestionKind.MultiSelect => chosen.Count > 0 && chosen.All(choice => InRange(choice, question)) && IsStrictlyAscending(chosen),
                QuestionKind.RankPriorities => chosen.Count == question.Options.Count
                    && chosen.All(choice => InRange(choice, question))
                    && chosen.Distinct().Count() == chosen.Count,
                _ => false,
            };

            if (!valid)
            {
                throw new InvalidOperationException(
                    $"The answer to question {index + 1} ({question.Kind}) does not fit its {question.Options.Count} options.");
            }
        }
    }

    /// <summary>Whether <paramref name="choice"/> is a valid option index of <paramref name="question"/>.</summary>
    /// <param name="choice">The option index.</param>
    /// <param name="question">The Question whose options it indexes.</param>
    private static bool InRange(int choice, Question question)
    {
        return choice >= 0 && choice < question.Options.Count;
    }

    /// <summary>Whether <paramref name="chosen"/> is in option order with no repeat.</summary>
    /// <param name="chosen">The chosen option indexes.</param>
    private static bool IsStrictlyAscending(IReadOnlyList<int> chosen)
    {
        for (int index = 1; index < chosen.Count; index++)
        {
            if (chosen[index] <= chosen[index - 1])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Composes the Message: each Question quoted, then its answer, then the asker Mentioned on the
    /// last line. A blank line separates a quote from its answer on purpose: a line directly after
    /// <c>&gt; question</c> is a lazy continuation of the blockquote in CommonMark, which would
    /// render the Human's answer inside the Agent's quote. A multiple choice is joined with
    /// <c>, </c>; a ranking is <c>1. … · 2. …</c> on one line, so the renderer does not turn it
    /// into a list that loses the numbers' meaning. The asker's <em>current</em> Name is used, so a
    /// renamed asker is still woken; an asker that no longer exists gets no Mention.
    /// </summary>
    /// <param name="pending">The card that was answered.</param>
    /// <param name="answers">The validated answers.</param>
    /// <param name="askerName">The asker's current Name, or <see langword="null"/> when it no longer exists.</param>
    private static string Compose(PendingQuestions pending, IReadOnlyList<QuestionAnswer> answers, string? askerName)
    {
        List<string> blocks = [];
        for (int index = 0; index < pending.Questions.Count; index++)
        {
            Question question = pending.Questions[index];
            blocks.Add($"> {question.Text}\n\n{AnswerText(question, answers[index])}");
        }

        if (askerName is not null)
        {
            blocks.Add($"@{askerName}");
        }

        return string.Join("\n\n", blocks);
    }

    /// <summary>The one-line text of one answer, by kind.</summary>
    /// <param name="question">The Question answered.</param>
    /// <param name="answer">The validated answer.</param>
    private static string AnswerText(Question question, QuestionAnswer answer)
    {
        return question.Kind == QuestionKind.RankPriorities
            ? string.Join(
                " · ",
                answer.Chosen.Select((choice, rank) => string.Create(CultureInfo.InvariantCulture, $"{rank + 1}. {question.Options[choice]}")))
            : string.Join(", ", answer.Chosen.Select(choice => question.Options[choice]));
    }

    /// <summary>Logs that an answer found nothing waiting - another caller already took the card.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="roomId">The Room the answer was for.</param>
    /// <param name="cardId">The card id the caller expected to still be waiting.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Answer for questions {CardId} in room {RoomId} found nothing waiting; another caller already took it.")]
    private static partial void LogGone(ILogger logger, string roomId, string cardId);

    /// <summary>Logs that a card was answered and posted.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="roomId">The Room the card was answered in.</param>
    /// <param name="cardId">The answered card's id.</param>
    /// <param name="questionCount">How many Questions the card held.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Answered questions {CardId} in room {RoomId}: {QuestionCount} question(s).")]
    private static partial void LogAnswered(ILogger logger, string roomId, string cardId, int questionCount);

    /// <summary>Logs that a card was dismissed without posting anything.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="roomId">The Room the card was dismissed in.</param>
    /// <param name="cardId">The dismissed card's id.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Dismissed questions {CardId} in room {RoomId}.")]
    private static partial void LogDismissed(ILogger logger, string roomId, string cardId);

    /// <summary>
    /// Logs that posting the answer failed, for example because the Room was deleted mid-tap. A
    /// Warning, not an Error: the card is already gone and the Human types the answer instead.
    /// </summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="roomId">The Room the answer could not be posted to.</param>
    /// <param name="reason">The <see cref="ChatException"/> message describing why.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not post the answer in room {RoomId}: {Reason}")]
    private static partial void LogPostFailed(ILogger logger, string roomId, string reason);
}
