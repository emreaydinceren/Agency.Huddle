using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Data;

public interface IChatStore
{
    Task AppendAsync(string roomId, ChatMessage message, CancellationToken ct = default);

    Task<IReadOnlyList<ChatMessage>> ReadAllAsync(string roomId, CancellationToken ct = default);

    /// <summary>
    /// Permanently deletes a Room's Transcript file, if one exists. A Room with no Transcript yet
    /// (nobody has posted to it) is a silent no-op, not an error.
    /// </summary>
    /// <param name="roomId">The Room whose Transcript to delete.</param>
    /// <param name="ct">Cancels the delete.</param>
    Task DeleteAsync(string roomId, CancellationToken ct = default);

    /// <summary>
    /// Whether a Room has taken any Messages yet, answered from the Transcript file itself — never by
    /// reading its contents.
    /// </summary>
    /// <param name="roomId">The Room to check.</param>
    /// <param name="ct">Cancels the check.</param>
    Task<bool> HasMessagesAsync(string roomId, CancellationToken ct = default);
}