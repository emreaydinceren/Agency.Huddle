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
    /// <inheritdoc />
    public string Name => "propose_sprint";

    /// <inheritdoc />
    public string Description => prompts.Render("tool.proposeSprint.description");

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

        (TaskActor? actor, string actorRefusal) = await TaskToolText.ResolveActorAsync(directory, callerAgentId, cancellationToken).ConfigureAwait(false);
        if (actor is null)
        {
            return actorRefusal;
        }

        List<TeamSummary> managed = [.. teams.Teams.Where(team => string.Equals(team.Manager, actor.Name, StringComparison.OrdinalIgnoreCase))];
        if (managed.Count == 0)
        {
            return "Only the manager of a Team can propose a Sprint.";
        }

        if (!TryReadName(arguments, out string name, out string nameRefusal))
        {
            return nameRefusal;
        }

        if (!this.TryResolveTasks(arguments, out List<TaskItem> found, out string tasksRefusal))
        {
            return tasksRefusal;
        }

        TeamSummary team = managed.FirstOrDefault(each => string.Equals(each.Name, found[0].Location.Team, StringComparison.OrdinalIgnoreCase)) ?? managed[0];
        string? membershipRefusal = CheckTasksBelongTo(team, found);
        if (membershipRefusal is not null)
        {
            return membershipRefusal;
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

    /// <summary>Reads the Sprint <c>name</c> argument: present, valid for a Sprint file and trimmed.</summary>
    /// <param name="arguments">The tool's JSON arguments.</param>
    /// <param name="name">The trimmed name when this returns <see langword="true"/>.</param>
    /// <param name="refusal">The refusal text when this returns <see langword="false"/>.</param>
    private static bool TryReadName(JsonObject arguments, out string name, out string refusal)
    {
        name = string.Empty;
        if (!TaskToolText.TryGetString(arguments, "name", out string? nameText, out refusal))
        {
            return false;
        }

        string trimmed = nameText?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            refusal = "Give the Sprint a name in 'name'.";
            return false;
        }

        if (SprintFileFormat.ValidateName(trimmed) is { } nameProblem)
        {
            refusal = nameProblem;
            return false;
        }

        name = trimmed;
        refusal = string.Empty;
        return true;
    }

    /// <summary>Resolves the <c>taskIds</c> argument to distinct Tasks; at least one id is required.</summary>
    /// <param name="arguments">The tool's JSON arguments.</param>
    /// <param name="found">The distinct Tasks, in the order named, when this returns <see langword="true"/>.</param>
    /// <param name="refusal">The refusal text when this returns <see langword="false"/>.</param>
    private bool TryResolveTasks(JsonObject arguments, out List<TaskItem> found, out string refusal)
    {
        found = [];
        if (!TaskToolText.TryGetStringList(arguments, "taskIds", out IReadOnlyList<string>? idTexts, out refusal))
        {
            return false;
        }

        if (idTexts is null || idTexts.Count == 0)
        {
            refusal = "Give at least one task id in 'taskIds'.";
            return false;
        }

        foreach (string idText in idTexts)
        {
            if (!TaskToolText.TryResolve(tasks, idText, out TaskItem? task, out refusal))
            {
                return false;
            }

            if (found.All(each => each.Id != task.Id))
            {
                found.Add(task);
            }
        }

        refusal = string.Empty;
        return true;
    }

    /// <summary>Checks that every Task belongs to <paramref name="team"/> and none is Done yet.</summary>
    /// <param name="team">The Team the Sprint is proposed for.</param>
    /// <param name="found">The Tasks to check.</param>
    /// <returns>The refusal text, or <see langword="null"/> when every Task is fine.</returns>
    private static string? CheckTasksBelongTo(TeamSummary team, List<TaskItem> found)
    {
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

        return null;
    }
}
