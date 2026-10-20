namespace Agency.Huddle.App.Acp.Tools;

using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;

/// <summary>Adds an existing Agent to a Room that already exists — the Invitation, issued by an Agent.</summary>
/// <remarks>
/// The counterpart of the Human's <c>/invite @name</c> composer command and the Add teammate control
/// on the Room header: all three end in <see cref="ChatService.InviteAsync(string, string, CancellationToken)"/>, so a Room renames
/// itself after its Agents the same way whoever issued the Invitation.
/// </remarks>
internal sealed class InviteAgentTool(ChatService chat, ITeamDirectory teamDirectory, IMentionAliasSource aliasSource, IPromptSource prompts, string? callerAgentId = null) : IAppTool
{
    /// <inheritdoc />
    public string Name => "invite_agent";

    /// <inheritdoc />
    public string Description => prompts.Render("tool.inviteAgent.description");

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

        if (!TaskToolText.TryGetString(arguments, "roomId", out string? roomId, out string roomIdRefusal))
        {
            return roomIdRefusal;
        }

        if (!TaskToolText.TryGetString(arguments, "agent", out string? agentName, out string agentNameRefusal))
        {
            return agentNameRefusal;
        }

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
        // docs/engineering/traps.md warns about.
        var user = await this.FindAgentAsync(agentName, cancellationToken).ConfigureAwait(false);
        if (user is null || user.Kind != UserKind.Agent)
        {
            return await this.DescribeUnknownAgentAsync(agentName, cancellationToken).ConfigureAwait(false);
        }

        var members = await teamDirectory.GetRoomMembersAsync(roomId, cancellationToken).ConfigureAwait(false);
        if (members.Any(m => m.Id == user.Id))
        {
            return $"{user.Name} is already a member of room '{roomId}'. Nothing to do.";
        }

        try
        {
            User? caller = callerAgentId is null ? null : await teamDirectory.GetUserAsync(callerAgentId, cancellationToken).ConfigureAwait(false);
            var room = await chat.InviteAsync(roomId, agentName, new FeedActor(FeedActorKind.Agent, caller?.Name ?? "An Agent"), cancellationToken).ConfigureAwait(false);
            var count = members.Count + 1;
            return $"Invited {user.Name} into room '{room.Name}' (id {room.Id}). It now has {count} members.";
        }
        catch (ChatException ex)
        {
            return $"Could not invite {user.Name}: {ex.Message}";
        }
    }

    /// <summary>Looks the name up as a user name first, then as a Persona Alias; the result may still not be an Agent.</summary>
    /// <param name="agentName">The name or Alias the model sent.</param>
    /// <param name="cancellationToken">Cancels the directory lookups.</param>
    private async Task<User?> FindAgentAsync(string agentName, CancellationToken cancellationToken)
    {
        var user = await teamDirectory.FindUserByNameAsync(agentName, cancellationToken).ConfigureAwait(false);
        if (user is null || user.Kind != UserKind.Agent)
        {
            var alias = aliasSource.Aliases.FirstOrDefault(a => string.Equals(a.Alias, agentName, StringComparison.OrdinalIgnoreCase));
            if (alias is not null)
            {
                user = await teamDirectory.FindUserByNameAsync(alias.Name, cancellationToken).ConfigureAwait(false);
            }
        }

        return user;
    }

    /// <summary>Words the refusal for a name that matches no Agent, listing the Agents that do exist.</summary>
    /// <param name="agentName">The name or Alias the model sent.</param>
    /// <param name="cancellationToken">Cancels the directory lookup.</param>
    private async Task<string> DescribeUnknownAgentAsync(string agentName, CancellationToken cancellationToken)
    {
        var users = await teamDirectory.GetUsersAsync(cancellationToken).ConfigureAwait(false);
        var existingNames = users.Where(u => u.Kind == UserKind.Agent).Select(u => u.Name).ToList();
        var existingText = existingNames.Count == 0 ? "none" : string.Join(", ", existingNames);
        return $"Unknown agent '{agentName}'. Agents that do exist: {existingText}.";
    }
}
