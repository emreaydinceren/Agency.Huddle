using Agency.Huddle.App;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Jobs;
using Agency.Huddle.Contracts;
using Agency.Huddle.Seeder.Model;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Seeder.Writing;

/// <summary>
/// Seeds Jobs as Huddle keeps them: each owner's <c>schedule.md</c> through the app's <see cref="JobDocument"/>, the firing
/// state through <see cref="JobStateStore"/>, and each Job that has run gets its Job Room through <see cref="JobRoomStore"/>,
/// holding Huddle's posts in <see cref="JobRunner.TextOf"/>'s words. Seeder spec §5, steps 3 and 6; SEED-SC-19.
/// </summary>
internal static class JobSeeder
{
    /// <summary>The app's Job for a seeded one.</summary>
    /// <param name="seed">The seeded Job.</param>
    /// <returns>The Job.</returns>
    /// <exception cref="InvalidOperationException">Its <c>when</c> is not a timetable the app reads.</exception>
    internal static Job ToJob(SeedJob seed)
    {
        if (!JobRecurrence.TryParse(seed.When, out JobRecurrence? when, out string? error))
        {
            throw new InvalidOperationException($"Job {seed.Id}: {error}");
        }

        JobOwner owner = seed.Owner is { } agent ? JobOwner.Agent(agent) : JobOwner.Human;
        return new Job(seed.Id, owner, seed.Title, seed.Instruction, when, seed.TimeZone, seed.By, seed.Paused);
    }

    /// <summary>Where an owner's <c>schedule.md</c> goes, relative to the data folder.</summary>
    /// <param name="owner">The Agent's Name, or <see langword="null"/> for the Human.</param>
    /// <returns>A <c>/</c>-separated path.</returns>
    internal static string RelativePath(string? owner) =>
        owner is null ? $"{JobFiles.HumanFolder}/{JobFiles.FileName}" : $"Agents/{owner}/{JobFiles.FileName}";

    /// <summary>When a seeded Job last ran: its last run's time, or <see langword="null"/> when it never has.</summary>
    /// <param name="plan">The plan, for its run date.</param>
    /// <param name="seed">The seeded Job.</param>
    /// <returns>The instant.</returns>
    internal static DateTimeOffset? LastRunOf(SeedPlan plan, SeedJob seed) =>
        seed.Runs.Count == 0 ? null : DatabaseSeeder.Timestamp(plan.Today, seed.Runs[^1].DaysAgo, seed.Runs[^1].Time);

    /// <summary>Writes one <c>schedule.md</c> per owner: its Jobs in plan order, then the sections it cannot read.</summary>
    /// <param name="plan">What to write.</param>
    /// <param name="dataDir">The data folder.</param>
    internal static void WriteFiles(SeedPlan plan, string dataDir)
    {
        IEnumerable<string?> owners = plan.Jobs.Select(static j => j.Owner).Concat(plan.JobProblems.Select(static p => p.Owner));
        foreach (string? owner in owners.Distinct(StringComparer.Ordinal))
        {
            SeedIo.WriteText(SeedIo.Resolve(dataDir, RelativePath(owner)), Render(plan, owner));
        }
    }

    /// <summary>The text of one owner's <c>schedule.md</c>.</summary>
    /// <param name="plan">The plan.</param>
    /// <param name="owner">The Agent's Name, or <see langword="null"/> for the Human.</param>
    /// <returns>The file's text.</returns>
    internal static string Render(SeedPlan plan, string? owner)
    {
        JobOwner jobOwner = owner is null ? JobOwner.Human : JobOwner.Agent(owner);
        List<JobSection> sections = [];
        foreach (SeedJob seed in plan.Jobs.Where(j => string.Equals(j.Owner, owner, StringComparison.Ordinal)))
        {
            Job job = ToJob(seed);
            sections.Add(new JobSection(job, JobDocument.Format(job), job.Title, null));
        }

        foreach (SeedJobProblem problem in plan.JobProblems.Where(p => string.Equals(p.Owner, owner, StringComparison.Ordinal)))
        {
            sections.AddRange(JobDocument.Parse(problem.Raw, jobOwner));
        }

        return JobDocument.Write(sections);
    }

    /// <summary>Stores every Job's state, and gives each Job that has run its Job Room and transcript.</summary>
    /// <param name="plan">What to create.</param>
    /// <param name="dataDir">The data folder, which already holds <c>team.db</c> and every Agent user.</param>
    /// <param name="ids">The user ids the database assigned.</param>
    /// <param name="clock">The clock the next runs are worked out from, and the machine's time zone.</param>
    /// <param name="ct">Cancels the writes.</param>
    /// <returns>Job id to Job Room id.</returns>
    internal static async Task<IReadOnlyDictionary<string, string>> SeedAsync(SeedPlan plan, string dataDir, DatabaseResult ids, TimeProvider clock, CancellationToken ct)
    {
        Dictionary<string, string> rooms = new(StringComparer.Ordinal);
        if (plan.Jobs.Count == 0)
        {
            return rooms;
        }

        IOptions<TeamOptions> options = Options.Create(new TeamOptions { DataDir = dataDir, HumanName = plan.HumanName });
        SqliteTeamDirectory directory = new(options);
        FileChatStore chat = new(options, NullLogger<FileChatStore>.Instance);
        JobStateStore states = new(options);
        JobRoomStore jobRooms = new(options);
        DateTimeOffset seedTime = FeedSeeder.SeedTime(plan, clock);

        try
        {
            foreach (SeedJob seed in plan.Jobs)
            {
                Job job = ToJob(seed);
                if (seed.Runs.Count > 0)
                {
                    rooms[seed.Id] = await CreateRoomAsync(plan, seed, job, ids, directory, chat, jobRooms, ct).ConfigureAwait(false);
                }

                // Worked out exactly as the scheduler would; a run date in the past leaves it stale, and the scheduler then skips to the next one.
                TimeZoneInfo zone = JobTimeZones.Resolve(job.TimeZoneId, clock.LocalTimeZone);
                JobOutcome? outcome = seed.Runs.Count == 0 ? null : seed.LastOutcome;
                states.Set(new JobState(job.Id, JobScheduler.KeyOf(job), job.When.NextAfter(seedTime, zone), LastRunOf(plan, seed), outcome));
            }

            return rooms;
        }
        finally
        {
            SqliteConnection.ClearPool(new SqliteConnection($"Data Source={Path.Combine(dataDir, "team.db")}"));
        }
    }

    private static async Task<string> CreateRoomAsync(
        SeedPlan plan,
        SeedJob seed,
        Job job,
        DatabaseResult ids,
        SqliteTeamDirectory directory,
        FileChatStore chat,
        JobRoomStore jobRooms,
        CancellationToken ct)
    {
        // As JobRunner makes it: the Human's Job Room holds the Human alone, an Agent's holds the Human and the Agent.
        User? agent = seed.Owner is { } name ? new User(ids.UserIds[name], name, UserKind.Agent, null) : null;
        List<string> members = agent is null ? [KnownIds.Human] : [KnownIds.Human, agent.Id];
        Room room = await directory.CreateRoomAsync(JobRunner.RoomNameOf(job), members, ct).ConfigureAwait(false);
        jobRooms.Set(job.Id, room.Id);

        int index = 0;
        for (int run = 0; run < seed.Runs.Count; run++)
        {
            SeedJobRun past = seed.Runs[run];
            bool posted = run < seed.Runs.Count - 1 || seed.LastOutcome == JobOutcome.Delivered;
            if (posted)
            {
                ChatMessage post = new(
                    DatabaseSeeder.MessageId(seed.RoomKey, index++),
                    DatabaseSeeder.Timestamp(plan.Today, past.DaysAgo, past.Time),
                    KnownIds.Huddle,
                    KnownIds.HuddleName,
                    JobRunner.TextOf(job, agent));
                await chat.AppendAsync(room.Id, post, ct).ConfigureAwait(false);
            }

            foreach (SeedMessage reply in past.Replies)
            {
                bool human = string.Equals(reply.Sender, plan.HumanName, StringComparison.Ordinal);
                ChatMessage message = new(
                    DatabaseSeeder.MessageId(seed.RoomKey, index++),
                    DatabaseSeeder.Timestamp(plan.Today, reply.DaysAgo, reply.Time),
                    human ? KnownIds.Human : ids.UserIds[reply.Sender],
                    reply.Sender,
                    reply.Text);
                await chat.AppendAsync(room.Id, message, ct).ConfigureAwait(false);
            }
        }

        if (seed.RoomArchived)
        {
            await directory.SetRoomArchivedAsync(room.Id, archived: true, ct).ConfigureAwait(false);
        }

        return room.Id;
    }
}
