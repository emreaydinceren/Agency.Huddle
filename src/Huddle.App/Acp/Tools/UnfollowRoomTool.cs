namespace Agency.Huddle.App.Acp.Tools;

using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Prompts;

/// <summary>Stops being woken by every Message in a Room the calling Agent is following — the counterpart of <see cref="FollowRoomTool"/>.</summary>
/// <remarks>
/// Unlike <see cref="FollowRoomTool"/>, this does not refuse a caller who is no longer a Member: a
/// Member who was removed from a Room they still follow should still be able to clear that follow, and
/// <see cref="RoomFollows.Unfollow"/> is harmless to call for a Room the caller was never following.
/// </remarks>
internal sealed class UnfollowRoomTool(RoomFollows roomFollows, ITeamDirectory teamDirectory, string callerAgentId, IPromptSource prompts) : IAppTool
{
    private static readonly IReadOnlyDictionary<string, string> NoValues = new Dictionary<string, string>();

    /// <inheritdoc />
    public string Name => "unfollow_room";

    /// <inheritdoc />
    public string Description => prompts.Render("tool.unfollowRoom.description", NoValues);

    /// <inheritdoc />
    public JsonObject InputSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["roomId"] = new JsonObject { ["type"] = "string" },
        },
        ["required"] = new JsonArray { "roomId" },
    };

    /// <inheritdoc />
    public async Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var roomId = (string?)arguments["roomId"];
        if (string.IsNullOrWhiteSpace(roomId))
        {
            return "'roomId' is a required argument.";
        }

        var room = await teamDirectory.GetRoomAsync(roomId, cancellationToken);
        if (room is null)
        {
            return $"Unknown room '{roomId}'.";
        }

        return roomFollows.Unfollow(callerAgentId, roomId)
            ? $"No longer following room '{room.Name}' (id {room.Id}). You will now be woken there only when a message mentions you."
            : $"You were not following room '{room.Name}' (id {room.Id}). Nothing to do.";
    }
}
