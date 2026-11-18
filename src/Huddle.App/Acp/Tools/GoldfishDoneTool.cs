using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Goldfish;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Acp.Tools;

/// <summary>
/// <c>goldfish_done(result, findings?, outcome?)</c> (goldfish spec 5.3): the way out of a possession. It refuses unless the caller's
/// (Agent, Room) is possessed, otherwise ends the possession Finished or Stuck with the result and findings.
/// </summary>
internal sealed class GoldfishDoneTool(
    string callerAgentId,
    GoldfishGuard guard,
    GoldfishSupervisor supervisor,
    ITeamDirectory directory,
    IPromptSource prompts) : IAppTool
{
    /// <inheritdoc />
    public string Name => "goldfish_done";

    /// <inheritdoc />
    public string Description => prompts.Render("tool.goldfishDone.description");

    /// <inheritdoc />
    public JsonObject InputSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["result"] = new JsonObject { ["type"] = "string", ["maxLength"] = 4000 },
            ["findings"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "string", ["maxLength"] = 500 },
                ["maxItems"] = 30,
            },
            ["outcome"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray { "finished", "stuck" } },
        },
        ["required"] = new JsonArray { "result" },
    };

    /// <inheritdoc />
    public async Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        string? possessionId = guard.PossessionOf(callerAgentId);
        if (possessionId is null)
        {
            return prompts.Render("tool.goldfishDone.notPossessed");
        }

        if (!TaskToolText.TryGetRequiredString(arguments, "result", out string? text, out string refusal))
        {
            return refusal;
        }

        List<string> findings = arguments["findings"] is JsonArray array
            ? [.. array.Select(static node => node?.GetValue<string>()).OfType<string>()]
            : [];
        GoldfishExit exit = string.Equals(arguments["outcome"]?.GetValue<string>(), "stuck", StringComparison.Ordinal) ? GoldfishExit.Stuck : GoldfishExit.Finished;

        _ = await supervisor.EndAsync(possessionId, exit, null, cancellationToken, new GoldfishResult(text, findings)).ConfigureAwait(false);

        string agentName = (await directory.GetUserAsync(callerAgentId, cancellationToken).ConfigureAwait(false))?.Name ?? callerAgentId;
        return prompts.Render("tool.goldfishDone.done", new Dictionary<string, string>(StringComparer.Ordinal) { ["{{agent}}"] = agentName });
    }
}
