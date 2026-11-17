using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;

namespace Agency.Huddle.App.Acp;

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
    /// deliberately client-side and never crosses the wire. That made it sound, once, as though this
    /// function could never see it and a Room banner could only ever describe what a delivery was
    /// <em>labelled</em>. That was true of the wire and false of the process: <see cref="RoomFollows"/>
    /// is an in-process singleton, so a caller <em>can</em> supply the following set it holds, and
    /// <paramref name="followingAgentIds"/> below is exactly that. What the wire boundary still rules
    /// out is a guarantee: this function reports what the gate would permit, not what a runner will
    /// actually do with the delivery - a raw pipe client can reply to anything regardless of what this
    /// resolves to, and a following Persona still has to be online and listening. So the copy built
    /// from this result must still be worded to instruct ("mention an agent to get a reply"), never
    /// to predict ("an agent will reply") - following is now a known input rather than an assumed
    /// <see langword="false"/>, but the gate remains permission, not obligation.
    /// </para>
    /// <para>
    /// <paramref name="reachableAgentIds"/> is a recipient filter, not an outcome, because
    /// <see cref="Agency.Huddle.App.Pipes.AgentGateway.DeliverAsync"/> also skips a disconnected Agent, and with
    /// <c>Team:Acp:Enabled</c> false - the default - no Persona has ever started. Folding reachability
    /// into the Reply/CatchUp/BudgetExhausted decision instead of filtering recipients with it first
    /// would let a Mention of an offline agent resolve to <see cref="RoomReply.Expected"/>, and the
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
    /// <param name="followingAgentIds">
    /// The Agent ids currently following this Room via <see cref="RoomFollows.FollowersOf"/> - each
    /// one's <see cref="ReplyGate.Decide"/> call passes <see langword="true"/> for <c>following</c>
    /// regardless of Mention.
    /// </param>
    /// <param name="budget">The Room's current Budget.</param>
    /// <returns>The one <see cref="RoomReply"/> that summarises this delivery.</returns>
    internal static RoomReply Resolve(
        IReadOnlyList<User> members,
        IReadOnlyList<User> mentions,
        string senderId,
        IReadOnlySet<string> reachableAgentIds,
        IReadOnlySet<string> followingAgentIds,
        RoomBudget budget)
    {
        // Checked before anything else, and the ordering is deliberate. An Agent the Human named
        // but that cannot receive the Message makes every other answer here untrue: "no agent was
        // mentioned" is false because one was, and "there was nobody to wake" is false whenever some
        // other Agent did receive it. So it is reported as itself and rendered as nothing.
        foreach (User mention in mentions)
        {
            if (MessagePostedEvent.IsRecipient(mention, senderId) && !reachableAgentIds.Contains(mention.Id))
            {
                return RoomReply.MentionedUnreachable;
            }
        }

        List<User> recipients = RoomRecipients.Find(members, senderId, reachableAgentIds);
        if (recipients.Count == 0)
        {
            return RoomReply.NoRecipients;
        }

        List<ReplyDecision> decisions = [];
        foreach (User recipient in recipients)
        {
            bool mentioned = mentions.Any(m => string.Equals(m.Id, recipient.Id, StringComparison.Ordinal));
            bool following = followingAgentIds.Contains(recipient.Id);
            decisions.Add(ReplyGate.Decide(mentioned, members.Count, budget.Used, budget.Granted, following));
        }

        if (decisions.Any(d => d == ReplyDecision.Reply))
        {
            return RoomReply.Expected;
        }

        return decisions.All(d => d == ReplyDecision.BudgetExhausted) ? RoomReply.Paused : RoomReply.ContextOnly;
    }
}
