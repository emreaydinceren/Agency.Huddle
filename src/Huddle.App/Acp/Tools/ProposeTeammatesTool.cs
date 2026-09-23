namespace Agency.Huddle.App.Acp.Tools;

using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Teammates;

/// <summary>
/// Asks the Human to approve a roster of new Teammates (Spec §6.9). Creates nothing itself - a
/// successful call only stores a <see cref="Proposal"/> for the Human to Approve or Decline; see
/// <c>ProposalService</c> (D11) for what happens next. Offered only to a Persona holding a Skill
/// that lists it - see <see cref="Agency.Huddle.App.Skills.SkillGrants"/> - the same gate
/// <see cref="ValidateTeammateTool"/> is behind.
/// </summary>
internal sealed class ProposeTeammatesTool(
    ProposalStore proposals,
    CandidateChecker checker,
    PersonaStore personas,
    ITeamDirectory directory,
    IOptions<TeamOptions> options,
    TimeProvider clock,
    string callerAgentId,
    IPromptSource prompts) : IAppTool
{
    private static readonly IReadOnlyDictionary<string, string> NoValues = new Dictionary<string, string>();

    public string Name => "propose_teammates";

    public string Description => prompts.Render("tool.proposeTeammates.description", NoValues);

    public JsonObject InputSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["roomId"] = new JsonObject { ["type"] = "string" },
            ["candidates"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["name"] = new JsonObject { ["type"] = "string" },
                        ["alias"] = new JsonObject { ["type"] = "string" },
                        ["title"] = new JsonObject { ["type"] = "string" },
                        ["body"] = new JsonObject { ["type"] = "string" },
                        ["teams"] = new JsonObject
                        {
                            ["type"] = "array",
                            ["items"] = new JsonObject { ["type"] = "string" },
                        },
                        ["consult_when"] = new JsonObject { ["type"] = "string" },
                    },
                    ["required"] = new JsonArray { "name", "alias", "title", "body" },
                },
            },
        },
        ["required"] = new JsonArray { "roomId", "candidates" },
    };

    /// <summary>
    /// Runs Spec §6.9's internal flow, in order: the Room must exist, the caller must be a Member
    /// of it, it must not be Archived, the Candidate count must be one to four, every Candidate must
    /// parse and pass <see cref="CandidateChecker"/>, and the Proposal must not push the Teammate
    /// count over <see cref="AcpOptions.MaxTeammates"/> - only then is it handed to
    /// <see cref="ProposalStore.TryPut"/>. Every early exit is a string; this never throws for an
    /// expected failure (Spec §5.3 Contract B).
    /// </summary>
    public async Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var roomId = (string?)arguments["roomId"];
        if (string.IsNullOrWhiteSpace(roomId))
        {
            return "The 'roomId' argument is required.";
        }

        var room = await directory.GetRoomAsync(roomId, cancellationToken);
        if (room is null)
        {
            return $"Unknown room '{roomId}'.";
        }

        var members = await directory.GetRoomMembersAsync(roomId, cancellationToken);
        if (!members.Any(member => string.Equals(member.Id, callerAgentId, StringComparison.Ordinal)))
        {
            return "You are not a Member of that Room.";
        }

        if (room.Archived)
        {
            return "That Room is Archived.";
        }

        if (arguments["candidates"] is not JsonArray candidatesArray || candidatesArray.Count is 0 or > 4)
        {
            return "Propose between one and four Candidates.";
        }

        var problems = new List<string>();
        var candidates = new List<Candidate>();
        for (var index = 0; index < candidatesArray.Count; index++)
        {
            if (CandidateJson.TryParse(candidatesArray[index], index + 1, out var candidate, problems) && candidate is not null)
            {
                candidates.Add(candidate);
            }
        }

        if (problems.Count > 0)
        {
            return string.Join('\n', problems);
        }

        var check = await checker.CheckAsync(candidates, cancellationToken);
        if (!check.IsValid)
        {
            return string.Join('\n', check.Problems);
        }

        var max = options.Value.Acp.MaxTeammates;
        var count = personas.Entries.Count;
        if (max > 0 && count + candidates.Count > max)
        {
            return $"{count} Teammates exist and the limit is {max}; propose at most {max - count}.";
        }

        var caller = await directory.GetUserAsync(callerAgentId, cancellationToken);
        var proposerName = caller?.Name ?? callerAgentId;

        var proposal = new Proposal(
            Guid.NewGuid().ToString("N"),
            roomId,
            callerAgentId,
            proposerName,
            candidates,
            clock.GetUtcNow());

        var put = proposals.TryPut(proposal);
        if (put.Result == ProposalPutResult.Refused)
        {
            var existingProposerName = put.Existing?.ProposerName ?? "another Agent";
            return $"A Proposal from {existingProposerName} is already waiting in this Room.";
        }

        var names = string.Join(", ", candidates.Select(candidate => candidate.Name));
        return $"Proposed {candidates.Count} Teammates ({names}) in Room '{room.Name}' (id {room.Id}). " +
            "The Human has been asked to approve. Do not create Rooms for them or mention them yet; you will be told the outcome in this Room.";
    }
}
