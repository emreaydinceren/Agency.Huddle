using System.Globalization;
using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Acp.Tools;

/// <summary>
/// The requesting Team's QA confirming a finished Request before it closes (RQ-5): the caller must be a member of the
/// <c>from_team</c> whose role covers <c>verify</c>, and the Request must be Done. Records <c>confirmed_by</c>, then closes the
/// Task through <see cref="TaskService.Close"/>. Offered only to a Persona holding a Skill that lists it.
/// </summary>
internal sealed class ConfirmRequestTool(
    ITeamCatalog teams,
    TaskService tasks,
    TaskStore store,
    PersonaStore personas,
    ProcessGateService gates,
    IRequestNotifier notifier,
    ITeamDirectory directory,
    string callerAgentId) : IAppTool
{
    /// <summary>The role coverage step a confirming member must have.</summary>
    private const string VerifyCoverage = "verify";

    /// <inheritdoc />
    public string Name => "confirm_request";

    /// <inheritdoc />
    public string Description =>
        "Confirms that a Done Request you filed was delivered as asked, and closes it. Only your Team's QA may call it. " +
        "Optionally add a note.";

    /// <inheritdoc />
    public JsonObject InputSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["taskId"] = new JsonObject { ["type"] = "string" },
            ["note"] = new JsonObject { ["type"] = "string" },
        },
        ["required"] = new JsonArray { "taskId" },
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

        if (!TaskToolText.TryGetString(arguments, "taskId", out string? taskIdText, out string taskIdRefusal))
        {
            return taskIdRefusal;
        }

        if (!TaskToolText.TryGetString(arguments, "note", out string? noteText, out string noteRefusal))
        {
            return noteRefusal;
        }

        if (!RequestTaskLookup.TryResolve(store, taskIdText, out TaskItem? request, out RequestFields? fields, out string lookupRefusal))
        {
            return lookupRefusal;
        }

        bool isRequesterQa = this.IsVerifierOf(fields.FromTeam, actor.Name);
        RequestTransition transition = RequestLifecycle.Apply(new RequestState(request.Status.ToWire(), fields.Triage), RequestEvent.Confirm, isRequesterQa);
        if (!transition.Allowed)
        {
            return transition.Error ?? string.Empty;
        }

        string note = (noteText ?? string.Empty).Trim();
        TaskPatch patch = new()
        {
            Description = note.Length > 0 ? string.Concat(request.Description, "\n\n## Confirmation\n", note) : null,
            ExtraFields = [.. (fields with { ConfirmedBy = actor.Name }).ToFrontmatter()],
        };

        if (!TaskToolText.TryRun(() => tasks.Update(request.Id, patch, request.Version, actor), out TaskResult? updateResult, out string updateRunRefusal))
        {
            return updateRunRefusal;
        }

        if (!RequestTaskLookup.TrySaved(updateResult,out TaskItem? confirmed, out string updateRefusal))
        {
            return updateRefusal;
        }

        if (!TaskToolText.TryRun(() => tasks.Close(confirmed.Id, actor), out TaskResult? closeResult, out string closeRunRefusal))
        {
            return closeRunRefusal;
        }

        if (!RequestTaskLookup.TrySaved(closeResult,out TaskItem? closed, out string closeRefusal))
        {
            return closeRefusal;
        }

        await notifier.NotifyAsync(closed, RequestEvent.Confirm, transition.Wake, actor.Name, cancellationToken);

        return string.Create(CultureInfo.InvariantCulture, $"Confirmed {closed.Id}; it is closed.");
    }

    /// <summary>Whether <paramref name="member"/> belongs to <paramref name="team"/> in a role whose coverage includes <c>verify</c>.</summary>
    /// <param name="team">The requesting Team's name.</param>
    /// <param name="member">The caller's name.</param>
    /// <returns><see langword="true"/> for a QA member of that Team.</returns>
    private bool IsVerifierOf(string team, string member)
    {
        TeamSummary? summary = teams.Find(team);
        if (summary is null || !summary.Members.Contains(member, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        return personas.Get(member) is { } persona
            && PersonaFrontmatter.TryReadIdentity(persona.Text, out PersonaIdentity? identity, out _)
            && identity.Role is { } role
            && gates.CoverageOf(role) is { } coverage
            && coverage.Split(',', StringSplitOptions.TrimEntries).Contains(VerifyCoverage, StringComparer.OrdinalIgnoreCase);
    }
}
