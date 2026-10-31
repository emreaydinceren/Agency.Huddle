namespace Agency.Huddle.App.Helpers.TeamMemory;

/// <summary>
/// Queues a judgement for each Team Memory proposal (Team Memory Helper spec §6.2), keyed by the proposal's id so two proposals made
/// within the wait are both judged. The input is built now, on the caller's thread, from the Team's memory as it is.
/// </summary>
/// <remarks>
/// The dispatcher is reached through a factory because the dispatcher depends on every Helper's invoker, and the Team Memory
/// Helper depends on this trigger to queue a second judgement: a constructor dependency both ways would be a cycle.
/// </remarks>
/// <param name="builder">Builds the input.</param>
/// <param name="dispatcher">Returns the dispatcher that takes the judgement.</param>
/// <param name="logger">Records a proposal that could not be queued.</param>
internal sealed class TeamMemoryTrigger(TeamMemoryInputBuilder builder, Func<HelperDispatcher> dispatcher, ILogger<TeamMemoryTrigger> logger)
{
    /// <summary>Builds the input for a proposal and queues it. Returns at once; never throws for an expected failure.</summary>
    /// <param name="proposal">The proposal, checked by the tool.</param>
    public void Propose(TeamMemoryProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        this.Enqueue(proposal, () => builder.Build(proposal));
    }

    /// <summary>Queues the second and last judgement of a proposal whose first Verdict was stale, with the named file shown in full.</summary>
    /// <param name="proposal">The proposal.</param>
    /// <param name="folder">The folder label the stale Verdict named.</param>
    /// <param name="file">The file the stale Verdict named.</param>
    public void Rejudge(TeamMemoryProposal proposal, string folder, string file)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        this.Enqueue(proposal, () => builder.Build(proposal, attempt: 2, folder, file));
    }

    /// <summary>Builds and queues one input, logging rather than raising any failure.</summary>
    /// <param name="proposal">The proposal.</param>
    /// <param name="build">Builds the input.</param>
    private void Enqueue(TeamMemoryProposal proposal, Func<TeamMemoryInput> build)
    {
        try
        {
            dispatcher().Enqueue(TeamMemoryHelper.HelperId, proposal.Id.ToString(), build());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A proposal is a contribution, never part of the proposer's work: a failure to queue it is logged and the Turn goes on.
            logger.LogWarning(ex, "A Team Memory proposal by {Proposer} for {Team} could not be queued.", proposal.Proposer, proposal.Team);
        }
    }
}
