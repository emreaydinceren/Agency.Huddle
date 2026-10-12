using Agency.Huddle.App.Data;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.NeedsYou;

/// <summary>
/// The one rule for whether a Mention of the Human is still unanswered, shared by the live tracker
/// and the startup scan so the two cannot disagree (Needs You spec §6.1).
/// </summary>
internal static class NeedsYouRule
{
    /// <summary>
    /// Finds the oldest Message that Mentions the Human and comes after the Human's last Message.
    /// The oldest, not the newest: the Room has been waiting since the first ask.
    /// </summary>
    /// <param name="messages">A Transcript or the end of one, oldest first.</param>
    /// <param name="mentionsHuman">Whether a Message names the Human; the caller owns Member and Alias resolution.</param>
    /// <returns>The Message, or <see langword="null"/> when no Mention follows the Human's last Message.</returns>
    internal static ChatMessage? OldestUnansweredMention(
        IReadOnlyList<ChatMessage> messages,
        Func<ChatMessage, bool> mentionsHuman)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(mentionsHuman);

        ChatMessage? oldest = null;
        for (int index = messages.Count - 1; index >= 0; index--)
        {
            ChatMessage message = messages[index];
            if (string.Equals(message.SenderId, KnownIds.Human, StringComparison.Ordinal))
            {
                break;
            }

            if (mentionsHuman(message))
            {
                oldest = message;
            }
        }

        return oldest;
    }
}
