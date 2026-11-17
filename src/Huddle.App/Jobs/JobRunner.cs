using System.Globalization;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.NeedsYou;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Jobs;

/// <summary>
/// Runs one Job: finds the Agent, asks the first <see cref="IJobDestination"/> that serves the Job where to post (a Job Room of its own,
/// or the Room a Follow-up names) and posts its instruction there as Huddle (ADR-0065). For an Agent the post
/// Mentions it, so it takes a Turn; for the Human the Room goes on Needs You, which also raises a Feed Notification.
/// </summary>
/// <param name="directory">Finds the Agent, and builds the Job Room destination when none is given.</param>
/// <param name="jobRooms">Remembers which Room is each Job's.</param>
/// <param name="chat">Posts as Huddle and reads the Room's Budget.</param>
/// <param name="events">Told when a Room appears or comes back from the archive.</param>
/// <param name="needsYou">Puts a Human's Job Room on Needs You.</param>
/// <param name="logger">Records each run.</param>
/// <param name="destinations">Where Jobs post, in the order they are asked; defaults to the Job Room alone.</param>
internal sealed partial class JobRunner(
    ITeamDirectory directory,
    JobRoomStore jobRooms,
    ChatService chat,
    RoomEvents events,
    NeedsYouTracker needsYou,
    ILogger<JobRunner> logger,
    IEnumerable<IJobDestination>? destinations = null) : IJobRunner
{
    /// <summary>The mark before a Job's title, in its Room's name and in each post.</summary>
    public const string Mark = "⏰";

    private readonly IReadOnlyList<IJobDestination> served = destinations is null
        ? [new JobRoomDestination(directory, jobRooms, chat, events)]
        : [.. destinations];

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

        IJobDestination destination = this.served.FirstOrDefault(candidate => candidate.Serves(job))
            ?? throw new InvalidOperationException("No destination serves Job " + job.Id + ".");
        JobTarget target = await destination.ResolveAsync(job, agent, ct).ConfigureAwait(false);
        if (target.Room is not { } room)
        {
            LogStopped(logger, job.Id, target.Outcome);
            return target.Outcome;
        }

        _ = await chat.PostAsHuddleAsync(room.Id, destination.TextOf(job, agent, lateDue), ct).ConfigureAwait(false);
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
        (agent is null ? string.Empty : "@" + agent.Name + " ") + Mark + " " + job.Title + LateSuffix(lateDue)
        + "\n\n" + job.Instruction;

    /// <summary>The words that follow a late run's title: " (ran late; due Mon 09:00)", or nothing on time.</summary>
    /// <param name="lateDue">When it was due, already in the zone to show, or <see langword="null"/> for a run on time.</param>
    /// <returns>The suffix.</returns>
    internal static string LateSuffix(DateTimeOffset? lateDue) =>
        lateDue is { } due ? " (ran late; due " + due.ToString("ddd HH:mm", CultureInfo.InvariantCulture) + ")" : string.Empty;

    /// <summary>The name a new Job Room takes.</summary>
    /// <param name="job">The Job.</param>
    /// <returns>The name.</returns>
    internal static string RoomNameOf(Job job) => Mark + " " + job.Title;

    private async Task<User?> FindAgentAsync(string name, CancellationToken ct)
    {
        User? user = await directory.FindUserByNameAsync(name, ct).ConfigureAwait(false);
        return user is { Kind: UserKind.Agent } ? user : null;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Job {JobId} ran in Room {RoomId}.")]
    private static partial void LogRan(ILogger logger, string jobId, string roomId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Job {JobId} did not run: no Agent called {AgentName} has come online yet.")]
    private static partial void LogNoAgent(ILogger logger, string jobId, string agentName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Job {JobId} did not post: {Outcome}.")]
    private static partial void LogStopped(ILogger logger, string jobId, JobOutcome outcome);
}
