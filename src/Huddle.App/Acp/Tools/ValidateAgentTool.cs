using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Agents;

namespace Agency.Huddle.App.Acp.Tools;

/// <summary>
/// Checks one proposed Agent against Spec §8.3, for free and with no side effects (Spec §5.3
/// Contract B, §6.8). Offered only to a Persona holding a Skill that lists it - see
/// <see cref="Agency.Huddle.App.Skills.SkillGrants"/>.
/// </summary>
internal sealed class ValidateAgentTool(CandidateChecker checker, IPromptSource prompts) : IAppTool
{
    public string Name => "validate_agent";

    public string Description => prompts.Render("tool.validateAgent.description");

    public JsonObject InputSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["candidate"] = CandidateJson.Schema(),
        },
        ["required"] = new JsonArray { "candidate" },
    };

    public async Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var problems = new List<string>();

        // CandidateJson.TryParse already reports "not a JSON object" for a missing 'candidate' key
        // (arguments["candidate"] is null in that case) exactly as it does for one present but of
        // the wrong shape, so a missing Candidate needs no separate check here (Spec §8.3 order 1).
        if (!CandidateJson.TryParse(arguments["candidate"], 1, out var candidate, problems) || candidate is null)
        {
            return string.Join('\n', problems);
        }

        var result = await checker.CheckAsync([candidate], cancellationToken).ConfigureAwait(false);

        return result.IsValid ? "Valid." : string.Join('\n', result.Problems);
    }
}
