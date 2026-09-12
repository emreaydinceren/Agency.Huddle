namespace Agency.Huddle.App.Acp;

/// <summary>
/// Decides whether an Agent should reply to a Message it received.
/// </summary>
internal static class ReplyGate
{
    /// <summary>
    /// In a Room with at most 2 Members — the Human and exactly one Agent — there
    /// is nobody else the Message could be for, so the Agent answers every Message
    /// regardless of Mention. In a Room with 3 or more Members the Agent answers
    /// only when Mentioned, so it does not talk over the other Agents.
    ///
    /// A Room has no stored kind: this member count is the whole rule, which is why
    /// it can be decided here, client-side, from what arrives on the wire.
    ///
    /// Deliberately no runaway-loop cap here: two Agents that keep Mentioning each
    /// other can reply forever. The repo owner chose to leave that out of this proof
    /// of concept; if a cap is added later, this is the single place it belongs.
    /// </summary>
    internal static bool ShouldReply(bool mentioned, int memberCount)
    {
        return memberCount <= 2 || mentioned;
    }
}