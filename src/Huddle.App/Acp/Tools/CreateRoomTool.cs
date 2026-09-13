namespace Agency.Huddle.App.Acp.Tools;

using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Hooks;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;

/// <summary>Creates a Room containing the calling Agent, the Human, and the named agents.</summary>
internal sealed class CreateRoomTool(ChatService chat, ITeamDirectory teamDirectory, string callerAgentId, IMentionAliasSource aliasSource, IHookSource hooks) : IAppTool
{
    private static readonly IReadOnlyDictionary<string, string> NoValues = new Dictionary<string, string>();

    public string Name => "create_room";

    public string Description => hooks.Render("tool.createRoom.description", NoValues);

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

        // Alias-aware, matching ChatService.InviteAsync: PersonaFrontmatter.ComposeJobDescription
        // deliberately keeps a Persona's Alias visible in list_agents' job description precisely so a
        // reading agent learns "@jar" is a working handle for "Jarvis" - rejecting that same handle
        // here would make the tool contradict what it just told the model. Each entry in 'agents' is
        // a complete JSON string the model filled in, never carved out of free text by a pattern, so
        // resolving it is a plain lookup, not the truncation-prone parsing docs/agencyteam/traps.md
        // warns about.
        var agentIds = new List<string> { callerAgentId };
        var unknown = new List<string>();
        foreach (var name in names)
        {
            var user = await teamDirectory.FindUserByNameAsync(name!, cancellationToken);
            if (user is null || user.Kind != UserKind.Agent)
            {
                var alias = aliasSource.Aliases.FirstOrDefault(a => string.Equals(a.Alias, name, StringComparison.OrdinalIgnoreCase));
                if (alias is not null)
                {
                    user = await teamDirectory.FindUserByNameAsync(alias.Name, cancellationToken);
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

        if (unknown.Count > 0)
        {
            var existingUsers = await teamDirectory.GetUsersAsync(cancellationToken);
            var existingNames = existingUsers.Where(u => u.Kind == UserKind.Agent).Select(u => u.Name).ToList();
            var existingText = existingNames.Count == 0 ? "none" : string.Join(", ", existingNames);
            return $"Unknown agent(s): {string.Join(", ", unknown)}. Agents that do exist: {existingText}.";
        }

        try
        {
            var room = await chat.CreateRoomForAsync(agentIds, cancellationToken);
            return $"Created room '{room.Name}' (id {room.Id}).";
        }
        catch (ChatException ex)
        {
            return $"Could not create the room: {ex.Message}";
        }
    }
}