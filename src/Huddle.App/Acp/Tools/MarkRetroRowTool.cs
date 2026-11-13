using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Prompts;
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
    string callerAgentId,
    IPromptSource prompts,
    ITeamRenames? renames = null) : IAppTool
{
    /// <inheritdoc />
    public string Name => "mark_retro_row";

    /// <inheritdoc />
    public string Description => prompts.Render("tool.markRetroRow.description");

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

        (TaskActor? actor, string actorRefusal) = await TaskToolText.ResolveActorAsync(directory, callerAgentId, cancellationToken).ConfigureAwait(false);
        if (actor is null)
        {
            return actorRefusal;
        }

        if (!TryReadTexts(arguments, out RetroTexts? texts, out string textsRefusal))
        {
            return textsRefusal;
        }

        if (string.IsNullOrWhiteSpace(texts.Team))
        {
            return "team is required.";
        }

        TeamSummary? team = teams.Find(texts.Team);
        if (team is null)
        {
            return RenamedTeamRefusal.For(renames, texts.Team) ?? $"There is no Team named {texts.Team}.";
        }

        string? refusal = RetroCaller.Check(personas, types, team, actor.Name, "Only the Team's manager or its Retro agent may mark its register rows.");
        if (refusal is not null)
        {
            return refusal;
        }

        if (!TryParseTarget(texts.Status, out RetroStatus status))
        {
            return "status must be Verified, Partial or Changed.";
        }

        if (string.IsNullOrWhiteSpace(texts.Evidence))
        {
            return "evidence is required.";
        }

        return this.MarkRow(team.Name, texts.RowId, status, texts.Evidence);
    }

    /// <summary>Reads the four text arguments in order, each checked to be a string.</summary>
    /// <param name="arguments">The tool's JSON arguments.</param>
    /// <param name="texts">The argument texts when this returns <see langword="true"/>.</param>
    /// <param name="refusal">The refusal text when this returns <see langword="false"/>.</param>
    private static bool TryReadTexts(JsonObject arguments, [NotNullWhen(true)] out RetroTexts? texts, out string refusal)
    {
        texts = null;
        if (!TaskToolText.TryGetString(arguments, "team", out string? teamText, out refusal)
            || !TaskToolText.TryGetString(arguments, "rowId", out string? rowId, out refusal)
            || !TaskToolText.TryGetString(arguments, "status", out string? statusText, out refusal)
            || !TaskToolText.TryGetString(arguments, "evidence", out string? evidence, out refusal))
        {
            return false;
        }

        texts = new RetroTexts(teamText, rowId, statusText, evidence);
        refusal = string.Empty;
        return true;
    }

    /// <summary>Checks the register row is due and the evidence suits the target status, then writes the new status.</summary>
    /// <param name="teamName">The Team whose register is edited.</param>
    /// <param name="rowId">The row id the model sent.</param>
    /// <param name="status">The target status.</param>
    /// <param name="evidence">The non-blank evidence text.</param>
    private string MarkRow(string teamName, string? rowId, RetroStatus status, string evidence)
    {
        try
        {
            RegisterRow? row = register.ReadRegister(teamName).FirstOrDefault(r => string.Equals(r.Id, rowId?.Trim(), StringComparison.Ordinal));
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

            register.SetStatus(teamName, row.Id, status, evidence.Trim());
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
#pragma warning disable HAA0102 // Enum.ToString returns the runtime's cached name, so a lookup table would save nothing.
            if (string.Equals(candidate.ToString(), text?.Trim(), StringComparison.OrdinalIgnoreCase))
#pragma warning restore HAA0102
            {
                status = candidate;
                return true;
            }
        }

        status = RetroStatus.Open;
        return false;
    }

    /// <summary>The four text arguments, once each is known to be a string.</summary>
    /// <param name="Team">The Team name, if any.</param>
    /// <param name="RowId">The register row id, if any.</param>
    /// <param name="Status">The target status word, if any.</param>
    /// <param name="Evidence">The evidence text, if any.</param>
    private sealed record RetroTexts(string? Team, string? RowId, string? Status, string? Evidence);
}
