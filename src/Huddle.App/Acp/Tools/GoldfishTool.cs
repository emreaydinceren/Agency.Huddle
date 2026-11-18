using System.Globalization;
using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Goldfish;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Acp.Tools;

/// <summary>
/// <c>goldfish(instructions)</c> (goldfish spec 5.1): the calling Agent steps aside in the Room its Turn runs in and a clean copy
/// does the work from the brief alone. It writes the possession, posts the start row, tells the runner and returns at once;
/// the result comes back later as a new Turn. Every refusal is final text from a Prompt.
/// </summary>
internal sealed class GoldfishTool(
    string callerAgentId,
    GoldfishSupervisor supervisor,
    IPossessionLookup lookup,
    TurnActivity turnActivity,
    ITeamDirectory directory,
    TaskService tasks,
    IPromptSource prompts,
    IOptions<TeamOptions> options) : IAppTool
{
    /// <inheritdoc />
    public string Name => "goldfish";

    /// <inheritdoc />
    public string Description => prompts.Render("tool.goldfish.description");

    /// <inheritdoc />
    public JsonObject InputSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["instructions"] = new JsonObject { ["type"] = "string", ["maxLength"] = 1000 },
        },
        ["required"] = new JsonArray { "instructions" },
    };

    /// <inheritdoc />
    public async Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        string brief = arguments["instructions"]?.GetValue<string>() ?? string.Empty;
        string? refusal = this.RefuseBeforeTurn(brief);
        if (refusal is not null)
        {
            return refusal;
        }

        string? roomId = turnActivity.SoleRoom(callerAgentId);
        if (roomId is null)
        {
            return prompts.Render("tool.goldfish.noTurn");
        }

        if (lookup.PossessionOf(callerAgentId, roomId) is not null)
        {
            return prompts.Render("tool.goldfish.nested");
        }

        Possession? open = supervisor.FindOpen(callerAgentId);
        if (open is not null)
        {
            return await this.AlreadyAsync(open.RoomId, cancellationToken).ConfigureAwait(false);
        }

        return await this.StartAsync(roomId, brief, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The refusals that need no Turn: an empty brief, an over-long one, and a Huddle that runs Turns side by side.</summary>
    private string? RefuseBeforeTurn(string brief)
    {
        if (string.IsNullOrWhiteSpace(brief))
        {
            return prompts.Render("tool.goldfish.briefEmpty");
        }

        int limit = options.Value.Goldfish.MaxBriefChars;
        if (brief.Length > limit)
        {
            return prompts.Render("tool.goldfish.briefTooLong", Values(("{{count}}", Number(brief.Length)), ("{{limit}}", Number(limit))));
        }

        int concurrent = options.Value.Acp.MaxConcurrentTurns;
        return concurrent > 1 ? prompts.Render("tool.goldfish.concurrent", Values(("{{count}}", Number(concurrent)))) : null;
    }

    /// <summary>Starts the possession and, with a Task in hand, comments the brief on it.</summary>
    private async Task<string> StartAsync(string roomId, string brief, CancellationToken ct)
    {
        TaskId? inHand = turnActivity.TaskInHand(callerAgentId, roomId);
        Possession? begun = await supervisor.BeginAsync(callerAgentId, roomId, inHand?.ToString(), brief, ct).ConfigureAwait(false);
        if (begun is null)
        {
            return await this.AlreadyAsync(supervisor.FindOpen(callerAgentId)?.RoomId ?? roomId, ct).ConfigureAwait(false);
        }

        if (inHand is { } taskId)
        {
            string agentName = (await directory.GetUserAsync(callerAgentId, ct).ConfigureAwait(false))?.Name ?? callerAgentId;

            // A Task that cannot take the comment must not stop the goldfish: the possession is already written and the runner told.
            _ = TaskToolText.TryRun(() => tasks.AddComment(taskId, GoldfishLine.TaskComment(agentName, brief), TaskActors.Huddle), out _, out _);
        }

        string room = await this.RoomNameAsync(roomId, ct).ConfigureAwait(false);
        return prompts.Render("tool.goldfish.started", Values(("{{room}}", room), ("{{silentToken}}", SilentReply.Token)));
    }

    private async Task<string> AlreadyAsync(string roomId, CancellationToken ct) =>
        prompts.Render("tool.goldfish.already", Values(("{{room}}", await this.RoomNameAsync(roomId, ct).ConfigureAwait(false))));

    private async Task<string> RoomNameAsync(string roomId, CancellationToken ct) =>
        (await directory.GetRoomAsync(roomId, ct).ConfigureAwait(false))?.Name ?? roomId;

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static Dictionary<string, string> Values(params (string Key, string Value)[] pairs) =>
        pairs.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);
}
