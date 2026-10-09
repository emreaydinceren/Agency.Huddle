using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teammates;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Acp.Tools;

/// <summary>
/// Lists every Team's card - name, type, manager, what it owns - and how many Requests are open in its backlog (RQ-8), so a
/// manager knows where a Request belongs. The built-in Chief of Staff, which manages no Team, may also list them for its roll-up (OR-3).
/// Offered only to a Persona holding a Skill that lists it.
/// </summary>
internal sealed class ListTeamsTool(ITeamCatalog teams, TaskStore store, PersonaStore personas, ITeamDirectory directory, string callerAgentId, IPromptSource prompts) : IAppTool
{
    /// <inheritdoc />
    public string Name => "list_teams";

    /// <summary>The placeholder values a prompt renders with; the tool descriptions take none.</summary>
    private static readonly IReadOnlyDictionary<string, string> NoValues = new Dictionary<string, string>();

    /// <inheritdoc />
    public string Description => prompts.Render("tool.listTeams.description", NoValues);

    /// <inheritdoc />
    public JsonObject InputSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject(),
    };

    /// <inheritdoc />
    public async Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        (TaskActor? actor, string actorRefusal) = await TaskToolText.ResolveActorAsync(directory, callerAgentId, cancellationToken);
        if (actor is null)
        {
            return actorRefusal;
        }

        bool isManager = teams.Teams.Any(team => string.Equals(team.Manager, actor.Name, StringComparison.OrdinalIgnoreCase));
        bool isChiefOfStaff = personas.Entries.Any(entry =>
            string.Equals(entry.Builtin, BuiltinTeammate.ChiefOfStaffMarker, StringComparison.Ordinal)
            && string.Equals(entry.Name, actor.Name, StringComparison.OrdinalIgnoreCase));
        if (!isManager && !isChiefOfStaff)
        {
            return "Only managers list Teams; ask your own manager.";
        }

        JsonArray cards = [];
        foreach (TeamSummary team in teams.Teams)
        {
            cards.Add(new JsonObject
            {
                ["name"] = team.Name,
                ["type"] = team.TeamType?.Id ?? string.Empty,
                ["manager"] = team.Manager ?? string.Empty,
                ["owns"] = team.Owns ?? string.Empty,
                ["openRequests"] = this.CountOpenRequests(team.Name),
            });
        }

        return cards.ToJsonString();
    }

    /// <summary>Counts the Requests in a Team's backlog that are neither closed nor in a Won't do state.</summary>
    /// <param name="team">The Team name.</param>
    /// <returns>The number of open Requests.</returns>
    private int CountOpenRequests(string team)
    {
        int count = 0;
        foreach (TaskItem task in store.All)
        {
            if (!string.Equals(task.Location.Team, team, StringComparison.OrdinalIgnoreCase) || task.Location.Closed || task.Status.IsWontDo())
            {
                continue;
            }

            Dictionary<string, string> frontmatter = new(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> field in task.UnknownFields)
            {
                _ = frontmatter.TryAdd(field.Key, field.Value);
            }

            if (RequestFields.TryRead(frontmatter, out _))
            {
                count++;
            }
        }

        return count;
    }
}
