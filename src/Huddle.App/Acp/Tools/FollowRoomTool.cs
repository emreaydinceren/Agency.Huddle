namespace Agency.Huddle.App.Acp.Tools;

using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Prompts;

/// <summary>Asks to be woken by every Message in a Room, even when the calling Agent is not Mentioned — roadmap item 8.</summary>
/// <remarks>
/// The counterpart of <see cref="UnfollowRoomTool"/>. Following is per (Agent, Room), held in
/// <see cref="RoomFollows"/> rather than on the Persona, and never crosses the wire: see
/// <see cref="RoomFollows"/>'s own remarks and <see cref="ReplyGate.Decide"/>'s <c>following</c>
/// parameter.
/// </remarks>
internal sealed class FollowRoomTool(RoomFollows roomFollows, ITeamDirectory teamDirectory, string callerAgentId, IPromptSource prompts) : IAppTool
{
    /// <inheritdoc />
    public string Name => "follow_room";

    /// <inheritdoc />
    public string Description => prompts.Render("tool.followRoom.description");

    /// <inheritdoc />
    public JsonObject InputSchema => TaskToolText.SingleStringSchema("roomId");

    /// <inheritdoc />
    public async Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!TaskToolText.TryGetRequiredString(arguments, "roomId", out string? roomId, out string roomIdRefusal))
        {
            return roomIdRefusal;
        }

        var room = await teamDirectory.GetRoomAsync(roomId, cancellationToken).ConfigureAwait(false);
        if (room is null)
        {
            return $"Unknown room '{roomId}'.";
        }

        // Delivery only ever reaches Members - AgentGateway.DeliverAsync skips anyone else - so
        // following a Room the caller is not in would report success here and then silently deliver
        // nothing. Refusing up front, and saying why, is cheaper than a caller discovering that on
        // the first Message that never arrives.
        var members = await teamDirectory.GetRoomMembersAsync(roomId, cancellationToken).ConfigureAwait(false);
        if (!members.Any(member => string.Equals(member.Id, callerAgentId, StringComparison.Ordinal)))
        {
            return $"You are not a member of room '{room.Name}' (id {room.Id}), so following it would be a silent no-op: delivery only ever reaches Members. Ask a member to invite you with invite_agent, or start your own Room with create_room instead.";
        }

        return roomFollows.Follow(callerAgentId, roomId)
            ? $"Now following room '{room.Name}' (id {room.Id}). You will receive every message posted there, whether or not it mentions you. Call unfollow_room when you no longer need it."
            : $"Already following room '{room.Name}' (id {room.Id}). Nothing to do.";
    }
}
