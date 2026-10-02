using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Elicitation;

/// <summary>
/// Turns the Human's answer to a waiting form into a Transcript Message and into the result the waiting
/// request returns to the agent, exactly once (elicitation bridge, decisions E-4, E-5 and E-6).
/// <see cref="ElicitationStore.TryTake"/> is the "exactly once" gate: a second caller racing the same card
/// id gets nothing back and nothing is posted. The Message is the Human's own, posted through
/// <see cref="ChatService.PostHumanAnswerAsync"/> so it mentions nobody and is withheld from the asker -
/// whose Turn is still open and takes the answer as the tool's own result. The request is answered after
/// the Message is posted, and always: whatever goes wrong while posting, the agent is never left waiting.
/// </summary>
/// <param name="store">Holds the waiting cards this service takes.</param>
/// <param name="chat">Posts the answer as a Message from the Human, withheld from the asker.</param>
/// <param name="directory">Resolves the Human at post time.</param>
/// <param name="logger">Used to log what an answer or a skip did.</param>
internal sealed partial class ElicitationService(
    ElicitationStore store,
    ChatService chat,
    ITeamDirectory directory,
    ILogger<ElicitationService> logger)
{
    /// <summary>
    /// Answers the card <paramref name="id"/> waiting in <paramref name="roomId"/>: checks the values,
    /// takes the card, posts the Transcript Message as the Human, then resolves the waiting request as
    /// accepted with the wire content. A failure to post (the Room was deleted mid-tap, the Transcript
    /// cannot be written) is logged and returns <see langword="null"/>; the request is answered anyway.
    /// </summary>
    /// <param name="roomId">The Room the card waits in.</param>
    /// <param name="id">The card id the caller expects to still be waiting.</param>
    /// <param name="values">What the Human entered, by field key.</param>
    /// <param name="ct">Cancels the lookups and the post; the waiting request is still answered.</param>
    /// <returns>The Message text that was posted, or <see langword="null"/> when nothing was posted.</returns>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="values"/> do not fit the form. The form builds them and the Human cannot make
    /// them, so this is a UI bug; the card is left waiting, because taking it first would leave the agent
    /// waiting for ever.
    /// </exception>
    internal async Task<string?> AnswerAsync(string roomId, string id, IReadOnlyDictionary<string, IReadOnlyList<string>> values, CancellationToken ct)
    {
        PendingElicitation? waiting = store.Get(roomId).FirstOrDefault(card => string.Equals(card.Id, id, StringComparison.Ordinal));
        if (waiting is null)
        {
            LogGone(logger, roomId, id);
            return null;
        }

        string? problem = ElicitationComposer.Check(waiting.Form, values);
        if (problem is not null)
        {
            throw new InvalidOperationException(problem);
        }

        PendingElicitation? taken = store.TryTake(roomId, id);
        if (taken is null)
        {
            LogGone(logger, roomId, id);
            return null;
        }

        ElicitationAnswer answer = ElicitationComposer.Compose(taken.Form, values);
        string? posted = null;
        try
        {
            User human = await directory.GetHumanAsync(ct);
            ChatMessage? message = await chat.PostHumanAnswerAsync(roomId, human.Id, answer.Text, taken.AskerAgentId, ct);
            posted = message?.Text;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogPostFailed(logger, roomId, ex.Message);
        }
        finally
        {
            _ = taken.Completion.TrySetResult(new ElicitationAccepted(answer.Content));
        }

        if (posted is not null)
        {
            LogAnswered(logger, roomId, taken.Id, answer.Content.Count);
        }

        return posted;
    }

    /// <summary>
    /// Skips the card: takes it, resolves the waiting request as declined and posts nothing, so the asker
    /// is not woken. The Human can still type.
    /// </summary>
    /// <param name="roomId">The Room the card waits in.</param>
    /// <param name="id">The card id the caller expects to still be waiting.</param>
    /// <returns><see langword="true"/> when a card was found and declined.</returns>
    internal Task<bool> DeclineAsync(string roomId, string id)
    {
        PendingElicitation? taken = store.TryTake(roomId, id);
        if (taken is null)
        {
            return Task.FromResult(false);
        }

        _ = taken.Completion.TrySetResult(new ElicitationDeclined());
        LogDeclined(logger, roomId, id);
        return Task.FromResult(true);
    }

    /// <summary>Logs that an answer found nothing waiting - another caller already took the card.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="roomId">The Room the answer was for.</param>
    /// <param name="cardId">The card id the caller expected to still be waiting.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Answer for form {CardId} in room {RoomId} found nothing waiting; another caller already took it.")]
    private static partial void LogGone(ILogger logger, string roomId, string cardId);

    /// <summary>Logs that a form was answered and its Message posted.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="roomId">The Room the form was answered in.</param>
    /// <param name="cardId">The answered card's id.</param>
    /// <param name="fieldCount">How many fields the answer carried.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Answered form {CardId} in room {RoomId}: {FieldCount} field(s).")]
    private static partial void LogAnswered(ILogger logger, string roomId, string cardId, int fieldCount);

    /// <summary>Logs that a form was skipped without posting anything.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="roomId">The Room the form was skipped in.</param>
    /// <param name="cardId">The skipped card's id.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Skipped form {CardId} in room {RoomId}.")]
    private static partial void LogDeclined(ILogger logger, string roomId, string cardId);

    /// <summary>
    /// Logs that posting the answer failed, for example because the Room was deleted mid-tap. A Warning,
    /// not an Error: the request is answered regardless and the Human can type the answer instead.
    /// </summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="roomId">The Room the answer could not be posted to.</param>
    /// <param name="reason">The exception's message describing why.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not post the answer in room {RoomId}: {Reason}")]
    private static partial void LogPostFailed(ILogger logger, string roomId, string reason);
}
