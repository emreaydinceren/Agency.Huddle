namespace Agency.Huddle.App.Acp.Tools;

using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Agents;
using Agency.Huddle.App.Services;

/// <summary>
/// Asks the Human to approve a roster of new Agents (Spec §6.9). Creates nothing itself - a
/// successful call only stores a <see cref="Proposal"/> for the Human to Approve or Decline; see
/// <c>ProposalService</c> (D11) for what happens next. Offered only to a Persona holding a Skill
/// that lists it - see <see cref="Agency.Huddle.App.Skills.SkillGrants"/> - the same gate
/// <see cref="ValidateAgentTool"/> is behind.
/// </summary>
internal sealed class ProposeAgentsTool(
    ProposalStore proposals,
    CandidateChecker checker,
    ITeamDirectory directory,
    HireLimitGuard hireLimits,
    TimeProvider clock,
    string callerAgentId,
    IPromptSource prompts) : IAppTool
{
    public string Name => "propose_agents";

    public string Description => prompts.Render("tool.proposeAgents.description");

    public JsonObject InputSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["roomId"] = new JsonObject { ["type"] = "string" },
            ["candidates"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = CandidateJson.Schema(),
            },
        },
        ["required"] = new JsonArray { "roomId", "candidates" },
    };

    /// <summary>
    /// Runs Spec §6.9's internal flow, in order: the Room must exist, the caller must be a Member
    /// of it, it must not be Archived, the Candidate count must be one to four, every Candidate must
    /// parse and pass <see cref="CandidateChecker"/>, and the Proposal must not push the Agent
    /// count over a hire limit (<see cref="HireLimitGuard"/>) - only then is it handed to
    /// <see cref="ProposalStore.TryPut"/>. Every early exit is a string; this never throws for an
    /// expected failure (Spec §5.3 Contract B).
    /// </summary>
    public async Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!TaskToolText.TryGetString(arguments, "roomId", out string? roomId, out string roomIdRefusal))
        {
            return roomIdRefusal;
        }

        if (string.IsNullOrWhiteSpace(roomId))
        {
            return "The 'roomId' argument is required.";
        }

        var (room, roomRefusal) = await this.ResolveProposableRoomAsync(roomId, cancellationToken).ConfigureAwait(false);
        if (room is null)
        {
            return roomRefusal;
        }

        if (arguments["candidates"] is not JsonArray candidatesArray || candidatesArray.Count is 0 or > 4)
        {
            return "Propose between one and four Candidates.";
        }

        var problems = new List<string>();
        var candidates = ParseCandidates(candidatesArray, problems);
        if (problems.Count > 0)
        {
            return string.Join('\n', problems);
        }

        var check = await checker.CheckAsync(candidates, cancellationToken).ConfigureAwait(false);
        if (!check.IsValid)
        {
            return string.Join('\n', check.Problems);
        }

        var refusal = hireLimits.Check(candidates);
        if (refusal is not null)
        {
            return DescribeHireLimit(refusal);
        }

        return await this.StoreProposalAsync(room, roomId, candidates, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Stores the Proposal for the Human to approve, unless this Room already has one waiting.</summary>
    /// <param name="room">The Room the Proposal is made in.</param>
    /// <param name="roomId">The Room id as the model sent it.</param>
    /// <param name="candidates">The checked Candidates.</param>
    /// <param name="cancellationToken">Cancels the directory lookup.</param>
    private async Task<string> StoreProposalAsync(Room room, string roomId, List<Candidate> candidates, CancellationToken cancellationToken)
    {
        var caller = await directory.GetUserAsync(callerAgentId, cancellationToken).ConfigureAwait(false);
        var proposerName = caller?.Name ?? callerAgentId;

        var proposal = new Proposal(
            Guid.NewGuid().ToString("N"),
            roomId,
            callerAgentId,
            proposerName,
            candidates,
            clock.GetUtcNow());

        var put = proposals.TryPut(proposal);
        if (put.Result == PendingPutResult.Refused)
        {
            var existingProposerName = put.Existing?.ProposerName ?? "another Agent";
            return $"A Proposal from {existingProposerName} is already waiting in this Room.";
        }

        var names = string.Join(", ", candidates.Select(candidate => candidate.Name));
        return $"Proposed {candidates.Count} Agents ({names}) in Room '{room.Name}' (id {room.Id}). " +
            "The Human has been asked to approve. Do not create Rooms for them or mention them yet; you will be told the outcome in this Room.";
    }

    /// <summary>Looks the Room up and checks the caller may propose in it: it exists, the caller is a Member and it is not Archived.</summary>
    /// <param name="roomId">The Room the model named.</param>
    /// <param name="cancellationToken">Cancels the directory lookups.</param>
    /// <returns>The Room, or a null Room with the refusal text.</returns>
    private async Task<(Room? Room, string Refusal)> ResolveProposableRoomAsync(string roomId, CancellationToken cancellationToken)
    {
        var room = await directory.GetRoomAsync(roomId, cancellationToken).ConfigureAwait(false);
        if (room is null)
        {
            return (null, $"Unknown room '{roomId}'.");
        }

        var members = await directory.GetRoomMembersAsync(roomId, cancellationToken).ConfigureAwait(false);
        if (!members.Any(member => string.Equals(member.Id, callerAgentId, StringComparison.Ordinal)))
        {
            return (null, "You are not a Member of that Room.");
        }

        if (room.Archived)
        {
            return (null, "That Room is Archived.");
        }

        return (room, string.Empty);
    }

    /// <summary>Parses every Candidate in the array, appending each problem to <paramref name="problems"/>.</summary>
    /// <param name="candidatesArray">The <c>candidates</c> array the model sent.</param>
    /// <param name="problems">Collects every problem found, across all Candidates.</param>
    private static List<Candidate> ParseCandidates(JsonArray candidatesArray, List<string> problems)
    {
        var candidates = new List<Candidate>();
        for (var index = 0; index < candidatesArray.Count; index++)
        {
            if (CandidateJson.TryParse(candidatesArray[index], index + 1, out var candidate, problems) && candidate is not null)
            {
                candidates.Add(candidate);
            }
        }

        return candidates;
    }

    /// <summary>Words the refusal for a Proposal that would push the Agent count over a hire limit.</summary>
    /// <param name="refusal">The refusal <see cref="HireLimitGuard"/> returned.</param>
    private static string DescribeHireLimit(HireRefusal refusal)
    {
        return refusal.Kind == HireLimitKind.PerTeam
            ? $"Team {refusal.Team} has {refusal.Used} Agents and the limit is {refusal.Limit} per Team; propose at most {refusal.Fits}."
            : $"{refusal.Used} live processes are running and the limit is {refusal.Limit} live processes; propose at most {refusal.Fits}.";
    }
}
