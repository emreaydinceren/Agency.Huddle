using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// The Reply Gate, asked once at post time for every Agent that would receive the Message, so the answer is
/// remembered instead of re-derived (Turn Queue spec §5.4).
/// </summary>
/// <param name="follows">Which Agents follow which Room.</param>
internal sealed class OwedTurnPolicy(RoomFollows follows) : IOwedTurnPolicy
{
    /// <inheritdoc />
    public IReadOnlyList<string> Owed(
        Room room,
        IReadOnlyList<User> members,
        IReadOnlyList<User> mentions,
        string senderId,
        RoomBudget budgetAfter,
        string? withheldFromAgentId,
        bool fromHuddle)
    {
        IReadOnlySet<string> followers = follows.FollowersOf(room.Id);
        List<string> owed = [];
        foreach (User recipient in RoomRecipients.Find(members, senderId, null))
        {
            if (string.Equals(recipient.Id, withheldFromAgentId, StringComparison.Ordinal))
            {
                continue;
            }

            bool mentioned = mentions.Any(m => string.Equals(m.Id, recipient.Id, StringComparison.Ordinal));
            bool following = followers.Contains(recipient.Id);
            if (ReplyGate.Decide(mentioned, members.Count, budgetAfter.Used, budgetAfter.Granted, following, fromHuddle) == ReplyDecision.Reply)
            {
                owed.Add(recipient.Id);
            }
        }

        return owed;
    }
}
