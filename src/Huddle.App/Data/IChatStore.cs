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

    /// <summary>
    /// Reads the last <paramref name="count"/> Messages of a Room's Transcript, oldest first. The default
    /// reads everything and keeps the end; <see cref="FileChatStore"/> overrides it to read only the end of
    /// the file, so a startup scan of many Rooms does not grow with their history.
    /// </summary>
    /// <param name="roomId">The Room to read.</param>
    /// <param name="count">How many Messages to keep at most.</param>
    /// <param name="ct">Cancels the read.</param>
    /// <returns>At most <paramref name="count"/> Messages; empty for a Room with no Transcript.</returns>
    async Task<IReadOnlyList<ChatMessage>> ReadTailAsync(string roomId, int count, CancellationToken ct = default)
    {
        IReadOnlyList<ChatMessage> all = await this.ReadAllAsync(roomId, ct).ConfigureAwait(false);
        return all.Count <= count ? all : [.. all.Skip(all.Count - count)];
    }
}