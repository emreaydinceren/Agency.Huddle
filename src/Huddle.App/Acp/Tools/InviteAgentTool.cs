namespace Agency.Huddle.App.Acp.Tools;

using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Hooks;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;

/// <summary>Adds an existing Agent to a Room that already exists — the Invitation, issued by an Agent.</summary>
/// <remarks>
/// The counterpart of the Human's <c>/invite @name</c> composer command and the Add teammate control
/// on the Room header: all three end in <see cref="ChatService.InviteAsync"/>, so a Room renames
/// itself after its Agents the same way whoever issued the Invitation.
/// </remarks>
internal sealed class InviteAgentTool(ChatService chat, ITeamDirectory teamDirectory, IMentionAliasSource aliasSource, IHookSource hooks) : IAppTool
{
    private static readonly IReadOnlyDictionary<string, string> NoValues = new Dictionary<string, string>();

    /// <inheritdoc />
    public string Name => "invite_agent";

    /// <inheritdoc />
    public string Description => hooks.Render("tool.inviteAgent.description", NoValues);

    /// <inheritdoc />
    public JsonObject InputSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["roomId"] = new JsonObject { ["type"] = "string" },
            ["agent"] = new JsonObject { ["type"] = "string" },
        },
        ["required"] = new JsonArray { "roomId", "agent" },
    };

    /// <inheritdoc />
    public async Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var roomId = (string?)arguments["roomId"];
        var agentName = (string?)arguments["agent"];
        if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(agentName))
        {
            return "Both 'roomId' and 'agent' are required arguments.";
        }

        // Resolved here rather than left to InviteAsync so an unknown name comes back with the names
        // that do exist: the model reads the list and corrects itself, exactly as it does for
        // create_room. Error text, never a thrown exception.
        //
        // Alias-aware, matching ChatService.InviteAsync: PersonaFrontmatter.ComposeJobDescription
        // deliberately keeps a Persona's Alias visible in list_agents' job description precisely so a
        // reading agent learns "@jar" is a working handle for "Jarvis" - rejecting that same handle
        // here would make the tool contradict what it just told the model. 'agent' arrives as a
        // complete JSON string argument the model filled in, never carved out of free text by a
        // pattern, so resolving it is a plain lookup, not the truncation-prone parsing
        // docs/agencyteam/traps.md warns about.
        var user = await teamDirectory.FindUserByNameAsync(agentName, cancellationToken);
        if (user is null || user.Kind != UserKind.Agent)
        {
            var alias = aliasSource.Aliases.FirstOrDefault(a => string.Equals(a.Alias, agentName, StringComparison.OrdinalIgnoreCase));
            if (alias is not null)
            {
                user = await teamDirectory.FindUserByNameAsync(alias.Name, cancellationToken);
            }
        }

        if (user is null || user.Kind != UserKind.Agent)
        {
            var users = await teamDirectory.GetUsersAsync(cancellationToken);
            var existingNames = users.Where(u => u.Kind == UserKind.Agent).Select(u => u.Name).ToList();
            var existingText = existingNames.Count == 0 ? "none" : string.Join(", ", existingNames);
            return $"Unknown agent '{agentName}'. Agents that do exist: {existingText}.";
        }

        var members = await teamDirectory.GetRoomMembersAsync(roomId, cancellationToken);
        if (members.Any(m => m.Id == user.Id))
        {
            return $"{user.Name} is already a member of room '{roomId}'. Nothing to do.";
        }

        try
        {
            var room = await chat.InviteAsync(roomId, agentName, cancellationToken);
            var count = members.Count + 1;
            return $"Invited {user.Name} into room '{room.Name}' (id {room.Id}). It now has {count} members.";
        }
        catch (ChatException ex)
        {
            return $"Could not invite {user.Name}: {ex.Message}";
        }
    }
}
