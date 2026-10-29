using Agency.Huddle.App.Data;

namespace Agency.Huddle.App.Services;

/// <summary>
/// Posting a Message as the Human on the Human's behalf, the way an outcome card (a Sprint or retro
/// proposal, an Agent proposal, an answered question) reports back into its Room.
/// </summary>
internal static class HumanPost
{
    /// <summary>
    /// Posts <paramref name="text"/> into <paramref name="roomId"/> as the Human. A refused post is
    /// returned as its problem, never thrown, so each caller logs it in its own words and decides
    /// whether it matters.
    /// </summary>
    /// <param name="chat">Posts the Message.</param>
    /// <param name="directory">Resolves the Human.</param>
    /// <param name="roomId">The Room to post in.</param>
    /// <param name="text">The Message text.</param>
    /// <param name="ct">Cancels the Human lookup and the post.</param>
    /// <returns><see langword="null"/> when the Message was posted; otherwise the <see cref="ChatException"/>'s message.</returns>
    public static async Task<string?> TryPostAsync(ChatService chat, ITeamDirectory directory, string roomId, string text, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(chat);
        ArgumentNullException.ThrowIfNull(directory);

        User human = await directory.GetHumanAsync(ct).ConfigureAwait(false);
        try
        {
            _ = await chat.PostAsync(roomId, human.Id, text, ct: ct).ConfigureAwait(false);
            return null;
        }
        catch (ChatException ex)
        {
            return ex.Message;
        }
    }

    /// <summary>The trailing Mention that wakes <paramref name="name"/> when appended to a post, or an empty string when no such user exists any more.</summary>
    /// <param name="directory">Finds the user by Name.</param>
    /// <param name="name">The Name to mention, such as a proposal's proposer.</param>
    /// <param name="ct">Cancels the lookup.</param>
    /// <returns><c>" @Name"</c>, with the user's current spelling, or <see cref="string.Empty"/>.</returns>
    public static async Task<string> MentionAsync(ITeamDirectory directory, string name, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(directory);

        User? user = await directory.FindUserByNameAsync(name, ct).ConfigureAwait(false);
        return user is null ? string.Empty : " @" + user.Name;
    }
}
