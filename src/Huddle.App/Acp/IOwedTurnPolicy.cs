using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;

namespace Agency.Huddle.App.Acp;

/// <summary>Works out, at post time, which Agents owe a Turn for a Message (Turn Queue spec §5.4).</summary>
internal interface IOwedTurnPolicy
{
    /// <summary>The ids of the Agents that owe a Turn for a Message.</summary>
    /// <param name="room">The Room the Message is posted to.</param>
    /// <param name="members">Every Member of the Room.</param>
    /// <param name="mentions">Every Member the Message Mentions.</param>
    /// <param name="senderId">The id of the User who sent the Message.</param>
    /// <param name="budgetAfter">The Room's Budget once this Message is counted.</param>
    /// <param name="withheldFromAgentId">The Agent the Message is withheld from, or <see langword="null"/>.</param>
    /// <param name="fromHuddle">Whether Huddle itself wrote the Message.</param>
    /// <returns>The Agent ids for which the Reply Gate answers Reply; no reachability filter, so an offline Agent still owes.</returns>
    IReadOnlyList<string> Owed(
        Room room,
        IReadOnlyList<User> members,
        IReadOnlyList<User> mentions,
        string senderId,
        RoomBudget budgetAfter,
        string? withheldFromAgentId,
        bool fromHuddle);
}
