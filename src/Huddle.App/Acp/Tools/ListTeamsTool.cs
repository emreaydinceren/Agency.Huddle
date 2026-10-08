using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Acp.Tools;

/// <summary>
/// Lists every Team's card - name, type, manager, what it owns - and how many Requests are open in its backlog (RQ-8), so a
/// manager knows where a Request belongs. Offered only to a Persona holding a Skill that lists it.
/// </summary>
internal sealed class ListTeamsTool(ITeamCatalog teams, TaskStore store) : IAppTool
{
    /// <inheritdoc />
    public string Name => "list_teams";

    /// <inheritdoc />
    public string Description =>
        "Lists every Team with its type, manager, what it owns and how many Requests are open in its backlog. " +
        "Use it to choose the Team to file a Request with.";

    /// <inheritdoc />
    public JsonObject InputSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject(),
    };

    /// <inheritdoc />
    public Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

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

        return Task.FromResult(cards.ToJsonString());
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
