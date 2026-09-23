using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Teammates;

namespace Agency.Huddle.App.Acp.Tools;

/// <summary>
/// Checks one proposed Teammate against Spec §8.3, for free and with no side effects (Spec §5.3
/// Contract B, §6.8). Offered only to a Persona holding a Skill that lists it - see
/// <see cref="Agency.Huddle.App.Skills.SkillGrants"/>.
/// </summary>
internal sealed class ValidateTeammateTool(CandidateChecker checker, IPromptSource prompts) : IAppTool
{
    private static readonly IReadOnlyDictionary<string, string> NoValues = new Dictionary<string, string>();

    public string Name => "validate_teammate";

    public string Description => prompts.Render("tool.validateTeammate.description", NoValues);

    public JsonObject InputSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["candidate"] = new JsonObject
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

        var result = await checker.CheckAsync([candidate], cancellationToken);

        return result.IsValid ? "Valid." : string.Join('\n', result.Problems);
    }
}
