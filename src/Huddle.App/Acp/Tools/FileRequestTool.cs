using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Acp.Tools;

/// <summary>
/// Files a Request into another Team's backlog (RQ-1, RQ-8): a Task created in the receiving Team's own folder through
/// <see cref="TaskService.Create"/>, carrying the six Request fields. Only a manager may call it, and nothing is written to
/// the receiving Team's workspace (RQ-10). Offered only to a Persona holding a Skill that lists it.
/// </summary>
internal sealed class FileRequestTool(
    ITeamCatalog teams,
    TaskService tasks,
    TaskStore store,
    IRequestNotifier notifier,
    ITeamDirectory directory,
    string callerAgentId,
    IPromptSource prompts) : IAppTool
{
    private static readonly string[] Kinds = ["bug", "feature", "feedback", "question"];

    private static readonly IReadOnlyDictionary<string, string> NoValues = new Dictionary<string, string>();

    /// <inheritdoc />
    public string Name => "file_request";

    /// <inheritdoc />
    public string Description => prompts.Render("tool.fileRequest.description", NoValues);

    /// <inheritdoc />
    public JsonObject InputSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["toTeam"] = new JsonObject { ["type"] = "string" },
            ["kind"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray { "bug", "feature", "feedback", "question" } },
            ["title"] = new JsonObject { ["type"] = "string" },
            ["body"] = new JsonObject { ["type"] = "string" },
            ["relatedTaskId"] = new JsonObject { ["type"] = "string" },
            ["fromTeam"] = new JsonObject { ["type"] = "string" },
        },
        ["required"] = new JsonArray { "toTeam", "kind", "title", "body" },
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
            return "Only managers file Requests; ask your own manager.";
        }

        if (!TaskToolText.TryGetString(arguments, "fromTeam", out string? fromTeamText, out string fromTeamRefusal))
        {
            return fromTeamRefusal;
        }

        TeamSummary? fromTeam;
        if (!string.IsNullOrWhiteSpace(fromTeamText))
        {
            fromTeam = managed.FirstOrDefault(team => string.Equals(team.Name, fromTeamText.Trim(), StringComparison.OrdinalIgnoreCase));
            if (fromTeam is null)
            {
                return string.Create(CultureInfo.InvariantCulture, $"You do not manage a Team named '{fromTeamText.Trim()}'.");
            }
        }
        else if (managed.Count > 1)
        {
            string names = string.Join(", ", managed.Select(static team => team.Name).Order(StringComparer.Ordinal));
            return string.Create(CultureInfo.InvariantCulture, $"You manage several Teams ({names}); name the Team filing this Request in 'fromTeam'.");
        }
        else
        {
            fromTeam = managed[0];
        }

        if (!TaskToolText.TryGetString(arguments, "toTeam", out string? toTeamText, out string toTeamRefusal))
        {
            return toTeamRefusal;
        }

        if (!TaskToolText.TryGetString(arguments, "kind", out string? kindText, out string kindRefusal))
        {
            return kindRefusal;
        }

        if (!TaskToolText.TryGetString(arguments, "title", out string? titleText, out string titleRefusal))
        {
            return titleRefusal;
        }

        if (!TaskToolText.TryGetString(arguments, "body", out string? bodyText, out string bodyRefusal))
        {
            return bodyRefusal;
        }

        if (!TaskToolText.TryGetString(arguments, "relatedTaskId", out string? relatedText, out string relatedRefusal))
        {
            return relatedRefusal;
        }

        if (string.IsNullOrWhiteSpace(toTeamText) || string.IsNullOrWhiteSpace(titleText) || string.IsNullOrWhiteSpace(bodyText))
        {
            return "'toTeam', 'kind', 'title' and 'body' are all required.";
        }

        TeamSummary? toTeam = teams.Find(toTeamText.Trim());
        if (toTeam is null)
        {
            return string.Create(CultureInfo.InvariantCulture, $"There is no Team named '{toTeamText.Trim()}'; call list_teams.");
        }

        if (string.Equals(toTeam.Name, fromTeam.Name, StringComparison.OrdinalIgnoreCase))
        {
            return "A Request goes to another Team; use create_task for your own Team's work.";
        }

        string kindWire = (kindText ?? string.Empty).Trim().ToLowerInvariant();
        if (!Kinds.Contains(kindWire, StringComparer.Ordinal) || !Enum.TryParse(kindWire, ignoreCase: true, out RequestKind kind))
        {
            return "'kind' must be one of bug, feature, feedback, question.";
        }

        foreach (string section in RequestFields.RequiredSections(kind))
        {
            if (!HasSectionContent(bodyText, section))
            {
                return string.Create(CultureInfo.InvariantCulture, $"A {kindWire} Request needs content under the '## {section}' heading.");
            }
        }

        TaskItem? related = null;
        if (!string.IsNullOrWhiteSpace(relatedText))
        {
            if (!TaskToolText.TryResolve(store, relatedText, out related, out string resolveRefusal))
            {
                return resolveRefusal;
            }

            if (!string.Equals(related.Location.Team, fromTeam.Name, StringComparison.OrdinalIgnoreCase))
            {
                return "The related Task must be one of your own Team's Tasks.";
            }
        }

        RequestFields fields = new(kind, fromTeam.Name, actor.Name, TriageState.None, string.Empty, string.Empty);
        TaskDraft draft = new(titleText.Trim(), toTeam.Name, null, Description: bodyText) { ExtraFields = [.. fields.ToFrontmatter()] };
        if (!TaskToolText.TryRun(() => tasks.Create(draft, actor), out TaskResult? result, out string runRefusal))
        {
            return runRefusal;
        }

        TaskItem request;
        switch (result)
        {
            case TaskResult.Saved saved:
                request = saved.Task;
                break;
            case TaskResult.Refused refused:
                return string.Join('\n', refused.Problems);
            case TaskResult.GateRefused gateRefused:
                return gateRefused.Message;
            default:
                throw new UnreachableException("TaskService.Create returns only Saved, Refused or GateRefused.");
        }

        string block = related is null ? string.Empty : this.BlockRelated(related, request, actor);

        RequestTransition transition = RequestLifecycle.Apply(new RequestState(string.Empty, TriageState.None), RequestEvent.Filed);
        await notifier.NotifyAsync(request, RequestEvent.Filed, transition.Wake, actor.Name, cancellationToken);

        return string.Create(CultureInfo.InvariantCulture, $"Filed {request.Id} in {toTeam.Name}.{block}");
    }

    /// <summary>Makes the requesting Team's related Task wait on the new Request so that the Request closing wakes it (RQ-6).</summary>
    /// <param name="related">The related Task, in the requesting Team.</param>
    /// <param name="request">The Request just filed.</param>
    /// <param name="actor">The caller.</param>
    /// <returns>An empty string on success; a sentence saying the Task was not updated otherwise.</returns>
    private string BlockRelated(TaskItem related, TaskItem request, TaskActor actor)
    {
        TaskPatch patch = new() { BlockedBy = [.. related.BlockedBy, request.Id] };
        TaskResult update = tasks.Update(related.Id, patch, related.Version, actor);
        return update is TaskResult.Saved
            ? string.Empty
            : string.Create(CultureInfo.InvariantCulture, $" {related.Id} was not marked as blocked by it.");
    }

    /// <summary>Whether <paramref name="body"/> has a <c>## heading</c> line for <paramref name="heading"/> followed by some text before the next heading.</summary>
    /// <param name="body">The Request body.</param>
    /// <param name="heading">The section name without the <c>## </c> prefix.</param>
    /// <returns><see langword="true"/> when the section exists and is not blank.</returns>
    private static bool HasSectionContent(string body, string heading)
    {
        bool inSection = false;
        foreach (string rawLine in body.Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.StartsWith('#'))
            {
                inSection = string.Equals(line, string.Concat("## ", heading), StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (inSection && line.Length > 0)
            {
                return true;
            }
        }

        return false;
    }
}
