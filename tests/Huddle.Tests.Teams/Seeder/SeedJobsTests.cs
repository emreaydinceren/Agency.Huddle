using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Jobs;
using Agency.Huddle.Contracts;
using Agency.Huddle.Seeder.Model;
using Agency.Huddle.Seeder.Writing;
using Agency.Huddle.Tests.Factories;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Seeder;

/// <summary>
/// Seeds the default scenario and reads its Jobs back with the app's own <see cref="JobStore"/>, <see cref="JobStateStore"/> and
/// <see cref="JobRoomStore"/>, so a change to how Huddle keeps Jobs cannot leave the seed behind. Seeder spec §7 (SEED-RT-8).
/// </summary>
public sealed class SeedJobsTests
{
    /// <summary>SEED-RT-8: Every seeded Job loads through the app's <see cref="JobStore"/> as planned, and the unreadable sections are listed as problems.</summary>
    [Fact]
    public async Task Jobs_LoadThroughTheAppsJobStore()
    {
        using SeededFolder seed = await SeededFolder.CreateAsync(TestContext.Current.CancellationToken);
        SeedPlan plan = seed.Summary.Plan;
        using PersonaStore personas = seed.Options().CreatePersonaStore();
        using JobStore store = new(personas, seed.Options(), NullLogger<JobStore>.Instance);

        Assert.Equal(plan.Jobs.Select(JobSeeder.ToJob).OrderBy(j => j.Id, StringComparer.Ordinal), store.Jobs.OrderBy(j => j.Id, StringComparer.Ordinal));
        Assert.Equal(["Fortnightly SLA report", "Pantry stock-take"], store.Problems.Select(p => p.Title).Order(StringComparer.Ordinal));
        Assert.True(File.Exists(Path.Combine(seed.DataDir, JobFiles.HumanFolder, JobFiles.FileName)));
    }

    /// <summary>
    /// SEED-RT-8: Each Job's state carries the scheduler's own timetable key, the next run worked out from the seed time, and,
    /// for a Job that has run, its last run and outcome; a Job that never ran has neither.
    /// </summary>
    [Fact]
    public async Task JobState_IsWhatTheSchedulerWouldHaveStored()
    {
        using SeededFolder seed = await SeededFolder.CreateAsync(TestContext.Current.CancellationToken);
        SeedPlan plan = seed.Summary.Plan;
        JobStateStore states = new(seed.Options());
        DateTimeOffset seedTime = new(SeededFolder.Today.ToDateTime(new TimeOnly(15, 0)), TimeSpan.Zero);

        Assert.All(plan.Jobs, seeded =>
        {
            Job job = JobSeeder.ToJob(seeded);
            JobState? state = states.Get(job.Id);
            TimeZoneInfo zone = JobTimeZones.Resolve(job.TimeZoneId, TimeZoneInfo.Local);
            JobOutcome? outcome = seeded.Runs.Count == 0 ? null : seeded.LastOutcome;

            Assert.Equal(new JobState(job.Id, JobScheduler.KeyOf(job), job.When.NextAfter(seedTime, zone), JobSeeder.LastRunOf(plan, seeded), outcome), state);
        });
        Assert.Contains(plan.Jobs, j => states.Get(j.Id)?.LastOutcome == JobOutcome.BudgetSpent);
    }

    /// <summary>
    /// SEED-RT-8: A Job that has run has its own Room, named and peopled as <c>JobRunner</c> makes one, holding Huddle's posts in
    /// the runner's words and then the replies; it is never returned as the Agent's one-to-one.
    /// </summary>
    [Fact]
    public async Task JobRooms_HoldHuddlesPostsAndAreNotOneToOnes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using SeededFolder seed = await SeededFolder.CreateAsync(ct);
        SeedPlan plan = seed.Summary.Plan;
        SqliteTeamDirectory directory = new(seed.Options());
        FileChatStore chat = new(seed.Options(), NullLogger<FileChatStore>.Instance);
        JobRoomStore jobRooms = new(seed.Options());
        SeedJob[] ran = [.. plan.Jobs.Where(j => j.Runs.Count > 0)];

        Assert.Equal(ran.Select(j => j.Id).Order(StringComparer.Ordinal), seed.Summary.Ids.JobRoomIds.Keys.Order(StringComparer.Ordinal));
        foreach (SeedJob seeded in ran)
        {
            Job job = JobSeeder.ToJob(seeded);
            string roomId = seed.Summary.Ids.JobRoomIds[job.Id];
            Room? room = await directory.GetRoomAsync(roomId, ct);
            IReadOnlyList<User> members = await directory.GetRoomMembersAsync(roomId, ct);
            IReadOnlyList<ChatMessage> messages = await chat.ReadAllAsync(roomId, ct);
            User? agent = seeded.Owner is { } name ? new User(seed.Summary.Ids.UserIds[name], name, UserKind.Agent, null) : null;
            int posts = seeded.LastOutcome == JobOutcome.Delivered ? seeded.Runs.Count : seeded.Runs.Count - 1;
            string[] expectedMembers = agent is null ? [KnownIds.Human] : [KnownIds.Human, agent.Id];

            Assert.Equal(roomId, jobRooms.RoomOf(job.Id));
            Assert.Equal(JobRunner.RoomNameOf(job), room?.Name);
            Assert.Equal(seeded.RoomArchived, room?.Archived);
            Assert.Equal(expectedMembers.Order(StringComparer.Ordinal), members.Select(m => m.Id).Order(StringComparer.Ordinal));
            Assert.Equal(posts + seeded.Runs.Sum(r => r.Replies.Count), messages.Count);
            Assert.Equal(posts, messages.Count(m => m.SenderId == KnownIds.Huddle));
            Assert.All(messages.Where(m => m.SenderId == KnownIds.Huddle), m => Assert.Equal(JobRunner.TextOf(job, agent), m.Text));
            Assert.Equal(messages.OrderBy(m => m.Timestamp).Select(m => m.Id), messages.Select(m => m.Id));
            if (agent is not null)
            {
                Room? oneToOne = await directory.FindRoomWithExactMembersAsync(KnownIds.Human, agent.Id, ct);
                Assert.NotEqual(roomId, oneToOne?.Id);
            }
        }
    }
}
