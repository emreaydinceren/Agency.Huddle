namespace Agency.Huddle.App.Services;

/// <summary>
/// The Archive, Unarchive and Delete a Room list row performs through <see cref="ChatService"/>, shared by
/// <c>RoomNavRow</c> and <c>ArchivedChatsDialog</c> so a refusal is turned into a message in one place (UX-R29).
/// </summary>
internal static class RoomActions
{
    /// <summary>Sets a Room's archived flag.</summary>
    /// <param name="chat">The chat service that performs the change.</param>
    /// <param name="roomId">The Room's Id.</param>
    /// <param name="archived"><see langword="true"/> to archive, <see langword="false"/> to unarchive.</param>
    /// <param name="ct">Cancels the change.</param>
    /// <returns>The refusal's message, or <see langword="null"/> when the change was made.</returns>
    public static async Task<string?> SetArchivedAsync(ChatService chat, string roomId, bool archived, CancellationToken ct)
    {
        try
        {
            _ = await chat.SetRoomArchivedAsync(roomId, archived, ct).ConfigureAwait(false);
            return null;
        }
        catch (ChatException ex)
        {
            return ex.Message;
        }
    }

    /// <summary>Deletes a Room.</summary>
    /// <param name="chat">The chat service that performs the delete.</param>
    /// <param name="roomId">The Room's Id.</param>
    /// <param name="ct">Cancels the delete.</param>
    /// <returns>The refusal's message, or <see langword="null"/> when the Room was deleted.</returns>
    public static async Task<string?> DeleteAsync(ChatService chat, string roomId, CancellationToken ct)
    {
        try
        {
            await chat.DeleteRoomAsync(roomId, ct).ConfigureAwait(false);
            return null;
        }
        catch (ChatException ex)
        {
            return ex.Message;
        }
    }
}
