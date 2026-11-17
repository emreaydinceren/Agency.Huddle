using System.Globalization;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.NeedsYou;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Jobs;

/// <summary>
/// Runs one Job: finds its Job Room, or makes one, and posts its instruction there as Huddle (ADR-0065). For an Agent the post
/// Mentions it, so it takes a Turn; for the Human the Room goes on Needs You, which also raises a Feed Notification.
/// </summary>
/// <param name="directory">Finds and creates the Room and its Members.</param>
/// <param name="jobRooms">Remembers which Room is each Job's.</param>
/// <param name="chat">Posts as Huddle and reads the Room's Budget.</param>
/// <param name="events">Told when a Room appears or comes back from the archive.</param>
/// <param name="needsYou">Puts a Human's Job Room on Needs You.</param>
/// <param name="logger">Records each run.</param>
internal sealed partial class JobRunner(
    ITeamDirectory directory,
    JobRoomStore jobRooms,
    ChatService chat,
    RoomEvents events,
    NeedsYouTracker needsYou,
    ILogger<JobRunner> logger) : IJobRunner
{
    /// <summary>The mark before a Job's title, in its Room's name and in each post.</summary>
    public const string Mark = "⏰";

    /// <inheritdoc />
    public Task<JobOutcome> RunAsync(Job job, DateTimeOffset ranAt, CancellationToken ct) => this.RunAsync(job, ranAt, null, ct);

    /// <inheritdoc />
    public async Task<JobOutcome> RunAsync(Job job, DateTimeOffset ranAt, DateTimeOffset? lateDue, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(job);

        User? agent = null;
        if (job.Owner.AgentName is { } name)
        {
            agent = await this.FindAgentAsync(name, ct).ConfigureAwait(false);
            if (agent is null)
            {
                LogNoAgent(logger, job.Id, name);
                return JobOutcome.NoAgent;
            }
        }

        Room room = await this.EnsureRoomAsync(job, agent, ct).ConfigureAwait(false);
        if (agent is not null && chat.GetBudget(room.Id).Exhausted)
        {
            // A Huddle post is exempt from the Budget but the Agent's reply is not, so posting would wake nobody.
            LogBudgetSpent(logger, job.Id, room.Id);
            return JobOutcome.BudgetSpent;
        }

        _ = await chat.PostAsHuddleAsync(room.Id, TextOf(job, agent, lateDue), ct).ConfigureAwait(false);
        if (agent is null)
        {
            needsYou.MarkJob(room, ranAt);
        }

        LogRan(logger, job.Id, room.Id);
        return lateDue is null ? JobOutcome.Delivered : JobOutcome.DeliveredLate;
    }

    /// <summary>The Message a run posts: the Agent's Mention when it is for an Agent, the mark and title (with "ran late; due Mon 09:00" when late), then the instruction.</summary>
    /// <param name="job">The Job.</param>
    /// <param name="agent">The Agent it is for, or <see langword="null"/> for the Human.</param>
    /// <param name="lateDue">When it was due, already in the zone to show, or <see langword="null"/> for a run on time.</param>
    /// <returns>The text.</returns>
    internal static string TextOf(Job job, User? agent, DateTimeOffset? lateDue = null) =>
        (agent is null ? string.Empty : "@" + agent.Name + " ") + Mark + " " + job.Title
        + (lateDue is { } due ? " (ran late; due " + due.ToString("ddd HH:mm", CultureInfo.InvariantCulture) + ")" : string.Empty)
        + "\n\n" + job.Instruction;

    /// <summary>The name a new Job Room takes.</summary>
    /// <param name="job">The Job.</param>
    /// <returns>The name.</returns>
    internal static string RoomNameOf(Job job) => Mark + " " + job.Title;

    private async Task<User?> FindAgentAsync(string name, CancellationToken ct)
    {
        User? user = await directory.FindUserByNameAsync(name, ct).ConfigureAwait(false);
        return user is { Kind: UserKind.Agent } ? user : null;
    }

    /// <summary>The Job's Room: the one it had, brought back from the archive and given its Agent when needed, or a new one.</summary>
    private async Task<Room> EnsureRoomAsync(Job job, User? agent, CancellationToken ct)
    {
        if (jobRooms.RoomOf(job.Id) is { } roomId && await directory.GetRoomAsync(roomId, ct).ConfigureAwait(false) is { } existing)
        {
            bool changed = false;
            if (existing.Archived)
            {
                await directory.SetRoomArchivedAsync(existing.Id, false, ct).ConfigureAwait(false);
                changed = true;
            }

            if (agent is not null)
            {
                changed |= await directory.AddMemberAsync(existing.Id, agent.Id, ct).ConfigureAwait(false);
            }

            if (changed)
            {
                events.PublishRoomsChanged();
            }

            return existing;
        }

        // A Human's Job Room holds the Human alone: an Agent beside them in a two-Member Room would take a Turn on every reminder.
        List<string> members = agent is null ? [KnownIds.Human] : [KnownIds.Human, agent.Id];
        Room room = await directory.CreateRoomAsync(RoomNameOf(job), members, ct).ConfigureAwait(false);
        jobRooms.Set(job.Id, room.Id);
        events.PublishRoomsChanged();
        return room;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Job {JobId} ran in Room {RoomId}.")]
    private static partial void LogRan(ILogger logger, string jobId, string roomId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Job {JobId} did not run: no Agent called {AgentName} has come online yet.")]
    private static partial void LogNoAgent(ILogger logger, string jobId, string agentName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Job {JobId} did not post: the Budget of Room {RoomId} is spent.")]
    private static partial void LogBudgetSpent(ILogger logger, string jobId, string roomId);
}
