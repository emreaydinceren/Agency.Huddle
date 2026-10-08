using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Acp.Tools;

/// <summary>
/// Lets the Team's manager or Retro agent close a Done or Partial row of the Retro register at the next retro (RT-7): Verified,
/// Partial or Changed. Verified needs evidence that links a Task id, a log line or a retro file, so a row is never verified on
/// opinion. Offered only to a Persona holding a Skill that lists it.
/// </summary>
internal sealed class MarkRetroRowTool(
    ITeamCatalog teams,
    PersonaStore personas,
    TeamTypeCatalog types,
    RetroFileStore register,
    ITeamDirectory directory,
    string callerAgentId) : IAppTool
{
    /// <inheritdoc />
    public string Name => "mark_retro_row";

    /// <inheritdoc />
    public string Description =>
        "Marks a Done or Partial row of the Team's Retro register as Verified, Partial or Changed after checking it against later evidence. " +
        "Verified needs evidence that links a Task id, a logs/... line (logs/...#L12) or a _retros/ file.";

    /// <inheritdoc />
    public JsonObject InputSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["team"] = new JsonObject { ["type"] = "string" },
            ["rowId"] = new JsonObject { ["type"] = "string" },
            ["status"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray { "Verified", "Partial", "Changed" } },
            ["evidence"] = new JsonObject { ["type"] = "string" },
        },
        ["required"] = new JsonArray { "team", "rowId", "status", "evidence" },
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

        if (!TaskToolText.TryGetString(arguments, "team", out string? teamText, out string teamRefusal))
        {
            return teamRefusal;
        }

        if (!TaskToolText.TryGetString(arguments, "rowId", out string? rowId, out string rowRefusal))
        {
            return rowRefusal;
        }

        if (!TaskToolText.TryGetString(arguments, "status", out string? statusText, out string statusRefusal))
        {
            return statusRefusal;
        }

        if (!TaskToolText.TryGetString(arguments, "evidence", out string? evidence, out string evidenceRefusal))
        {
            return evidenceRefusal;
        }

        if (string.IsNullOrWhiteSpace(teamText))
        {
            return "team is required.";
        }

        TeamSummary? team = teams.Find(teamText);
        if (team is null)
        {
            return $"There is no Team named {teamText}.";
        }

        string? refusal = RetroCaller.Check(personas, types, team, actor.Name, "Only the Team's manager or its Retro agent may mark its register rows.");
        if (refusal is not null)
        {
            return refusal;
        }

        if (!TryParseTarget(statusText, out RetroStatus status))
        {
            return "status must be Verified, Partial or Changed.";
        }

        if (string.IsNullOrWhiteSpace(evidence))
        {
            return "evidence is required.";
        }

        try
        {
            RegisterRow? row = register.ReadRegister(team.Name).FirstOrDefault(r => string.Equals(r.Id, rowId?.Trim(), StringComparison.Ordinal));
            if (row is null)
            {
                return $"The register has no row {rowId?.Trim()}.";
            }

            if (row.Status is not (RetroStatus.Done or RetroStatus.Partial))
            {
                return $"Row {row.Id} is not due: only a Done or Partial row can be checked.";
            }

            if (status == RetroStatus.Verified && !RetroVerifier.HasEvidenceLink(evidence))
            {
                return "Verified needs evidence from after the change.";
            }

            register.SetStatus(team.Name, row.Id, status, evidence.Trim());
            return $"Row {row.Id} is now {status}.";
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message;
        }
    }

    /// <summary>Reads the status word; only the three outcomes of a check are allowed.</summary>
    private static bool TryParseTarget(string? text, out RetroStatus status)
    {
        foreach (RetroStatus candidate in new[] { RetroStatus.Verified, RetroStatus.Partial, RetroStatus.Changed })
        {
            if (string.Equals(candidate.ToString(), text?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                status = candidate;
                return true;
            }
        }

        status = RetroStatus.Open;
        return false;
    }
}
