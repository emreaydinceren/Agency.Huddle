using Agency.Huddle.App.Data;
using Agency.Huddle.App.Profile;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Tasks;

/// <summary>
/// Answers a manager's Sprint proposal for the Human (PL-4). Approve is the only path that creates a Sprint: Huddle, not the
/// agent, adds the line to <c>sprints.md</c> and sets <c>sprint:</c> on each Task (ADR-0037). A proposal is taken out of the
/// store first, so a second answer to it finds nothing and changes nothing.
/// </summary>
internal sealed class SprintProposalService(
    SprintProposalStore proposals,
    SprintStore sprints,
    TaskService tasks,
    TaskStore taskStore,
    ChatService chat,
    ITeamDirectory directory,
    ITeamCatalog teams,
    IHumanIdentity humanIdentity,
    ILogger<SprintProposalService> logger)
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> approveGates = new(StringComparer.Ordinal);

    private readonly SprintApprover approver = new(sprints, tasks, taskStore);

    private const string NoDeclineReason = "no reason given.";

    /// <summary>The proposals waiting in a room: those of the Teams whose manager is a member of it.</summary>
    /// <param name="roomId">The room's id.</param>
    /// <param name="ct">Cancels the member lookup.</param>
    /// <returns>The pending proposals; empty when the room holds no manager or nothing is pending.</returns>
    public async Task<IReadOnlyList<SprintProposal>> PendingAsync(string roomId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);

        IReadOnlyList<User> members = await directory.GetRoomMembersAsync(roomId, ct).ConfigureAwait(false);
        List<SprintProposal> pending = [];
        foreach (TeamSummary team in teams.Teams)
        {
            if (team.Manager is { } manager
                && members.Any(member => member.Kind == UserKind.Agent && string.Equals(member.Name, manager, StringComparison.OrdinalIgnoreCase)))
            {
                pending.AddRange(proposals.Pending(team.Name));
            }
        }

        return pending;
    }

    /// <summary>
    /// Approves a proposal: creates the Sprint (an existing name is accepted), sets it on each Task that still exists and is
    /// not Done, removes the proposal and posts what happened to the manager. A Sprint that cannot be created leaves the
    /// proposal pending.
    /// </summary>
    /// <param name="roomId">The manager's room the proposal is pending in.</param>
    /// <param name="proposalId">The proposal's id.</param>
    /// <param name="ct">Cancels the lookups and the post.</param>
    /// <returns>What happened; <see cref="SprintProposalOutcomeKind.Gone"/> when the proposal is not pending in that room.</returns>
    public async Task<SprintProposalOutcome> ApproveAsync(string roomId, string proposalId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        ArgumentException.ThrowIfNullOrWhiteSpace(proposalId);

        // One approval of a proposal at a time, so a second approve finds it gone (idempotence) while a failed one leaves it pending.
        SemaphoreSlim gate = this.approveGates.GetOrAdd(proposalId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await this.ApproveCoreAsync(roomId, proposalId, ct).ConfigureAwait(false);
        }
        finally
        {
            _ = gate.Release();
        }
    }

    /// <summary>Approves a proposal under its gate: the proposal is removed only after the Sprint exists.</summary>
    /// <param name="roomId">The manager's room.</param>
    /// <param name="proposalId">The proposal's id.</param>
    /// <param name="ct">Cancels the lookups and the post.</param>
    private async Task<SprintProposalOutcome> ApproveCoreAsync(string roomId, string proposalId, CancellationToken ct)
    {
        IReadOnlyList<SprintProposal> pending = await this.PendingAsync(roomId, ct).ConfigureAwait(false);
        SprintProposal? proposal = pending.FirstOrDefault(each => string.Equals(each.Id, proposalId, StringComparison.Ordinal));
        if (proposal is null)
        {
            return Gone();
        }

        Sprint sprint;
        bool existed;
        switch (sprints.Create(proposal.Name))
        {
            case SprintCreateResult.Created created:
                sprint = created.Sprint;
                existed = false;
                break;
            case SprintCreateResult.Existing existing:
                sprint = existing.Sprint;
                existed = true;
                break;
            case SprintCreateResult.Refused refused:
                logger.LogWarning("Sprint proposal {ProposalId} could not be approved: {Problem}", proposal.Id, refused.Problem);
                return new SprintProposalOutcome(SprintProposalOutcomeKind.Failed, false, [], [], refused.Problem);
            default:
                throw new InvalidOperationException("Unknown Sprint create result.");
        }

        _ = proposals.Remove(proposal.Id);

        TaskActor human = TaskActors.Human(humanIdentity);
        SprintApprover.Additions additions = this.approver.AddTasks(sprint, proposal.TaskIds, human);

        string mention = await HumanPost.MentionAsync(directory, proposal.Proposer, ct).ConfigureAwait(false);
        string text = $"Approved Sprint '{proposal.Name}'{(existed ? " (it already existed)" : string.Empty)}. {SprintApprover.Tail(additions)}{mention}";
        await this.PostAsync(roomId, text, ct).ConfigureAwait(false);
        return new SprintProposalOutcome(SprintProposalOutcomeKind.Approved, existed, additions.Applied, additions.Skipped, text);
    }

    /// <summary>Declines a proposal: removes it, creates nothing and posts the reason to the manager.</summary>
    /// <param name="roomId">The manager's room the proposal is pending in.</param>
    /// <param name="proposalId">The proposal's id.</param>
    /// <param name="reason">Why the Human declined; blank is reported as no reason given.</param>
    /// <param name="ct">Cancels the lookups and the post.</param>
    /// <returns>What happened; <see cref="SprintProposalOutcomeKind.Gone"/> when the proposal is not pending in that room.</returns>
    public async Task<SprintProposalOutcome> DeclineAsync(string roomId, string proposalId, string reason, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        ArgumentException.ThrowIfNullOrWhiteSpace(proposalId);
        ArgumentNullException.ThrowIfNull(reason);

        SprintProposal? proposal = await this.TakeAsync(roomId, proposalId, ct).ConfigureAwait(false);
        if (proposal is null)
        {
            return Gone();
        }

        string mention = await HumanPost.MentionAsync(directory, proposal.Proposer, ct).ConfigureAwait(false);
        string reasonText = string.IsNullOrWhiteSpace(reason) ? NoDeclineReason : reason.Trim();
        string text = $"Declined the proposed Sprint '{proposal.Name}': {reasonText}{mention}";
        await this.PostAsync(roomId, text, ct).ConfigureAwait(false);
        return new SprintProposalOutcome(SprintProposalOutcomeKind.Declined, false, [], [], text);
    }

    /// <summary>The outcome for a proposal that is not pending.</summary>
    private static SprintProposalOutcome Gone() => new(SprintProposalOutcomeKind.Gone, false, [], [], string.Empty);

    /// <summary>Finds the proposal in the room and removes it from the store; only one caller gets it.</summary>
    /// <param name="roomId">The manager's room.</param>
    /// <param name="proposalId">The proposal's id.</param>
    /// <param name="ct">Cancels the member lookup.</param>
    private async Task<SprintProposal?> TakeAsync(string roomId, string proposalId, CancellationToken ct)
    {
        IReadOnlyList<SprintProposal> pending = await this.PendingAsync(roomId, ct).ConfigureAwait(false);
        SprintProposal? proposal = pending.FirstOrDefault(each => string.Equals(each.Id, proposalId, StringComparison.Ordinal));
        return proposal is not null && proposals.Remove(proposal.Id) ? proposal : null;
    }

    /// <summary>Posts <paramref name="text"/> in the room as the Human; a refused post is logged, never thrown.</summary>
    /// <param name="roomId">The room to post in.</param>
    /// <param name="text">The text.</param>
    /// <param name="ct">Cancels the post.</param>
    private async Task PostAsync(string roomId, string text, CancellationToken ct)
    {
        string? problem = await HumanPost.TryPostAsync(chat, directory, roomId, text, ct).ConfigureAwait(false);
        if (problem is not null)
        {
            logger.LogWarning("Could not post the Sprint proposal outcome in room {RoomId}: {Problem}", roomId, problem);
        }
    }
}
