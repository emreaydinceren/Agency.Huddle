namespace Agency.Huddle.App.Acp.Tools;

using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;

/// <summary>Creates a Room containing the calling Agent, the Human, and the named agents.</summary>
internal sealed class CreateRoomTool(ChatService chat, ITeamDirectory teamDirectory, string callerAgentId, IMentionAliasSource aliasSource, IPromptSource prompts) : IAppTool
{
    public string Name => "create_room";

    public string Description => prompts.Render("tool.createRoom.description");

    public JsonObject InputSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["agents"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "string" },
            },
            ["seed"] = new JsonObject { ["type"] = "string" },
        },
        ["required"] = new JsonArray { "agents" },
    };

    public async Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (arguments["agents"] is not JsonArray agentsArray || agentsArray.Count == 0)
        {
            return "The 'agents' argument is required and must be a non-empty array of agent names.";
        }

        var names = agentsArray.Select(node => (string?)node).ToList();
        if (names.Any(string.IsNullOrWhiteSpace))
        {
            return "Every entry in 'agents' must be a non-empty agent name.";
        }

        (List<string> agentIds, List<string> unknown) = await this.ResolveAgentsAsync(names, cancellationToken).ConfigureAwait(false);
        if (unknown.Count > 0)
        {
            return await this.DescribeUnknownAgentsAsync(unknown, cancellationToken).ConfigureAwait(false);
        }

        // Read before the Room exists, so a wrongly-typed seed is refused without half-creating one.
        if (!TaskToolText.TryGetString(arguments, "seed", out string? seed, out string seedRefusal))
        {
            return seedRefusal;
        }

        return await this.CreateRoomWithSeedAsync(agentIds, seed, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves every named agent to its user id, starting with the caller. Alias-aware, matching ChatService.InviteAsync:
    /// PersonaFrontmatter.ComposeJobDescription deliberately keeps a Persona's Alias visible in list_agents' job
    /// description precisely so a reading agent learns "@jar" is a working handle for "Jarvis" - rejecting that same
    /// handle here would make the tool contradict what it just told the model. Each entry in 'agents' is a complete JSON
    /// string the model filled in, never carved out of free text by a pattern, so resolving it is a plain lookup, not
    /// the truncation-prone parsing docs/engineering/traps.md warns about.
    /// </summary>
    /// <param name="names">The non-empty agent names the model sent.</param>
    /// <param name="cancellationToken">Cancels the directory lookups.</param>
    /// <returns>The distinct agent ids to put in the Room, and the names that match no agent.</returns>
    private async Task<(List<string> AgentIds, List<string> Unknown)> ResolveAgentsAsync(List<string?> names, CancellationToken cancellationToken)
    {
        var agentIds = new List<string> { callerAgentId };
        var unknown = new List<string>();
        foreach (var name in names)
        {
            var user = await teamDirectory.FindUserByNameAsync(name!, cancellationToken).ConfigureAwait(false);
            if (user is null || user.Kind != UserKind.Agent)
            {
                var alias = aliasSource.Aliases.FirstOrDefault(a => string.Equals(a.Alias, name, StringComparison.OrdinalIgnoreCase));
                if (alias is not null)
                {
                    user = await teamDirectory.FindUserByNameAsync(alias.Name, cancellationToken).ConfigureAwait(false);
                }
            }

            if (user is null || user.Kind != UserKind.Agent)
            {
                unknown.Add(name!);
                continue;
            }

            if (!agentIds.Contains(user.Id))
            {
                agentIds.Add(user.Id);
            }
        }

        return (agentIds, unknown);
    }

    /// <summary>Words the refusal for names that match no agent, listing the agents that do exist.</summary>
    /// <param name="unknown">The names that matched no agent.</param>
    /// <param name="cancellationToken">Cancels the directory lookup.</param>
    private async Task<string> DescribeUnknownAgentsAsync(List<string> unknown, CancellationToken cancellationToken)
    {
        var existingUsers = await teamDirectory.GetUsersAsync(cancellationToken).ConfigureAwait(false);
        var existingNames = existingUsers.Where(u => u.Kind == UserKind.Agent).Select(u => u.Name).ToList();
        var existingText = existingNames.Count == 0 ? "none" : string.Join(", ", existingNames);
        return $"Unknown agent(s): {string.Join(", ", unknown)}. Agents that do exist: {existingText}.";
    }

    /// <summary>Creates the Room, then posts the optional seed Message as the caller; a failed seed post never un-creates the Room.</summary>
    /// <param name="agentIds">The user ids to put in the Room.</param>
    /// <param name="seed">The optional first Message.</param>
    /// <param name="cancellationToken">Cancels the chat calls.</param>
    private async Task<string> CreateRoomWithSeedAsync(List<string> agentIds, string? seed, CancellationToken cancellationToken)
    {
        Room room;
        try
        {
            User? caller = await teamDirectory.GetUserAsync(callerAgentId, cancellationToken).ConfigureAwait(false);
            room = await chat.CreateRoomForAsync(agentIds, null, null, new FeedActor(FeedActorKind.Agent, caller?.Name ?? "An Agent"), cancellationToken).ConfigureAwait(false);
        }
        catch (ChatException ex)
        {
            return $"Could not create the room: {ex.Message}";
        }

        // ADR-0005: "an optional 'seed' parameter on create_room that posts the first Message as part
        // of creation. Without it the sequence can half-happen, leaving a contextless Room with two
        // Agents in it and no way for them to learn why." A failed seed post must not un-create the
        // Room - the Room already exists and is worth keeping even if the very next step fails - so
        // this is its own try/catch rather than folding into the one above.
        if (string.IsNullOrWhiteSpace(seed))
        {
            return $"Created room '{room.Name}' (id {room.Id}).";
        }

        try
        {
            await chat.PostAsync(room.Id, callerAgentId, seed, ct: cancellationToken).ConfigureAwait(false);
            return $"Created room '{room.Name}' (id {room.Id}) and posted the seed message into it.";
        }
        catch (ChatException ex)
        {
            return $"Created room '{room.Name}' (id {room.Id}), but could not post the seed message: {ex.Message}";
        }
    }
}