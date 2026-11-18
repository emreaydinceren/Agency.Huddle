namespace Agency.Huddle.App.Acp.Tools;

using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Goldfish;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Services;

/// <summary>Posts a Message into a Room, as the calling Agent.</summary>
/// <param name="chat">Posts the Message.</param>
/// <param name="callerAgentId">The Agent this tool instance is bound to.</param>
/// <param name="prompts">Renders the description and the goldfish refusal.</param>
/// <param name="ownPosts">Records what the Agent posted.</param>
/// <param name="possessions">Tells whether a Room holds the caller's goldfish; optional, goldfish mode is off when absent.</param>
/// <param name="activity">Tells which Room the caller's Turn is in; optional.</param>
/// <param name="directory">Names the caller and the Room in the refusal; optional.</param>
internal sealed class PostMessageTool(
    ChatService chat,
    string callerAgentId,
    IPromptSource prompts,
    OwnPosts ownPosts,
    IPossessionLookup? possessions = null,
    TurnActivity? activity = null,
    Agency.Huddle.App.Data.ITeamDirectory? directory = null) : IAppTool
{
    public string Name => "post_message";

    public string Description => prompts.Render("tool.postMessage.description");

    public JsonObject InputSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["roomId"] = new JsonObject { ["type"] = "string" },
            ["text"] = new JsonObject { ["type"] = "string" },
        },
        ["required"] = new JsonArray { "roomId", "text" },
    };

    public async Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!TaskToolText.TryGetString(arguments, "roomId", out string? roomId, out string roomIdRefusal))
        {
            return roomIdRefusal;
        }

        if (!TaskToolText.TryGetString(arguments, "text", out string? text, out string textRefusal))
        {
            return textRefusal;
        }

        if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(text))
        {
            return "Both 'roomId' and 'text' are required arguments.";
        }

        if (await this.WarmBlockedAsync(roomId, cancellationToken).ConfigureAwait(false) is { } blocked)
        {
            return blocked;
        }

        try
        {
            await chat.PostAsync(roomId, callerAgentId, text, ct: cancellationToken).ConfigureAwait(false);
            ownPosts.Record(callerAgentId, roomId, text);
            return $"Posted to room '{roomId}'.";
        }
        catch (ChatException ex)
        {
            return $"Could not post the message: {ex.Message}";
        }
    }

    /// <summary>The refusal when the target Room holds the caller's goldfish and the caller's Turn is not that goldfish's own.</summary>
    /// <param name="roomId">The Room the model named.</param>
    /// <param name="cancellationToken">Cancels the directory lookups.</param>
    /// <returns>The refusal, or <see langword="null"/> when the post may go ahead.</returns>
    private async Task<string?> WarmBlockedAsync(string roomId, CancellationToken cancellationToken)
    {
        if (possessions?.PossessionOf(callerAgentId, roomId) is null
            || activity is null
            || string.Equals(activity.SoleRoom(callerAgentId), roomId, StringComparison.Ordinal))
        {
            return null;
        }

        Agency.Huddle.App.Data.Room? room = directory is null ? null : await directory.GetRoomAsync(roomId, cancellationToken).ConfigureAwait(false);
        Agency.Huddle.App.Data.User? caller = directory is null ? null : await directory.GetUserAsync(callerAgentId, cancellationToken).ConfigureAwait(false);
        return prompts.Render(
            "tool.goldfish.warmBlocked",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["{{agent}}"] = caller?.Name ?? callerAgentId,
                ["{{room}}"] = room?.Name ?? roomId,
            });
    }
}