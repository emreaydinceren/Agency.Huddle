using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// The one recipient loop: which Members of a Room a Message is delivered to. <see cref="RoomReplyResolver"/> and
/// <see cref="OwedTurnPolicy"/> both call it, so what the Room banner calls a recipient and what the Turn Queue
/// records as owed cannot drift apart.
/// </summary>
internal static class RoomRecipients
{
    /// <summary>The Agents that receive a Message.</summary>
    /// <param name="members">Every Member of the Room.</param>
    /// <param name="senderId">The id of the User who sent the Message.</param>
    /// <param name="reachableAgentIds">The Agent ids that can receive it right now, or <see langword="null"/> for no reachability filter.</param>
    /// <returns>The recipients, in Member order.</returns>
    internal static List<User> Find(IReadOnlyList<User> members, string senderId, IReadOnlySet<string>? reachableAgentIds)
    {
        List<User> recipients = [];
        foreach (User member in members)
        {
            if (MessagePostedEvent.IsRecipient(member, senderId) && (reachableAgentIds is null || reachableAgentIds.Contains(member.Id)))
            {
                recipients.Add(member);
            }
        }

        return recipients;
    }
}
