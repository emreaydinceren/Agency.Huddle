using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
    IPromptSource prompts,
    ITeamRenames? renames = null) : IAppTool
{
    private static readonly string[] Kinds = ["bug", "feature", "feedback", "question"];

    /// <inheritdoc />
    public string Name => "file_request";

    /// <inheritdoc />
    public string Description => prompts.Render("tool.fileRequest.description");

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

        (TaskActor? actor, string actorRefusal) = await TaskToolText.ResolveActorAsync(directory, callerAgentId, cancellationToken).ConfigureAwait(false);
        if (actor is null)
        {
            return actorRefusal;
        }

        if (!this.TryResolveFromTeam(arguments, actor, out TeamSummary? fromTeam, out string fromTeamRefusal))
        {
            return fromTeamRefusal;
        }

        if (!TryReadTexts(arguments, out RequestTexts? texts, out string textsRefusal))
        {
            return textsRefusal;
        }

        if (!this.TryResolveToTeam(texts.ToTeam, fromTeam, out TeamSummary? toTeam, out string toTeamRefusal))
        {
            return toTeamRefusal;
        }

        if (!TryParseKind(texts, out RequestKind kind, out string kindRefusal))
        {
            return kindRefusal;
        }

        if (!this.TryResolveRelated(texts.Related, fromTeam, out TaskItem? related, out string relatedRefusal))
        {
            return relatedRefusal;
        }

        if (!this.TryCreateRequest(kind, fromTeam, toTeam, texts, actor, out TaskItem? request, out string createRefusal))
        {
            return createRefusal;
        }

        string block = related is null ? string.Empty : this.BlockRelated(related, request, actor);

        RequestTransition transition = RequestLifecycle.Apply(new RequestState(string.Empty, TriageState.None), RequestEvent.Filed);
        await notifier.NotifyAsync(request, RequestEvent.Filed, transition.Wake, actor.Name, cancellationToken).ConfigureAwait(false);

        return string.Create(CultureInfo.InvariantCulture, $"Filed {request.Id} in {toTeam.Name}.{block}");
    }

    /// <summary>Picks the Team the Request is filed from: the one named in <c>fromTeam</c>, or the caller's only managed Team.</summary>
    /// <param name="arguments">The tool's JSON arguments.</param>
    /// <param name="actor">The caller.</param>
    /// <param name="fromTeam">The requesting Team when this returns <see langword="true"/>.</param>
    /// <param name="refusal">The refusal text when this returns <see langword="false"/>.</param>
    private bool TryResolveFromTeam(JsonObject arguments, TaskActor actor, [NotNullWhen(true)] out TeamSummary? fromTeam, out string refusal)
    {
        fromTeam = null;
        List<TeamSummary> managed = [.. teams.Teams.Where(team => string.Equals(team.Manager, actor.Name, StringComparison.OrdinalIgnoreCase))];
        if (managed.Count == 0)
        {
            refusal = "Only managers file Requests; ask your own manager.";
            return false;
        }

        if (!TaskToolText.TryGetString(arguments, "fromTeam", out string? fromTeamText, out refusal))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(fromTeamText))
        {
            fromTeam = managed.FirstOrDefault(team => string.Equals(team.Name, fromTeamText.Trim(), StringComparison.OrdinalIgnoreCase));
            if (fromTeam is null)
            {
                refusal = string.Create(CultureInfo.InvariantCulture, $"You do not manage a Team named '{fromTeamText.Trim()}'.");
                return false;
            }
        }
        else if (managed.Count > 1)
        {
            string names = string.Join(", ", managed.Select(static team => team.Name).Order(StringComparer.Ordinal));
            refusal = string.Create(CultureInfo.InvariantCulture, $"You manage several Teams ({names}); name the Team filing this Request in 'fromTeam'.");
            return false;
        }
        else
        {
            fromTeam = managed[0];
        }

        refusal = string.Empty;
        return true;
    }

    /// <summary>Reads the five text arguments in order, then checks that the required ones are not blank.</summary>
    /// <param name="arguments">The tool's JSON arguments.</param>
    /// <param name="texts">The argument texts when this returns <see langword="true"/>.</param>
    /// <param name="refusal">The refusal text when this returns <see langword="false"/>.</param>
    private static bool TryReadTexts(JsonObject arguments, [NotNullWhen(true)] out RequestTexts? texts, out string refusal)
    {
        texts = null;
        if (!TaskToolText.TryGetString(arguments, "toTeam", out string? toTeamText, out refusal)
            || !TaskToolText.TryGetString(arguments, "kind", out string? kindText, out refusal)
            || !TaskToolText.TryGetString(arguments, "title", out string? titleText, out refusal)
            || !TaskToolText.TryGetString(arguments, "body", out string? bodyText, out refusal)
            || !TaskToolText.TryGetString(arguments, "relatedTaskId", out string? relatedText, out refusal))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(toTeamText) || string.IsNullOrWhiteSpace(titleText) || string.IsNullOrWhiteSpace(bodyText))
        {
            refusal = "'toTeam', 'kind', 'title' and 'body' are all required.";
            return false;
        }

        texts = new RequestTexts(toTeamText, kindText, titleText, bodyText, relatedText);
        refusal = string.Empty;
        return true;
    }

    /// <summary>Finds the receiving Team, which must exist and must not be the requesting Team.</summary>
    /// <param name="toTeamText">The <c>toTeam</c> argument.</param>
    /// <param name="fromTeam">The requesting Team.</param>
    /// <param name="toTeam">The receiving Team when this returns <see langword="true"/>.</param>
    /// <param name="refusal">The refusal text when this returns <see langword="false"/>.</param>
    private bool TryResolveToTeam(string toTeamText, TeamSummary fromTeam, [NotNullWhen(true)] out TeamSummary? toTeam, out string refusal)
    {
        toTeam = teams.Find(toTeamText.Trim());
        if (toTeam is null)
        {
            string asked = toTeamText.Trim();
            if (renames is not null && renames.Resolve(asked) is { } today && !string.Equals(today, asked, StringComparison.OrdinalIgnoreCase))
            {
                refusal = string.Create(CultureInfo.InvariantCulture, $"Team '{asked}' was renamed to '{today}'. Use '{today}'.");
                return false;
            }

            refusal = string.Create(CultureInfo.InvariantCulture, $"There is no Team named '{toTeamText.Trim()}'; call list_teams.");
            return false;
        }

        if (string.Equals(toTeam.Name, fromTeam.Name, StringComparison.OrdinalIgnoreCase))
        {
            refusal = "A Request goes to another Team; use create_task for your own Team's work.";
            return false;
        }

        refusal = string.Empty;
        return true;
    }

    /// <summary>Parses <c>kind</c> and checks that the body has content under each heading that kind requires.</summary>
    /// <param name="texts">The argument texts.</param>
    /// <param name="kind">The parsed kind when this returns <see langword="true"/>.</param>
    /// <param name="refusal">The refusal text when this returns <see langword="false"/>.</param>
    private static bool TryParseKind(RequestTexts texts, out RequestKind kind, out string refusal)
    {
        string kindWire = (texts.Kind ?? string.Empty).Trim().ToLowerInvariant();
        if (!Kinds.Contains(kindWire, StringComparer.Ordinal) || !Enum.TryParse(kindWire, ignoreCase: true, out kind))
        {
            kind = default;
            refusal = "'kind' must be one of bug, feature, feedback, question.";
            return false;
        }

        foreach (string section in RequestFields.RequiredSections(kind))
        {
            if (!HasSectionContent(texts.Body, section))
            {
                refusal = string.Create(CultureInfo.InvariantCulture, $"A {kindWire} Request needs content under the '## {section}' heading.");
                return false;
            }
        }

        refusal = string.Empty;
        return true;
    }

    /// <summary>Resolves the optional related Task, which must belong to the requesting Team.</summary>
    /// <param name="relatedText">The <c>relatedTaskId</c> argument, if any.</param>
    /// <param name="fromTeam">The requesting Team.</param>
    /// <param name="related">The related Task, or null when none was named.</param>
    /// <param name="refusal">The refusal text when this returns <see langword="false"/>.</param>
    private bool TryResolveRelated(string? relatedText, TeamSummary fromTeam, out TaskItem? related, out string refusal)
    {
        related = null;
        if (string.IsNullOrWhiteSpace(relatedText))
        {
            refusal = string.Empty;
            return true;
        }

        if (!TaskToolText.TryResolve(store, relatedText, out related, out refusal))
        {
            return false;
        }

        if (!string.Equals(related.Location.Team, fromTeam.Name, StringComparison.OrdinalIgnoreCase))
        {
            refusal = "The related Task must be one of your own Team's Tasks.";
            return false;
        }

        refusal = string.Empty;
        return true;
    }

    /// <summary>Creates the Request Task in the receiving Team's folder through <see cref="TaskService.Create"/>.</summary>
    /// <param name="kind">The Request kind.</param>
    /// <param name="fromTeam">The requesting Team.</param>
    /// <param name="toTeam">The receiving Team.</param>
    /// <param name="texts">The argument texts.</param>
    /// <param name="actor">The caller.</param>
    /// <param name="request">The created Task when this returns <see langword="true"/>.</param>
    /// <param name="refusal">The refusal text when this returns <see langword="false"/>.</param>
    private bool TryCreateRequest(
        RequestKind kind,
        TeamSummary fromTeam,
        TeamSummary toTeam,
        RequestTexts texts,
        TaskActor actor,
        [NotNullWhen(true)] out TaskItem? request,
        out string refusal)
    {
        request = null;
        RequestFields fields = new(kind, fromTeam.Name, actor.Name, TriageState.None, string.Empty, string.Empty);
        TaskDraft draft = new(texts.Title.Trim(), toTeam.Name, null, Description: texts.Body) { ExtraFields = [.. fields.ToFrontmatter()] };
        if (!TaskToolText.TryRun(() => tasks.Create(draft, actor), out TaskResult? result, out refusal))
        {
            return false;
        }

        switch (result)
        {
            case TaskResult.Saved saved:
                request = saved.Task;
                refusal = string.Empty;
                return true;
            case TaskResult.Refused refused:
                refusal = string.Join('\n', refused.Problems);
                return false;
            case TaskResult.GateRefused gateRefused:
                refusal = gateRefused.Message;
                return false;
            default:
                throw new UnreachableException("TaskService.Create returns only Saved, Refused or GateRefused.");
        }
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

    /// <summary>The five text arguments, once each is known to be a string and the required ones are not blank.</summary>
    /// <param name="ToTeam">The non-blank receiving Team name.</param>
    /// <param name="Kind">The kind text, if any.</param>
    /// <param name="Title">The non-blank title.</param>
    /// <param name="Body">The non-blank body.</param>
    /// <param name="Related">The related Task id text, if any.</param>
    private sealed record RequestTexts(string ToTeam, string? Kind, string Title, string Body, string? Related);
}
