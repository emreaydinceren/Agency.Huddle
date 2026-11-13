using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Helpers;
using Agency.Huddle.App.Helpers.TeamMemory;
using Agency.Huddle.App.Memory;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Acp.Tools;

/// <summary>
/// The <c>propose_team_memory</c> App Tool (Team Memory Helper spec §6.1): a member proposes one fact for a Team's shared Memory,
/// and the Team Memory Helper decides whether and where to file it. The call checks membership and size, queues the proposal and
/// returns at once: it never waits for the Helper, starts no Turn and posts no Message. Refusals are plain text, as in every App Tool.
/// </summary>
/// <param name="personaName">The calling Persona's Name.</param>
/// <param name="access">Decides which Teams the caller may propose to: those it reads, every Team for the Chief of Staff.</param>
/// <param name="trigger">Queues the proposal.</param>
/// <param name="registry">Finds the Team Memory Helper, for its settings.</param>
/// <param name="settings">Tells whether the Helper is on.</param>
/// <param name="budget">Tells whether the Helper is paused for today.</param>
/// <param name="prompts">Renders the description and every reply.</param>
/// <param name="options">Supplies the size caps and whether Adapters are on.</param>
/// <param name="renames">Names today's Team when the call carries the old name of a renamed one; optional.</param>
internal sealed class ProposeTeamMemoryTool(
    string personaName,
    TeamMemoryAccess access,
    TeamMemoryTrigger trigger,
    HelperRegistry registry,
    HelperSettingsResolver settings,
    HelperBudget budget,
    IPromptSource prompts,
    IOptions<TeamOptions> options,
    ITeamRenames? renames = null) : IAppTool
{
    /// <summary>The tool's name, without any Adapter prefix.</summary>
    public const string ToolName = "propose_team_memory";

    /// <inheritdoc />
    public string Name => ToolName;

    /// <inheritdoc />
    public string Description => prompts.Render("tool.proposeTeamMemory.description");

    /// <inheritdoc />
    public JsonObject InputSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["team"] = new JsonObject { ["type"] = "string" },
            ["fact"] = new JsonObject { ["type"] = "string" },
            ["why"] = new JsonObject { ["type"] = "string" },
        },
        ["required"] = new JsonArray("team", "fact"),
    };

    /// <summary>Whether <paramref name="name"/> is this tool as offered: the bare name, or the name behind an Adapter's <c>mcp__{server}__</c> prefix.</summary>
    /// <param name="name">A tool name as offered or as an Adapter reported it.</param>
    /// <returns><see langword="true"/> for <c>propose_team_memory</c> and for any name ending <c>__propose_team_memory</c>.</returns>
    public static bool IsToolName(string name) => AppToolNames.Matches(name, ToolName);

    /// <inheritdoc />
    public Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return Task.FromResult(this.Propose(arguments));
    }

    /// <summary>Checks the call, then queues the proposal.</summary>
    /// <param name="arguments">The tool arguments.</param>
    private string Propose(JsonObject arguments)
    {
        string teamArgument = Text(arguments, "team");
        TeamSummary? team = teamArgument.Length == 0 ? null : access.Find(teamArgument);
        if (team is null && RenamedTeamRefusal.For(renames, teamArgument) is { } renamedRefusal)
        {
            return renamedRefusal;
        }

        if (team is null || !team.HasFolder || !access.CanRead(personaName, team))
        {
            string teams = string.Join(", ", access.Readable(personaName).Select(static readable => readable.Name));
            string offered = teams.Length > 0 ? teams : "none";
            return teamArgument.Length == 0
                ? this.Say("tool.proposeTeamMemory.noTeam", ("teams", offered))
                : this.Say("tool.proposeTeamMemory.notMember", ("team", teamArgument), ("teams", offered));
        }

        string fact = Text(arguments, "fact");
        string why = Text(arguments, "why");
        if (fact.Length == 0)
        {
            return this.Say("tool.proposeTeamMemory.noFact");
        }

        MemoryOptions caps = options.Value.Memory;
        if ((TooLong("fact", fact, caps.MaxProposalChars) ?? TooLong("why", why, caps.MaxProposalWhyChars)) is { } tooLong)
        {
            return this.Say("tool.proposeTeamMemory.tooLong", ("field", tooLong.Field), ("length", tooLong.Length), ("max", tooLong.Max));
        }

        if (this.Unavailable() is { } unavailable)
        {
            return unavailable;
        }

        trigger.Propose(new TeamMemoryProposal(Guid.NewGuid(), team.Name, fact, why.Length == 0 ? null : why, personaName));
        return this.Say("tool.proposeTeamMemory.proposed", ("team", team.Name));
    }

    /// <summary>The refusal when the Helper cannot take a proposal now, else <see langword="null"/>.</summary>
    private string? Unavailable()
    {
        IHelperAgent? helper = registry.Find(TeamMemoryHelper.HelperId);
        HelperSettings? effective = helper is null ? null : settings.For(helper);
        if (effective is not { Enabled: true } || !options.Value.Acp.Enabled)
        {
            return this.Say("tool.proposeTeamMemory.off");
        }

        return budget.IsPaused(TeamMemoryHelper.HelperId, effective.DailyCap) ? this.Say("tool.proposeTeamMemory.paused") : null;
    }

    /// <summary>The over-cap details for <paramref name="value"/>, or <see langword="null"/> when it fits or there is no cap.</summary>
    /// <param name="field">The argument's name.</param>
    /// <param name="value">The trimmed value.</param>
    /// <param name="max">The cap; zero or less means none.</param>
    private static (string Field, string Length, string Max)? TooLong(string field, string value, int max) =>
        max > 0 && value.Length > max
            ? (field, value.Length.ToString(CultureInfo.InvariantCulture), max.ToString(CultureInfo.InvariantCulture))
            : null;

    /// <summary>A string argument, trimmed; empty when it is missing or not a string.</summary>
    /// <param name="arguments">The tool arguments.</param>
    /// <param name="name">The argument's name.</param>
    private static string Text(JsonObject arguments, string name) =>
        arguments[name] is JsonValue value && value.TryGetValue(out string? text) ? text.Trim() : string.Empty;

    /// <summary>Renders a Prompt, naming each placeholder by its bare name.</summary>
    /// <param name="key">The Prompt's key.</param>
    /// <param name="values">The placeholder names (without braces) and their values.</param>
    private string Say(string key, params (string Name, string Value)[] values) => prompts.Say(key, values);
}
