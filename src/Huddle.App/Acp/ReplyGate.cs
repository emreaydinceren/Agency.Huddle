namespace Agency.Huddle.App.Acp;

/// <summary>
/// What the Reply Gate decided about one delivery.
/// </summary>
internal enum ReplyDecision
{
    /// <summary>
    /// Take a Turn. The caller takes and clears the Room's Catch-up buffer first, so the missed
    /// Messages ride along with this one.
    /// </summary>
    Reply,

    /// <summary>
    /// Do not take a Turn: the Message was not for this Agent. It is buffered as Catch-up and rides
    /// along the next time this Agent is Mentioned in that Room.
    /// </summary>
    CatchUp,

    /// <summary>
    /// Do not take a Turn: the Room has spent its Budget. The Message is <em>not</em> buffered — it
    /// is being held for re-delivery, and buffering it would show it to the Agent twice.
    /// </summary>
    BudgetExhausted,
}

/// <summary>
/// Decides whether an Agent should reply to a Message it received.
/// </summary>
internal static class ReplyGate
{
    /// <summary>
    /// <para>
    /// In a Room with at most 2 Members — the Human and exactly one Agent — there is nobody else the
    /// Message could be for, so the Agent answers every Message regardless of Mention. In a Room with
    /// 3 or more Members the Agent answers only when Mentioned, so it does not talk over the other
    /// Agents. A Room has no stored kind: this member count is the whole of that rule.
    /// </para>
    /// <para>
    /// The Budget is checked <em>first</em>, and that ordering is the rule rather than an
    /// implementation detail: a Mention does not buy a Turn past the cap, and neither does Following
    /// - roadmap item 8's <paramref name="following"/> is the second thing that must not buy a Turn
    /// past the cap. It lives inside this function precisely so it cannot be reordered at a call
    /// site, where swapping two <c>if</c> statements would silently exempt every Mentioned or
    /// following Agent from the cap while still compiling and still passing every test that predates
    /// the cap.
    /// </para>
    /// <para>
    /// Both Budget figures arrive on the Envelope as labels computed by the server, the same way a
    /// Member count does. That keeps the whole decision here and client-side, per ADR-0003, and it is
    /// what lets the Human extend a Room's Budget at all: the allowance lives in <c>ChatService</c>
    /// and can be raised, so an Agent reading its own configured default would decline a re-delivered
    /// Message and the extension would do nothing.
    /// </para>
    /// </summary>
    /// <param name="mentioned">Whether this Agent is Mentioned in the Message.</param>
    /// <param name="memberCount">How many Members the Room has, the Human included.</param>
    /// <param name="agentMessagesSinceHuman">
    /// Agent-authored Messages this Room has taken since the Human last spoke, this one included.
    /// </param>
    /// <param name="budget">
    /// How many the Room currently allows. Zero or less means uncapped, which is the only way back to
    /// the behaviour ADR-0004 recorded as "deliberately no runaway-loop guard".
    /// </param>
    /// <param name="following">
    /// Whether the Agent asked to be woken by every Message in this Room, via
    /// <c>mcp__team__follow_room</c> (a later phase's tool), so it replies without being Mentioned.
    /// This is per (Agent, Room), never a property of a Persona: ADR-0005 rejects an
    /// <c>isCoordinator</c> frontmatter flag for exactly that reason, because the same Persona can
    /// follow one Room and not another.
    /// </param>
    /// <returns>What to do with this delivery.</returns>
    internal static ReplyDecision Decide(bool mentioned, int memberCount, int agentMessagesSinceHuman, int budget, bool following)
    {
        // >= rather than >: the count includes the Message just received, so a reply to it would be
        // the Budget-plus-first. ChatService compares the same way, so the two agree by construction.
        if (budget > 0 && agentMessagesSinceHuman >= budget)
        {
            return ReplyDecision.BudgetExhausted;
        }

        return memberCount <= 2 || mentioned || following ? ReplyDecision.Reply : ReplyDecision.CatchUp;
    }
}