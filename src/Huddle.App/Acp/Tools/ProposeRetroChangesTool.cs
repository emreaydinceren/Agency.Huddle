using System.Globalization;
using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Acp.Tools;

/// <summary>
/// Lets a Team member who holds the <c>retrospective</c> Skill propose up to three changes to the Human (RT-5). A change names an
/// allowed file, the exact text to find and its replacement. The tool only stores the proposal; it has no write path to any file, and
/// Huddle makes the edit when the Human accepts. Offered only to a Persona holding a Skill that lists it.
/// </summary>
internal sealed class ProposeRetroChangesTool(
    ITeamCatalog teams,
    RetroProposalStore proposals,
    PersonaStore personas,
    TeamTypeCatalog types,
    ITeamDirectory directory,
    string callerAgentId,
    IPromptSource prompts) : IAppTool
{
    /// <summary>The most changes one proposal carries.</summary>
    private const int MaxChanges = 3;

    private static readonly IReadOnlyDictionary<string, string> NoValues = new Dictionary<string, string>();

    /// <inheritdoc />
    public string Name => "propose_retro_changes";

    /// <inheritdoc />
    public string Description => prompts.Render("tool.proposeRetroChanges.description", NoValues);

    /// <inheritdoc />
    public JsonObject InputSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["team"] = new JsonObject { ["type"] = "string" },
            ["proposals"] = new JsonObject
            {
                ["type"] = "array",
                ["maxItems"] = MaxChanges,
                ["items"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["title"] = new JsonObject { ["type"] = "string" },
                        ["targetFile"] = new JsonObject { ["type"] = "string" },
                        ["find"] = new JsonObject { ["type"] = "string" },
                        ["replace"] = new JsonObject { ["type"] = "string" },
                    },
                    ["required"] = new JsonArray { "title", "targetFile", "find", "replace" },
                },
            },
        },
        ["required"] = new JsonArray { "team", "proposals" },
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

        if (!TaskToolText.TryGetString(arguments, "team", out string? teamText, out string teamRefusal))
        {
            return teamRefusal;
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

        string? refusal = RetroCaller.Check(personas, types, team, actor.Name, "Only the Team's manager or its Retro agent may propose retro changes.");
        if (refusal is not null)
        {
            return refusal;
        }

        if (!TryParseChanges(arguments, team, out List<RetroChange> changes, out string changesRefusal))
        {
            return changesRefusal;
        }

        foreach (RetroProposalSet earlier in proposals.Pending(team.Name).Where(set => string.Equals(set.Proposer, actor.Name, StringComparison.OrdinalIgnoreCase)))
        {
            _ = proposals.Remove(earlier.Id);
        }

        RetroProposalSet proposal = new(Guid.NewGuid().ToString("N"), team.Name, actor.Name, changes);
        return proposals.Add(proposal)
            ? "Proposal sent to the Human."
            : "The proposal could not be saved; try again.";
    }

    /// <summary>Reads the <c>proposals</c> array into one to three changes, refusing a missing field or a file the Human may not be asked to edit.</summary>
    /// <param name="arguments">The tool's JSON arguments.</param>
    /// <param name="team">The Team the proposal is for.</param>
    /// <param name="changes">The parsed changes when this returns <see langword="true"/>.</param>
    /// <param name="refusal">The refusal text when this returns <see langword="false"/>.</param>
    private static bool TryParseChanges(JsonObject arguments, TeamSummary team, out List<RetroChange> changes, out string refusal)
    {
        changes = [];
        JsonArray items = arguments["proposals"] as JsonArray ?? [];
        if (items.Count == 0)
        {
            refusal = "Propose at least one change.";
            return false;
        }

        if (items.Count > MaxChanges)
        {
            refusal = "Propose at most 3 changes.";
            return false;
        }

        foreach (JsonNode? item in items)
        {
            string title = Text(item, "title").Trim();
            string file = Text(item, "targetFile");
            string find = Text(item, "find");
            if (title.Length == 0 || file.Length == 0 || find.Length == 0)
            {
                refusal = "Each proposal needs a title, a targetFile and a find text.";
                return false;
            }

            if (!IsAllowed(team, file))
            {
                refusal = "targetFile must be Process.md, _team.md or the file of a Team member (Name.md).";
                return false;
            }

            changes.Add(new RetroChange((changes.Count + 1).ToString(CultureInfo.InvariantCulture), title, file, find, Text(item, "replace")));
        }

        refusal = string.Empty;
        return true;
    }

    /// <summary>Whether <paramref name="file"/> is one of the three kinds the Human may be asked to edit. The file is matched by name only and never used as a path.</summary>
    private static bool IsAllowed(TeamSummary team, string file)
    {
        if (string.Equals(file, TeamPaths.ProcessFileName, StringComparison.Ordinal) || string.Equals(file, TeamPaths.TeamFileName, StringComparison.Ordinal))
        {
            return true;
        }

        const string Extension = ".md";
        return file.EndsWith(Extension, StringComparison.Ordinal)
            && file.Length > Extension.Length
            && team.Members.Contains(file[..^Extension.Length], StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Reads a string property, empty when it is missing or not a string.</summary>
    private static string Text(JsonNode? item, string property) =>
        item is JsonObject obj && obj[property] is JsonValue value && value.TryGetValue(out string? text) ? text : string.Empty;
}
