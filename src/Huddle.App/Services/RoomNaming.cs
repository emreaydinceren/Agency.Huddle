using Agency.Huddle.App.Data;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Services;

/// <summary>
/// The one rule for what a Room is called when nobody has chosen a name for it: its Agent Members,
/// in order, joined by <c>", "</c>. This is the same shape as <see cref="Agency.Huddle.App.Acp.ReplyGate"/>
/// and <see cref="Agency.Huddle.App.Acp.PersonaStatusResolver"/> - a rule that must not be re-derived
/// differently at each call site - because <see cref="Services.ChatService"/> needs this exact
/// derivation in three places: creating a direct Room, creating a group Room, and renaming a Room
/// after an Invitation.
/// </summary>
internal static class RoomNaming
{
    /// <summary>
    /// Derives the auto-name for a Room from its Members: the Agents among them, in order, joined by
    /// <c>", "</c>. The Human never appears in it.
    /// </summary>
    /// <param name="members">The Room's current Members.</param>
    /// <returns>The derived name, or <see cref="string.Empty"/> if no Member is an Agent.</returns>
    internal static string Derive(IEnumerable<User> members)
    {
        return string.Join(", ", members.Where(m => m.Kind == UserKind.Agent).Select(m => m.Name));
    }
}
