using Agency.Huddle.App.Data;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Services;

/// <summary>A Message that reached a Room's Transcript, with who was in the Room and who it names.</summary>
/// <param name="Room">The Room the Message was posted in.</param>
/// <param name="Message">The Message itself.</param>
/// <param name="Members">The Room's Members at the moment of posting.</param>
/// <param name="Mentions">The Members the Message names.</param>
/// <param name="Budget">The Room's Budget after the post.</param>
/// <param name="WithheldFromAgentId">
/// The Agent this Message is not delivered to, or <see langword="null"/> when every Agent Member
/// except the sender receives it. Set only for the Human's answer to a form that Agent opened: its
/// Turn is still open and takes the answer as the tool's own result, so a delivery would queue a
/// second Turn. Read only by <c>AgentGateway</c>; the Room view shows the Message either way.
/// </param>
public sealed record MessagePostedEvent(
    Room Room,
    ChatMessage Message,
    IReadOnlyList<User> Members,
    IReadOnlyList<User> Mentions,
    RoomBudget Budget,
    string? WithheldFromAgentId = null)
{
    /// <summary>
    /// Whether <paramref name="member"/> is an Agent this Message is delivered to. An Agent never
    /// receives its own Message - structural, and what stops naive clients echo-looping.
    /// </summary>
    /// <param name="member">The candidate recipient.</param>
    /// <param name="senderId">The id of the User who sent the Message.</param>
    /// <returns><see langword="true"/> when this Message is delivered to <paramref name="member"/>.</returns>
    internal static bool IsRecipient(User member, string senderId) =>
        member.Kind == UserKind.Agent && !string.Equals(member.Id, senderId, StringComparison.Ordinal);
}