using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Teammates;

/// <summary>
/// Turns the Human's answer to a Proposal into Teammates and a Message, exactly once (Spec
/// §6.10). <see cref="ProposalStore.TryTake"/> is the "exactly once" gate: a second caller racing
/// the same Proposal id gets <see langword="null"/> back and this class reports
/// <see cref="ProposalOutcomeKind.Gone"/> without posting anything - <see cref="ProposalStore"/>
/// has already raised <see cref="RoomEvents.ProposalChanged"/> for that take, so this class must
/// never raise it again.
/// </summary>
/// <param name="proposals">Holds the pending Proposal this call takes.</param>
/// <param name="checker">
/// Re-validates each Candidate before it becomes a Teammate - state may have moved since it was
/// proposed (another Teammate created in the meantime, a name freed up, and so on).
/// </param>
/// <param name="personas">Where each valid Candidate becomes a Persona file, and the source of how many Teammates already exist for the limit check.</param>
/// <param name="chat">
/// Posts the outcome as a Message from the Human, which is what wakes the proposer through the
/// ordinary Reply Gate - no bespoke delivery of this class's own.
/// </param>
/// <param name="directory">Resolves the Human's User, and the proposer's current Name, at post time.</param>
/// <param name="hireLimits">Checks the Proposal against the per-Team and live-process hire limits.</param>
/// <param name="logger">Used to log what an Approve did.</param>
/// <param name="roleHirer">Hires a Candidate that carries a role from its team type; <see langword="null"/> when team types are not available, and such a Candidate is written as a plain one.</param>
internal sealed partial class ProposalService(
    ProposalStore proposals,
    CandidateChecker checker,
    PersonaStore personas,
    ChatService chat,
    ITeamDirectory directory,
    HireLimitGuard hireLimits,
    ILogger<ProposalService> logger,
    RoleHirer? roleHirer = null)
{
    // Serialises Persona creation across every Proposal in the app - the same race
    // PersonaStore.Add's own writeGate closes for the Teammate card (Spec §6.10's implementation
    // notes: "creationGate also closes the pre-existing race in PersonaStore.Add, but only between
    // Proposals"). One static instance app-wide, matching the Spec's own "SemaphoreSlim(1,1),
    // app-wide": a per-instance semaphore would do nothing, since ProposalService is already a
    // singleton and the race this closes is against concurrent Approvals, not against a second
    // ProposalService instance.
    private static readonly SemaphoreSlim CreationGate = new(1, 1);

    /// <summary>Approves a Proposal: creates every valid Candidate as a Teammate and posts the outcome (Spec §6.10).</summary>
    /// <param name="roomId">The Room the Proposal is pending in.</param>
    /// <param name="proposalId">The Proposal id the caller expects to still be pending.</param>
    /// <param name="ct">Cancels the Team Directory lookups, the Candidate checks and the post.</param>
    /// <returns>
    /// What happened, and the Message text that was posted - or, for
    /// <see cref="ProposalOutcomeKind.Gone"/>, an empty string, since nothing was posted.
    /// </returns>
    internal async Task<ProposalOutcome> ApproveAsync(string roomId, string proposalId, CancellationToken ct)
    {
        var proposal = proposals.TryTake(roomId, proposalId);
        if (proposal is null)
        {
            ProposalService.LogGone(logger, roomId, proposalId);
            return new ProposalOutcome(ProposalOutcomeKind.Gone, [], [], string.Empty);
        }

        var mention = await this.ResolveMentionAsync(proposal.ProposerAgentId, ct).ConfigureAwait(false);
        var refusal = hireLimits.Check(proposal.Candidates);

        if (refusal is not null)
        {
            var overLimitText = ProposalService.ComposeOverLimitText(refusal, proposal.Candidates.Count, mention);
            await this.PostOutcomeAsync(roomId, overLimitText, ct).ConfigureAwait(false);
            ProposalService.LogOverLimit(logger, roomId, proposal.Id, refusal.Kind, refusal.Used, refusal.Limit, proposal.Candidates.Count);
            return new ProposalOutcome(ProposalOutcomeKind.OverLimit, [], [], overLimitText);
        }

        var (created, failed) = await this.CreateCandidatesAsync(proposal.Candidates, ct).ConfigureAwait(false);

        var (kind, text) = failed.Count switch
        {
            0 => (ProposalOutcomeKind.Created, ProposalService.ComposeCreatedText(created, mention)),
            _ when created.Count == 0 => (ProposalOutcomeKind.NoneCreated, ProposalService.ComposeNoneCreatedText(failed, mention)),
            _ => (ProposalOutcomeKind.PartlyCreated, ProposalService.ComposePartlyCreatedText(created, failed, mention)),
        };

        await this.PostOutcomeAsync(roomId, text, ct).ConfigureAwait(false);
        ProposalService.LogApproved(logger, roomId, proposal.Id, created.Count, failed.Count);
        return new ProposalOutcome(kind, created, failed, text);
    }

    /// <summary>Creates every valid Candidate as a Teammate, one at a time under the creation gate.</summary>
    /// <param name="candidates">The Proposal's Candidates.</param>
    /// <param name="ct">Cancels the gate wait and the Candidate checks.</param>
    /// <returns>The names created and the Candidates that failed, each with its reason.</returns>
    private async Task<(List<string> Created, List<CandidateFailure> Failed)> CreateCandidatesAsync(
        IReadOnlyList<Candidate> candidates, CancellationToken ct)
    {
        var created = new List<string>();
        var failed = new List<CandidateFailure>();

        await CreationGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            foreach (var candidate in candidates)
            {
                var check = await checker.CheckAsync([candidate], ct).ConfigureAwait(false);
                if (!check.IsValid)
                {
                    failed.Add(new CandidateFailure(candidate.Name, check.Problems[0]));
                    continue;
                }

                if (candidate.Role is not null && roleHirer is not null)
                {
                    HireResult hired = roleHirer.Hire(candidate);
                    if (hired.PersonaName is not null)
                    {
                        created.Add(candidate.Name);
                    }
                    else
                    {
                        failed.Add(new CandidateFailure(candidate.Name, hired.Error ?? string.Empty));
                    }

                    continue;
                }

                var identity = new PersonaIdentity(candidate.Name, candidate.Title, candidate.Alias, candidate.Teams);
                try
                {
                    personas.Add(identity, candidate.Body);
                    created.Add(candidate.Name);
                }
                catch (ChatException ex)
                {
                    failed.Add(new CandidateFailure(candidate.Name, ex.Message));
                }
            }
        }
        finally
        {
            CreationGate.Release();
        }

        return (created, failed);
    }

    /// <summary>Declines a Proposal: creates nothing and posts the outcome naming every declined Candidate (Spec §6.10).</summary>
    /// <param name="roomId">The Room the Proposal is pending in.</param>
    /// <param name="proposalId">The Proposal id the caller expects to still be pending.</param>
    /// <param name="ct">Cancels the Team Directory lookups and the post.</param>
    /// <returns>
    /// What happened, and the Message text that was posted - or, for
    /// <see cref="ProposalOutcomeKind.Gone"/>, an empty string, since nothing was posted.
    /// </returns>
    internal async Task<ProposalOutcome> DeclineAsync(string roomId, string proposalId, CancellationToken ct)
    {
        var proposal = proposals.TryTake(roomId, proposalId);
        if (proposal is null)
        {
            ProposalService.LogGone(logger, roomId, proposalId);
            return new ProposalOutcome(ProposalOutcomeKind.Gone, [], [], string.Empty);
        }

        // No CreationGate: unlike Approve, Decline never touches PersonaStore, so there is nothing
        // for that semaphore to serialise here.
        var mention = await this.ResolveMentionAsync(proposal.ProposerAgentId, ct).ConfigureAwait(false);
        var text = ProposalService.ComposeDeclinedText(proposal.Candidates, mention);

        await this.PostOutcomeAsync(roomId, text, ct).ConfigureAwait(false);
        ProposalService.LogDeclined(logger, roomId, proposal.Id, proposal.Candidates.Count);
        return new ProposalOutcome(ProposalOutcomeKind.Declined, [], [], text);
    }

    /// <summary>
    /// Posts <paramref name="text"/> in <paramref name="roomId"/> as the Human - the delivery that
    /// wakes the proposer through the ordinary Reply Gate. A failure here (Spec §6.10's
    /// Constraints: e.g. the Room was deleted mid-Approve) is logged, never thrown - by the time
    /// this runs, any Teammates this Approve created already exist and Spec §14 D-7 rules out
    /// rolling them back over a Message that could not be delivered, so the caller's own
    /// <see cref="ProposalOutcome"/> is unaffected and still carries <paramref name="text"/> as
    /// <see cref="ProposalOutcome.PostedText"/> for its card to show.
    /// </summary>
    /// <param name="roomId">The Room to post in.</param>
    /// <param name="text">The outcome text to post.</param>
    /// <param name="ct">Cancels the Human lookup and the post.</param>
    private async Task PostOutcomeAsync(string roomId, string text, CancellationToken ct)
    {
        string? problem = await HumanPost.TryPostAsync(chat, directory, roomId, text, ct).ConfigureAwait(false);
        if (problem is not null)
        {
            ProposalService.LogPostFailed(logger, roomId, problem);
        }
    }

    /// <summary>
    /// Resolves the proposer's current Name for the outcome Message's Mention (Spec §6.10's
    /// implementation notes): looked up by <see cref="Proposal.ProposerAgentId"/> at post time,
    /// never the <see cref="Proposal.ProposerName"/> captured when the Proposal was made, so a
    /// proposer renamed while its Proposal waited is still woken under its new Name.
    /// </summary>
    /// <param name="proposerAgentId">The stable user id <see cref="Proposal.ProposerAgentId"/> carries.</param>
    /// <param name="ct">Cancels the lookup.</param>
    /// <returns>The Mention token, e.g. <c>"@Chief of Staff"</c>, or <see langword="null"/> when the proposer no longer exists.</returns>
    private async Task<string?> ResolveMentionAsync(string proposerAgentId, CancellationToken ct)
    {
        var proposer = await directory.GetUserAsync(proposerAgentId, ct).ConfigureAwait(false);
        return proposer is null ? null : $"@{proposer.Name}";
    }

    /// <summary>Formats a list of Names as prose, matching every outcome template in Spec §6.10's table: "A", "A and B", "A, B and C".</summary>
    /// <param name="names">The Names to format, in the order they should read.</param>
    /// <returns>The formatted list, or an empty string when <paramref name="names"/> is empty.</returns>
    private static string FormatNameList(IReadOnlyList<string> names)
    {
        return names.Count switch
        {
            0 => string.Empty,
            1 => names[0],
            2 => $"{names[0]} and {names[1]}",
            _ => $"{string.Join(", ", names.Take(names.Count - 1))} and {names[^1]}",
        };
    }

    /// <summary>Formats every failed Candidate as <c>"Name: Reason"</c>, joined by <c>"; "</c>, for the NoneCreated and PartlyCreated templates.</summary>
    /// <param name="failures">The failures to format, in the order they occurred.</param>
    private static string FormatFailures(IReadOnlyList<CandidateFailure> failures)
    {
        return string.Join("; ", failures.Select(failure => $"{failure.Name}: {failure.Reason}"));
    }

    /// <summary>Composes the Created outcome text: every Candidate became a Teammate.</summary>
    /// <param name="created">The Names created, in Proposal order.</param>
    /// <param name="mention">
    /// The proposer's Mention token, or <see langword="null"/> to omit it. "Go ahead" is an
    /// instruction to the proposer, so a missing proposer drops the whole trailing clause, not just
    /// the '@' - "go ahead" addressed to nobody is as much a dangling reference as the '@' itself.
    /// </param>
    private static string ComposeCreatedText(IReadOnlyList<string> created, string? mention)
    {
        var body = $"Approved. Created {ProposalService.FormatNameList(created)}.";
        return mention is null ? body : $"{body} {mention} go ahead.";
    }

    /// <summary>Composes the PartlyCreated outcome text: some Candidates became Teammates, some did not.</summary>
    /// <param name="created">The Names created, in Proposal order.</param>
    /// <param name="failed">Every Candidate that did not become a Teammate, with why.</param>
    /// <param name="mention">The proposer's Mention token, or <see langword="null"/> to omit it.</param>
    private static string ComposePartlyCreatedText(IReadOnlyList<string> created, IReadOnlyList<CandidateFailure> failed, string? mention)
    {
        var body = $"Approved. Created {ProposalService.FormatNameList(created)}. Could not create {ProposalService.FormatFailures(failed)}";
        return mention is null ? body : $"{body} {mention}";
    }

    /// <summary>Composes the NoneCreated outcome text: every Candidate failed.</summary>
    /// <param name="failed">Every Candidate that did not become a Teammate, with why.</param>
    /// <param name="mention">The proposer's Mention token, or <see langword="null"/> to omit it.</param>
    private static string ComposeNoneCreatedText(IReadOnlyList<CandidateFailure> failed, string? mention)
    {
        var body = $"Approved, but nothing was created. {ProposalService.FormatFailures(failed)}";
        return mention is null ? body : $"{body} {mention}";
    }

    /// <summary>Composes the OverLimit outcome text: nothing was attempted because the Proposal would have exceeded the limit.</summary>
    /// <param name="refusal">The limit that refused, naming its kind, Team, usage and value.</param>
    /// <param name="addCount">How many Candidates this Proposal would have added.</param>
    /// <param name="mention">The proposer's Mention token, or <see langword="null"/> to omit it.</param>
    private static string ComposeOverLimitText(HireRefusal refusal, int addCount, string? mention)
    {
        var body = refusal.Kind == HireLimitKind.PerTeam
            ? $"Approved, but nothing was created: Team {refusal.Team} has {refusal.Used} Teammates, the limit is {refusal.Limit} per Team, and this Proposal adds {addCount}."
            : $"Approved, but nothing was created: {refusal.Used} live processes are running, the limit is {refusal.Limit} live processes, and this Proposal adds {addCount}.";
        return mention is null ? body : $"{body} {mention}";
    }

    /// <summary>
    /// Composes the Declined outcome text. Unlike every other template, Spec §6.10's own worked
    /// example joins Names with plain commas, not "and" - <c>"Declined the proposed Teammates:
    /// Vera, Quill."</c> - so this deliberately does not call <see cref="FormatNameList"/>.
    /// </summary>
    /// <param name="candidates">The declined Candidates, in Proposal order.</param>
    /// <param name="mention">The proposer's Mention token, or <see langword="null"/> to omit it.</param>
    private static string ComposeDeclinedText(IReadOnlyList<Candidate> candidates, string? mention)
    {
        var names = string.Join(", ", candidates.Select(candidate => candidate.Name));
        var body = $"Declined the proposed Teammates: {names}.";
        return mention is null ? body : $"{body} {mention}";
    }

    /// <summary>Logs that an Approve found nothing pending - another caller already took the Proposal.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="roomId">The Room the Approve was called against.</param>
    /// <param name="proposalId">The Proposal id the caller expected to still be pending.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Approve for proposal {ProposalId} in room {RoomId} found nothing pending; another caller already took it.")]
    private static partial void LogGone(ILogger logger, string roomId, string proposalId);

    /// <summary>Logs that an Approve created nothing because the Proposal would have exceeded the Teammate limit.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="roomId">The Room the Proposal was approved in.</param>
    /// <param name="proposalId">The approved Proposal's id.</param>
    /// <param name="kind">Which hire limit refused.</param>
    /// <param name="used">How many members of the Team, or live processes, were already counted.</param>
    /// <param name="limit">The limit that refused.</param>
    /// <param name="addCount">How many Candidates the Proposal would have added.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Approve for proposal {ProposalId} in room {RoomId} created nothing: the {Kind} limit of {Limit} is reached at {Used}, and the proposal adds {AddCount}.")]
    private static partial void LogOverLimit(ILogger logger, string roomId, string proposalId, HireLimitKind kind, int used, int limit, int addCount);

    /// <summary>Logs the result of a completed Approve.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="roomId">The Room the Proposal was approved in.</param>
    /// <param name="proposalId">The approved Proposal's id.</param>
    /// <param name="createdCount">How many Candidates became Teammates.</param>
    /// <param name="failedCount">How many Candidates did not.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Approved proposal {ProposalId} in room {RoomId}: {CreatedCount} teammate(s) created, {FailedCount} failed.")]
    private static partial void LogApproved(ILogger logger, string roomId, string proposalId, int createdCount, int failedCount);

    /// <summary>
    /// Logs that posting the outcome Message failed (Spec §6.10's Constraints, e.g. the Room was
    /// deleted mid-Approve) - a Warning, not an Error, because any Teammates already created stay
    /// created (Spec §14 D-7's no rollback) and the caller's own <see cref="ProposalOutcome"/>
    /// still carries the text that would have been posted.
    /// </summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="roomId">The Room the outcome Message could not be posted to.</param>
    /// <param name="reason">The <see cref="ChatException"/> message describing why the post failed.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not post the outcome message in room {RoomId}: {Reason}")]
    private static partial void LogPostFailed(ILogger logger, string roomId, string reason);

    /// <summary>Logs that a Proposal was declined.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="roomId">The Room the Proposal was declined in.</param>
    /// <param name="proposalId">The declined Proposal's id.</param>
    /// <param name="candidateCount">How many Candidates were declined.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Declined proposal {ProposalId} in room {RoomId}: {CandidateCount} candidate(s).")]
    private static partial void LogDeclined(ILogger logger, string roomId, string proposalId, int candidateCount);
}
