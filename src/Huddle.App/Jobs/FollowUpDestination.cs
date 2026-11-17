using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;

namespace Agency.Huddle.App.Jobs;

/// <summary>
/// Where a Follow-up posts: the Room it names, left archived if it is, with no <c>room_jobs</c> row (Turn Queue spec §7.2 and §7.4).
/// </summary>
/// <param name="directory">Finds the Room and its Members.</param>
/// <param name="chat">Reads the Room's Budget.</param>
/// <param name="personaExists">Says whether an Agent's Persona is still there: the Agent row outlives it.</param>
internal sealed class FollowUpDestination(ITeamDirectory directory, ChatService chat, PersonaExists personaExists) : IJobDestination
{
    /// <summary>The words after the mark that tell a Follow-up from a Job.</summary>
    public const string Label = "Follow-up: ";

    /// <inheritdoc />
    public bool Serves(Job job)
    {
        ArgumentNullException.ThrowIfNull(job);
        return job.RoomId is not null;
    }

    /// <inheritdoc />
    public async Task<JobTarget> ResolveAsync(Job job, User? agent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(job);

        // The Agent row and its Membership outlive a removed Persona, so the Persona is asked for as well.
        if (agent is null || !personaExists(agent.Name) || job.RoomId is not { } roomId)
        {
            return JobTarget.Stopped(JobOutcome.NoAgent);
        }

        if (await directory.GetRoomAsync(roomId, ct).ConfigureAwait(false) is not { } room)
        {
            return JobTarget.Stopped(JobOutcome.NoRoom);
        }

        IReadOnlyList<User> members = await directory.GetRoomMembersAsync(room.Id, ct).ConfigureAwait(false);
        if (!members.Any(member => string.Equals(member.Id, agent.Id, StringComparison.Ordinal)))
        {
            return JobTarget.Stopped(JobOutcome.NoAgent);
        }

        // An archived Room is posted into and left archived. A paused one is not: the Agent could not reply, so the Follow-up waits.
        return chat.GetBudget(room.Id).Exhausted ? JobTarget.Stopped(JobOutcome.Held) : JobTarget.In(room);
    }

    /// <inheritdoc />
    public string TextOf(Job job, User? agent, DateTimeOffset? lateDue)
    {
        ArgumentNullException.ThrowIfNull(job);
        return (agent is null ? string.Empty : "@" + agent.Name + " ") + JobRunner.Mark + " " + Label + job.Title + JobRunner.LateSuffix(lateDue) + "\n" + job.Instruction;
    }
}
