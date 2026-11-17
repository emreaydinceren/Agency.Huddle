using System.Globalization;
using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Agents;
using Agency.Huddle.App.Jobs;
using Agency.Huddle.App.Profile;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Acp.Tools;

/// <summary>
/// Schedules Jobs: instructions that run on a timetable in their own Job Room. Every Agent may schedule, change and remove its
/// own Jobs, a manager may also do so for the members of its Team, and only the Chief of Staff may do so for anyone else or for the Human. Offered to every Persona while Jobs are on.
/// </summary>
/// <param name="store">Holds the Jobs.</param>
/// <param name="scheduler">Works out next runs, runs a Job now and deletes one.</param>
/// <param name="personas">Resolves a target's Name or Alias and recognises the Chief of Staff.</param>
/// <param name="teams">Tells which Teams a manager manages and who belongs to them.</param>
/// <param name="human">The Human's current Name, accepted as a target.</param>
/// <param name="prompts">Supplies the description.</param>
/// <param name="personaName">The calling Persona's Name.</param>
internal sealed class JobsTool(
    JobStore store,
    JobScheduler scheduler,
    PersonaStore personas,
    ITeamCatalog teams,
    IHumanIdentity human,
    IPromptSource prompts,
    string personaName) : IAppTool
{
    /// <summary>Said when a Persona other than the Chief of Staff acts on a Job that is not its own.</summary>
    internal const string Refusal = "You can schedule Jobs for yourself and for members of a Team you manage. Ask the Chief of Staff to schedule one for anyone else or for the Human.";

    private static readonly string[] Actions = ["create", "list", "update", "pause", "resume", "delete", "run_now"];

    private static readonly string ActionList = string.Join(", ", Actions);

    /// <inheritdoc />
    public string Name => "jobs";

    /// <inheritdoc />
    public string Description => prompts.Render("tool.jobs.description");

    /// <inheritdoc />
    public JsonObject InputSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["action"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray([.. Actions.Select(action => JsonValue.Create(action))]) },
            ["id"] = Text("The Job's id, for every action but create and list."),
            ["target"] = Text("Who the Job is for: an Agent's Name or Alias, or the Human's Name. Defaults to you."),
            ["title"] = Text("A short title; it names the Job's Room."),
            ["instruction"] = Text("What to do when the Job runs; posted into its Room."),
            ["when"] = Text("One of " + JobRecurrence.Grammar),
            ["timezone"] = Text("An IANA time zone such as Europe/Berlin. Defaults to this machine's."),
        },
        ["required"] = new JsonArray("action"),
    };

    /// <inheritdoc />
    public async Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        string action = Read(arguments, "action") ?? string.Empty;
        if (string.Equals(action, "create", StringComparison.Ordinal))
        {
            return this.Create(arguments);
        }

        if (string.Equals(action, "list", StringComparison.Ordinal))
        {
            return this.List();
        }

        if (!Actions.Contains(action, StringComparer.Ordinal))
        {
            return $"'{action}' is not an action; use {ActionList}.";
        }

        string? id = Read(arguments, "id");
        if (id is null || store.Find(id) is not { } job)
        {
            return $"No Job has the id '{id}'. Call jobs with action 'list' to see yours.";
        }

        if (!this.MayManage(job.Owner))
        {
            return Refusal;
        }

        return action switch
        {
            "update" => this.Update(job, arguments),
            "pause" => this.SetPaused(job, true),
            "resume" => this.SetPaused(job, false),
            "delete" => scheduler.Delete(job.Id) ? $"Deleted {job.Id} \"{job.Title}\". Its Room stays, with its history." : $"{job.Id} was already gone.",
            _ => await this.RunNowAsync(job, cancellationToken).ConfigureAwait(false),
        };
    }

    private static JsonObject Text(string description) => new() { ["type"] = "string", ["description"] = description };

    private static string? Read(JsonObject arguments, string key) =>
        arguments[key] is JsonValue value && value.TryGetValue(out string? text) && text.Trim() is { Length: > 0 } trimmed ? trimmed : null;

    private string Create(JsonObject arguments)
    {
        if (!this.TryResolveTarget(Read(arguments, "target"), out JobOwner? owner, out string? refusal))
        {
            return refusal;
        }

        if (!this.MayManage(owner))
        {
            return Refusal;
        }

        string? title = Read(arguments, "title");
        string? instruction = Read(arguments, "instruction");
        if (title is null || instruction is null)
        {
            return "A new Job needs a 'title', an 'instruction' and a 'when'.";
        }

        if (!JobRecurrence.TryParse(Read(arguments, "when"), out JobRecurrence? when, out string? error))
        {
            return error;
        }

        string? zone = Read(arguments, "timezone");
        if (zone is not null && !JobTimeZones.TryFind(zone, out _))
        {
            return JobTimeZones.Unknown(zone);
        }

        Job job = store.Add(new Job(string.Empty, owner, title, instruction, when, zone, personaName, false));
        scheduler.Seed(job);
        return $"Scheduled {job.Id} \"{job.Title}\" for {this.NameOf(job.Owner)}: {job.When}. {this.NextRunText(job)}";
    }

    private string Update(Job job, JsonObject arguments)
    {
        JobOwner owner = job.Owner;
        if (Read(arguments, "target") is { } target)
        {
            if (!this.TryResolveTarget(target, out JobOwner? moved, out string? refusal))
            {
                return refusal;
            }

            if (!this.MayManage(moved))
            {
                return Refusal;
            }

            owner = moved;
        }

        JobRecurrence when = job.When;
        if (Read(arguments, "when") is { } whenText)
        {
            if (!JobRecurrence.TryParse(whenText, out JobRecurrence? parsed, out string? error))
            {
                return error;
            }

            when = parsed;
        }

        string? zone = Read(arguments, "timezone") ?? job.TimeZoneId;
        if (zone is not null && !JobTimeZones.TryFind(zone, out _))
        {
            return JobTimeZones.Unknown(zone);
        }

        Job changed = job with
        {
            Owner = owner,
            Title = Read(arguments, "title") ?? job.Title,
            Instruction = Read(arguments, "instruction") ?? job.Instruction,
            When = when,
            TimeZoneId = zone,
        };
        _ = store.Update(changed);
        return $"Updated {changed.Id} \"{changed.Title}\" for {this.NameOf(changed.Owner)}: {changed.When}. {this.NextRunText(changed)}";
    }

    private string SetPaused(Job job, bool paused)
    {
        Job changed = job with { Paused = paused };
        _ = store.Update(changed);
        return paused ? $"Paused {job.Id} \"{job.Title}\"; it will not run until it is resumed." : $"Resumed {job.Id} \"{job.Title}\". {this.NextRunText(changed)}";
    }

    private async Task<string> RunNowAsync(Job job, CancellationToken ct)
    {
        JobOutcome? outcome = await scheduler.RunNowAsync(job.Id, ct).ConfigureAwait(false);
        return outcome switch
        {
            JobOutcome.Delivered => $"Ran {job.Id} \"{job.Title}\": its instruction is posted in its Room.",
            JobOutcome.BudgetSpent => $"{job.Id} did not run: its Room's Budget is spent until the Human continues it.",
            JobOutcome.NoAgent => $"{job.Id} did not run: {this.NameOf(job.Owner)} is not available.",
            _ => $"{job.Id} did not run.",
        };
    }

    private string List()
    {
        bool all = this.IsChiefOfStaff();
        JsonArray rows = [];
        foreach (Job job in store.Jobs.Where(job => all || this.MayManage(job.Owner)))
        {
            rows.Add(new JsonObject
            {
                ["id"] = job.Id,
                ["title"] = job.Title,
                ["target"] = this.NameOf(job.Owner),
                ["when"] = job.When.ToString(),
                ["timezone"] = job.TimeZoneId ?? JobTimeZones.IdOf(scheduler.ZoneOf(job)),
                ["paused"] = job.Paused,
                ["nextRun"] = scheduler.NextRunOf(job)?.ToString("O", CultureInfo.InvariantCulture),
                ["by"] = job.CreatedBy,
                ["instruction"] = job.Instruction,
            });
        }

        return rows.ToJsonString();
    }

    private bool TryResolveTarget(string? target, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out JobOwner? owner, [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out string? refusal)
    {
        refusal = null;
        if (target is null)
        {
            owner = JobOwner.Agent(personaName);
            return true;
        }

        if (string.Equals(target.TrimStart('@'), human.Name, StringComparison.OrdinalIgnoreCase))
        {
            owner = JobOwner.Human;
            return true;
        }

        if (personas.ResolveByNameOrAlias(target.TrimStart('@')) is { } entry)
        {
            owner = JobOwner.Agent(entry.Name);
            return true;
        }

        owner = null;
        refusal = $"No Agent and not the Human is called '{target}'. Call list_agents to see the Agents.";
        return false;
    }

    private bool MayManage(JobOwner owner) => owner.IsAgent(personaName) || this.IsChiefOfStaff() || this.ManagesTeamOf(owner);

    private bool ManagesTeamOf(JobOwner owner) =>
        owner.AgentName is { } member
        && teams.Teams.Any(team =>
            string.Equals(team.Manager, personaName, StringComparison.OrdinalIgnoreCase)
            && team.Members.Contains(member, StringComparer.OrdinalIgnoreCase));

    private bool IsChiefOfStaff() => personas.Entries.Any(entry =>
        string.Equals(entry.Builtin, BuiltinAgent.ChiefOfStaffMarker, StringComparison.Ordinal)
        && string.Equals(entry.Name, personaName, StringComparison.OrdinalIgnoreCase));


    private string NameOf(JobOwner owner) => owner.AgentName ?? human.Name;

    private string NextRunText(Job job)
    {
        if (job.Paused)
        {
            return "It is paused.";
        }

        if (scheduler.NextRunOf(job) is not { } next)
        {
            return "Its time has passed, so it will not run.";
        }

        TimeZoneInfo zone = scheduler.ZoneOf(job);
        DateTimeOffset local = TimeZoneInfo.ConvertTime(next, zone);
        string zoneName = JobTimeZones.IdOf(zone);
        return string.Create(CultureInfo.InvariantCulture, $"It runs next on {local:ddd d MMM yyyy 'at' HH:mm} ({zoneName}).");
    }
}
