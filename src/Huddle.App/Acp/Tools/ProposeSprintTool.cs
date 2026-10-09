namespace Agency.Huddle.App.Acp.Tools;

using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;

/// <summary>
/// Lets a Team's manager propose a Sprint to the Human (PL-4). A successful call only stores a
/// <see cref="SprintProposal"/>; it never calls <c>SprintStore.Create</c> - the Human's approval is the only path that
/// creates a Sprint (ADR-0037). Offered only to a Persona holding a Skill that lists it - see
/// <see cref="Agency.Huddle.App.Skills.SkillGrants"/>.
/// </summary>
internal sealed class ProposeSprintTool(
    TaskStore tasks,
    ISprintCatalog sprints,
    SprintProposalStore proposals,
    ITeamCatalog teams,
    ITeamDirectory directory,
    string callerAgentId,
    IPromptSource prompts) : IAppTool
{
    private static readonly IReadOnlyDictionary<string, string> NoValues = new Dictionary<string, string>();

    /// <inheritdoc />
    public string Name => "propose_sprint";

    /// <inheritdoc />
    public string Description => prompts.Render("tool.proposeSprint.description", NoValues);

    /// <inheritdoc />
    public JsonObject InputSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["name"] = new JsonObject { ["type"] = "string" },
            ["taskIds"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "string" },
            },
        },
        ["required"] = new JsonArray { "name", "taskIds" },
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

        List<TeamSummary> managed = [.. teams.Teams.Where(team => string.Equals(team.Manager, actor.Name, StringComparison.OrdinalIgnoreCase))];
        if (managed.Count == 0)
        {
            return "Only the manager of a Team can propose a Sprint.";
        }

        if (!TaskToolText.TryGetString(arguments, "name", out string? nameText, out string nameRefusal))
        {
            return nameRefusal;
        }

        string name = nameText?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            return "Give the Sprint a name in 'name'.";
        }

        if (SprintFileFormat.ValidateName(name) is { } nameProblem)
        {
            return nameProblem;
        }

        if (!TaskToolText.TryGetStringList(arguments, "taskIds", out IReadOnlyList<string>? idTexts, out string idsRefusal))
        {
            return idsRefusal;
        }

        if (idTexts is null || idTexts.Count == 0)
        {
            return "Give at least one task id in 'taskIds'.";
        }

        List<TaskItem> found = [];
        foreach (string idText in idTexts)
        {
            if (!TaskToolText.TryResolve(tasks, idText, out TaskItem? task, out string taskRefusal))
            {
                return taskRefusal;
            }

            if (found.All(each => each.Id != task.Id))
            {
                found.Add(task);
            }
        }

        TeamSummary team = managed.FirstOrDefault(each => string.Equals(each.Name, found[0].Location.Team, StringComparison.OrdinalIgnoreCase)) ?? managed[0];
        foreach (TaskItem task in found)
        {
            if (!string.Equals(task.Location.Team, team.Name, StringComparison.OrdinalIgnoreCase))
            {
                return $"Task {task.Id} belongs to Team {task.Location.Team}, not Team {team.Name}.";
            }

            if (task.Status == TaskState.Done)
            {
                return $"Task {task.Id} is already Done.";
            }
        }

        Sprint? existing = sprints.FindByName(name);
        if (existing is not null && existing.Id != sprints.Current?.Id)
        {
            return $"A Sprint named '{name}' already exists.";
        }

        if (proposals.Pending(team.Name).Any(each => string.Equals(each.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return $"A proposal named '{name}' is already waiting for the Human.";
        }

        SprintProposal proposal = new(Guid.NewGuid().ToString("N"), name, [.. found.Select(static each => each.Id.ToString())], actor.Name, team.Name);
        return proposals.Add(proposal)
            ? "Proposal sent to the Human."
            : "The proposal could not be saved; try again.";
    }
}
