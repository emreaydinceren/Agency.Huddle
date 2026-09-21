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
}