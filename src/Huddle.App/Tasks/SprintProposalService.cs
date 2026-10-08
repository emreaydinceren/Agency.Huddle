using Microsoft.Extensions.Options;
using Agency.Huddle.App.Data;
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
    IOptions<TeamOptions> options,
    ILogger<SprintProposalService> logger)
{
    private const string MissingReason = "no longer exists";
    private const string DoneReason = "already Done";
    private const string NotUpdatedReason = "could not be updated";
    private const string NoDeclineReason = "no reason given.";

    /// <summary>The proposals waiting in a room: those of the Teams whose manager is a member of it.</summary>
    /// <param name="roomId">The room's id.</param>
    /// <param name="ct">Cancels the member lookup.</param>
    /// <returns>The pending proposals; empty when the room holds no manager or nothing is pending.</returns>
    public async Task<IReadOnlyList<SprintProposal>> PendingAsync(string roomId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);

        IReadOnlyList<User> members = await directory.GetRoomMembersAsync(roomId, ct);
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

        SprintProposal? proposal = await this.TakeAsync(roomId, proposalId, ct);
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
                _ = proposals.Add(proposal);
                logger.LogWarning("Sprint proposal {ProposalId} could not be approved: {Problem}", proposal.Id, refused.Problem);
                return new SprintProposalOutcome(SprintProposalOutcomeKind.Failed, false, [], [], refused.Problem);
            default:
                throw new InvalidOperationException("Unknown Sprint create result.");
        }

        TaskActor human = TaskActors.Human(options.Value);
        List<string> applied = [];
        List<SkippedTask> skipped = [];
        foreach (string idText in proposal.TaskIds)
        {
            string? reason = this.AddToSprint(idText, sprint, human);
            if (reason is null)
            {
                applied.Add(idText);
            }
            else
            {
                skipped.Add(new SkippedTask(idText, reason));
            }
        }

        string mention = await this.MentionAsync(proposal.Proposer, ct);
        string added = applied.Count == 0 ? "none" : string.Join(", ", applied);
        string text = $"Approved Sprint '{proposal.Name}'{(existed ? " (it already existed)" : string.Empty)}. Added: {added}.";
        if (skipped.Count > 0)
        {
            string skippedList = string.Join(", ", skipped.Select(static each => string.Concat(each.Id, " (", each.Reason, ")")));
            text = string.Concat(text, " Skipped: ", skippedList, ".");
        }

        text += mention;
        await this.PostAsync(roomId, text, ct);
        return new SprintProposalOutcome(SprintProposalOutcomeKind.Approved, existed, applied, skipped, text);
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

        SprintProposal? proposal = await this.TakeAsync(roomId, proposalId, ct);
        if (proposal is null)
        {
            return Gone();
        }

        string mention = await this.MentionAsync(proposal.Proposer, ct);
        string reasonText = string.IsNullOrWhiteSpace(reason) ? NoDeclineReason : reason.Trim();
        string text = $"Declined the proposed Sprint '{proposal.Name}': {reasonText}{mention}";
        await this.PostAsync(roomId, text, ct);
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
        IReadOnlyList<SprintProposal> pending = await this.PendingAsync(roomId, ct);
        SprintProposal? proposal = pending.FirstOrDefault(each => string.Equals(each.Id, proposalId, StringComparison.Ordinal));
        return proposal is not null && proposals.Remove(proposal.Id) ? proposal : null;
    }

    /// <summary>Sets the Sprint on one Task as the Human.</summary>
    /// <param name="idText">The Task id as the manager wrote it.</param>
    /// <param name="sprint">The Sprint to set.</param>
    /// <param name="human">The actor recorded in the Change log.</param>
    /// <returns><see langword="null"/> when the Task now carries the Sprint; otherwise why it was skipped.</returns>
    private string? AddToSprint(string idText, Sprint sprint, TaskActor human)
    {
        if (!TaskId.TryParse(idText, out TaskId id) || taskStore.Get(id) is not { } task)
        {
            return MissingReason;
        }

        if (task.Status == TaskState.Done)
        {
            return DoneReason;
        }

        TaskPatch patch = new() { Sprint = Optional<SprintId?>.Set(sprint.Id) };
        return tasks.Update(id, patch, null, human) switch
        {
            TaskResult.Saved or TaskResult.Unchanged => null,
            TaskResult.NotFound => MissingReason,
            TaskResult.GateRefused gate => gate.Message,
            TaskResult.Refused refused => string.Join("; ", refused.Problems),
            _ => NotUpdatedReason,
        };
    }

    /// <summary>The trailing Mention that wakes the proposer, or an empty string when the proposer no longer exists.</summary>
    /// <param name="proposer">The proposer's name.</param>
    /// <param name="ct">Cancels the lookup.</param>
    private async Task<string> MentionAsync(string proposer, CancellationToken ct)
    {
        User? user = await directory.FindUserByNameAsync(proposer, ct);
        return user is null ? string.Empty : " @" + user.Name;
    }

    /// <summary>Posts <paramref name="text"/> in the room as the Human; a refused post is logged, never thrown.</summary>
    /// <param name="roomId">The room to post in.</param>
    /// <param name="text">The text.</param>
    /// <param name="ct">Cancels the post.</param>
    private async Task PostAsync(string roomId, string text, CancellationToken ct)
    {
        User human = await directory.GetHumanAsync(ct);
        try
        {
            _ = await chat.PostAsync(roomId, human.Id, text, ct: ct);
        }
        catch (ChatException ex)
        {
            logger.LogWarning("Could not post the Sprint proposal outcome in room {RoomId}: {Problem}", roomId, ex.Message);
        }
    }
}
