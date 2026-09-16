using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// What a Room's delivery of one Message amounted to, once every reachable Agent recipient's
/// <see cref="ReplyGate"/> decision is folded together.
/// </summary>
internal enum RoomReply
{
    /// <summary>At least one reachable Agent recipient's Reply Gate said Reply.</summary>
    Expected,

    /// <summary>Every reachable Agent recipient decided CatchUp: no teammate was addressed.</summary>
    ContextOnly,

    /// <summary>The Room's Budget is spent. ADR-0006's Continue prompt already owns this surface.</summary>
    Paused,

    /// <summary>Nobody was left to deliver to: the only Agent Members are the sender, or unreachable, or absent.</summary>
    NoRecipients,

    /// <summary>
    /// A Teammate was Mentioned by name but cannot receive the Message - Offline, Degraded, or
    /// otherwise not reachable. Deliberately its own outcome rather than folded into
    /// <see cref="ContextOnly"/>, which would be false (a Teammate WAS named), or into
    /// <see cref="NoRecipients"/>, which would be false too whenever some other Teammate did receive
    /// it. Every surface renders nothing for this today: explaining it is the deferred half of
    /// ADR-0012, and rendering nothing is how the Room avoids saying something wrong in the meantime.
    /// </summary>
    MentionedUnreachable,
}

/// <summary>
/// Folds every reachable Agent recipient's individual <see cref="ReplyGate"/> decision into one
/// <see cref="RoomReply"/> for the Room as a whole — the answer a Room banner or health strip needs,
/// where <see cref="ReplyGate"/> only ever speaks for a single Agent.
/// </summary>
internal static class RoomReplyResolver
{
    /// <summary>
    /// <para>
    /// Resolves what a Room's delivery of one Message amounted to. The fan-out lives here, deliberately
    /// separate from <see cref="ReplyGate"/> itself: <see cref="ReplyGate.Decide"/> checks the Room's
    /// Budget before its Mention rule, and that ordering must stay inside one function or a caller
    /// could re-express it as two <c>if</c> statements and silently get it backwards. Looping over every
    /// recipient to summarise the Room is arithmetic no Razor render ever needs to exercise, so it does
    /// not belong beside the markup either - it belongs here, once, called by whatever surface needs it.
    /// </para>
    /// <para>
    /// Roadmap item 8 gives <see cref="ReplyGate.Decide"/> a further <c>following</c> parameter that is
    /// deliberately client-side and never crosses the wire. This function will never see it, and so it
    /// answers only <em>what the delivery was labelled</em> - never what a runner actually did with it.
    /// A Room banner built from this result must be worded to instruct ("mention a teammate to get a
    /// reply"), never to predict ("a teammate will reply") - this function has no way to know whether a
    /// following Persona is even listening.
    /// </para>
    /// <para>
    /// <paramref name="reachableAgentIds"/> is a recipient filter, not an outcome, because
    /// <see cref="Agency.Huddle.App.Pipes.AgentGateway.DeliverAsync"/> also skips a disconnected Agent, and with
    /// <c>Team:Acp:Enabled</c> false - the default - no Persona has ever started. Folding reachability
    /// into the Reply/CatchUp/BudgetExhausted decision instead of filtering recipients with it first
    /// would let a Mention of an offline teammate resolve to <see cref="RoomReply.Expected"/>, and the
    /// Room view would tell the Human to address someone who cannot answer.
    /// </para>
    /// </summary>
    /// <param name="members">Every Member of the Room, the Human included.</param>
    /// <param name="mentions">Every Member the Message Mentions.</param>
    /// <param name="senderId">The id of the User who sent the Message.</param>
    /// <param name="reachableAgentIds">
    /// The Agent ids that could actually receive this Message right now. The caller derives these
    /// through <see cref="PersonaStatusResolver.Resolve"/> rather than from
    /// <see cref="Agency.Huddle.App.Pipes.AgentGateway.IsOnline"/> alone: health outranks pipe
    /// liveness, and that ordering is the rule - any of a runner's loops can die and leave its pipe
    /// open, so an Agent can be deaf and still report online.
    /// </param>
    /// <param name="budget">The Room's current Budget.</param>
    /// <returns>The one <see cref="RoomReply"/> that summarises this delivery.</returns>
    internal static RoomReply Resolve(
        IReadOnlyList<User> members,
        IReadOnlyList<User> mentions,
        string senderId,
        IReadOnlySet<string> reachableAgentIds,
        RoomBudget budget)
    {
        // Checked before anything else, and the ordering is deliberate. A Teammate the Human named
        // but that cannot receive the Message makes every other answer here untrue: "no teammate was
        // mentioned" is false because one was, and "there was nobody to wake" is false whenever some
        // other Teammate did receive it. So it is reported as itself and rendered as nothing.
        foreach (User mention in mentions)
        {
            if (MessagePostedEvent.IsRecipient(mention, senderId) && !reachableAgentIds.Contains(mention.Id))
            {
                return RoomReply.MentionedUnreachable;
            }
        }

        List<User> recipients = FindRecipients(members, senderId, reachableAgentIds);
        if (recipients.Count == 0)
        {
            return RoomReply.NoRecipients;
        }

        List<ReplyDecision> decisions = [];
        foreach (User recipient in recipients)
        {
            bool mentioned = mentions.Any(m => string.Equals(m.Id, recipient.Id, StringComparison.Ordinal));
            decisions.Add(ReplyGate.Decide(mentioned, members.Count, budget.Used, budget.Granted));
        }

        if (decisions.Any(d => d == ReplyDecision.Reply))
        {
            return RoomReply.Expected;
        }

        return decisions.All(d => d == ReplyDecision.BudgetExhausted) ? RoomReply.Paused : RoomReply.ContextOnly;
    }

    private static List<User> FindRecipients(IReadOnlyList<User> members, string senderId, IReadOnlySet<string> reachableAgentIds)
    {
        List<User> recipients = [];
        foreach (User member in members)
        {
            if (MessagePostedEvent.IsRecipient(member, senderId) && reachableAgentIds.Contains(member.Id))
            {
                recipients.Add(member);
            }
        }

        return recipients;
    }
}
