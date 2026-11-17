using System.Globalization;
using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Agents;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Jobs;
using Agency.Huddle.App.Profile;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Teams;
using Microsoft.Extensions.Options;

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
/// <param name="directory">Says whether the caller is a Member of a Follow-up's Room and names the Room.</param>
/// <param name="feed">Takes the Follow-up's Feed item.</param>
/// <param name="options">Holds the cap on Follow-ups per Agent.</param>
/// <param name="clock">The clock a relative Follow-up time is worked out from.</param>
/// <param name="personaName">The calling Persona's Name.</param>
internal sealed class JobsTool(
    JobStore store,
    JobScheduler scheduler,
    PersonaStore personas,
    ITeamCatalog teams,
    IHumanIdentity human,
    IPromptSource prompts,
    ITeamDirectory directory,
    IFeedPublisher feed,
    IOptions<TeamOptions> options,
    TimeProvider clock,
    string personaName) : IAppTool
{
    /// <summary>Said when a Persona other than the Chief of Staff acts on a Job that is not its own.</summary>
    internal const string Refusal = "You can schedule Jobs for yourself and for members of a Team you manage. Ask the Chief of Staff to schedule one for anyone else or for the Human.";

    private static readonly string[] Actions = ["create", "list", "update", "pause", "resume", "delete", "run_now", "follow_up"];

    private const string TooSoon = "A Follow-up must be at least 1 minute ahead.";
    private const string TooFar = "A Follow-up may be at most 31 days ahead.";
    private static readonly string ActionList =string.Join(", ", Actions);

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
            ["roomId"] = Text("For follow_up: the id of the Room you are in and will come back to."),
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

        if (string.Equals(action, "follow_up", StringComparison.Ordinal))
        {
            return await this.FollowUpAsync(arguments, cancellationToken).ConfigureAwait(false);
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

    private async Task<string> FollowUpAsync(JsonObject arguments, CancellationToken ct)
    {
        int cap = options.Value.Jobs.MaxFollowUpsPerAgent;
        if (cap <= 0)
        {
            return "Follow-ups are switched off here. Say in the Room what you will check and let the Human remind you.";
        }

        if (Read(arguments, "target") is not null)
        {
            return "A Follow-up is always for you; 'target' is not accepted.";
        }

        string? roomId = Read(arguments, "roomId");
        string? title = Read(arguments, "title");
        string? instruction = Read(arguments, "instruction");
        string? whenText = Read(arguments, "when");
        if (roomId is null || title is null || instruction is null || whenText is null)
        {
            return "A Follow-up needs a 'roomId', a 'title', an 'instruction' and a 'when'.";
        }

        Room? room = await directory.GetRoomAsync(roomId, ct).ConfigureAwait(false);
        IReadOnlyList<User> members = room is null ? [] : await directory.GetRoomMembersAsync(roomId, ct).ConfigureAwait(false);
        if (room is null || !members.Any(member => string.Equals(member.Name, personaName, StringComparison.OrdinalIgnoreCase)))
        {
            return $"You are not a Member of Room '{roomId}'. A Follow-up can only come back to a Room you are in.";
        }

        TimeZoneInfo zone = scheduler.ZoneOf(Job.FollowUp(string.Empty, personaName, roomId, title, instruction, DateOnly.MinValue, TimeOnly.MinValue));
        if (!this.TryResolveDue(whenText, zone, out DateTimeOffset due, out string? refusal))
        {
            return refusal;
        }

        if (store.Jobs.Count(job => job.RoomId is not null && job.Owner.IsAgent(personaName)) >= cap)
        {
            return string.Create(CultureInfo.InvariantCulture, $"You already have {cap} Follow-ups waiting, the most one Agent may have. Delete one with jobs delete, or say in the Room what you will check and let the Human remind you.");
        }

        DateTime local = TimeZoneInfo.ConvertTime(due, zone).DateTime;
        Job job = store.Add(Job.FollowUp(string.Empty, personaName, roomId, title, instruction, DateOnly.FromDateTime(local), TimeOnly.FromDateTime(local)));
        scheduler.Seed(job);
        string when = local.ToString("ddd d MMM yyyy 'at' HH:mm", CultureInfo.InvariantCulture);
        feed.Post(new FeedMessage(FeedSourceType.JobFollowUpSet, job.Id, new FeedActor(FeedActorKind.Agent, personaName), false, new JobFollowUpSetPayload(job.Id, personaName, title, roomId, room.Name, when)));
        return $"Set {job.Id} \"{title}\": you will come back to {room.Name} on {when}.";
    }

    /// <summary>Works out when a Follow-up is due: a relative time is UTC arithmetic from the clock, rounded up to the next whole minute; an absolute one is Jobs' own grammar.</summary>
    private bool TryResolveDue(string whenText, TimeZoneInfo zone, out DateTimeOffset due, [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out string? refusal)
    {
        DateTimeOffset now = clock.GetUtcNow();
        due = now;
        refusal = null;
        string[] parts = whenText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 3 && string.Equals(parts[0], "in", StringComparison.OrdinalIgnoreCase) && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int amount))
        {
            TimeSpan unit = parts[2].TrimEnd('s', 'S').ToUpperInvariant() switch
            {
                "MINUTE" => TimeSpan.FromMinutes(1),
                "HOUR" => TimeSpan.FromHours(1),
                "DAY" => TimeSpan.FromDays(1),
                _ => TimeSpan.Zero,
            };
            if (unit != TimeSpan.Zero)
            {
                if (amount > 100_000)
                {
                    refusal = TooFar;
                    return false;
                }

                long ticks = (now + (unit * amount)).Ticks;
                due = new DateTimeOffset(ticks - (ticks % TimeSpan.TicksPerMinute) + (ticks % TimeSpan.TicksPerMinute > 0 ? TimeSpan.TicksPerMinute : 0), TimeSpan.Zero);
                return CheckRange(now, due, out refusal);
            }
        }

        if (!JobRecurrence.TryParse(whenText, out JobRecurrence? when, out string? error))
        {
            refusal = error;
            return false;
        }

        if (when.Cadence != JobCadence.Once)
        {
            refusal = JobRules.FollowUpRunsOnce;
            return false;
        }

        return CheckOnce(when, now, zone, out due, out refusal);
    }

    /// <summary>The one bound a Follow-up's one-off timetable obeys, for <c>follow_up</c> and <c>update</c> alike: at least 1 minute and at most 31 days ahead.</summary>
    private static bool CheckOnce(JobRecurrence when, DateTimeOffset now, TimeZoneInfo zone, out DateTimeOffset due, [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out string? refusal)
    {
        due = now;
        if (when.NextAfter(now, zone) is not { } next)
        {
            refusal = TooSoon;
            return false;
        }

        due = next;
        return CheckRange(now, due, out refusal);
    }

    private static bool CheckRange(DateTimeOffset now, DateTimeOffset due, [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out string? refusal)
    {
        TimeSpan ahead = due - now;
        if (ahead < TimeSpan.FromMinutes(1))
        {
            refusal = TooSoon;
        }
        else
        {
            refusal = ahead > TimeSpan.FromDays(31) ? TooFar : null;
        }

        return refusal is null;
    }

    private string Update(Job job, JsonObject arguments)
    {
        JobOwner owner = job.Owner;
        if (job.RoomId is not null && Read(arguments, "target") is not null)
        {
            return "A Follow-up cannot move to another owner.";
        }

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

        if (job.RoomId is not null && when.Cadence == JobCadence.Once && !ReferenceEquals(when, job.When)
            && !CheckOnce(when, clock.GetUtcNow(), scheduler.ZoneOf(job with { TimeZoneId = zone }), out _, out string? rangeRefusal))
        {
            return rangeRefusal;
        }

        Job changed = job with
        {
            Owner = owner,
            Title = Read(arguments, "title") ?? job.Title,
            Instruction = Read(arguments, "instruction") ?? job.Instruction,
            When = when,
            TimeZoneId = zone,
        };
        if (JobRules.Check(changed) is { } ruleRefusal)
        {
            return ruleRefusal;
        }

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
