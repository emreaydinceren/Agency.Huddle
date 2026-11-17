using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;

namespace Agency.Huddle.App.Jobs;

/// <summary>
/// Where an ordinary Job posts: its own Job Room, found in <c>room_jobs</c> and brought back from the archive, or made new (ADR-0065).
/// </summary>
/// <param name="directory">Finds and creates the Room and its Members.</param>
/// <param name="jobRooms">Remembers which Room is each Job's.</param>
/// <param name="chat">Reads the Room's Budget.</param>
/// <param name="events">Told when a Room appears or comes back from the archive.</param>
internal sealed class JobRoomDestination(ITeamDirectory directory, JobRoomStore jobRooms, ChatService chat, RoomEvents events) : IJobDestination
{
    /// <inheritdoc />
    public bool Serves(Job job)
    {
        ArgumentNullException.ThrowIfNull(job);
        return job.RoomId is null;
    }

    /// <inheritdoc />
    public async Task<JobTarget> ResolveAsync(Job job, User? agent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(job);

        Room room = await this.EnsureRoomAsync(job, agent, ct).ConfigureAwait(false);
        // A Huddle post is exempt from the Budget but the Agent's reply is not, so posting would wake nobody.
        return agent is not null && chat.GetBudget(room.Id).Exhausted ? JobTarget.Stopped(JobOutcome.BudgetSpent) : JobTarget.In(room);
    }

    /// <inheritdoc />
    public string TextOf(Job job, User? agent, DateTimeOffset? lateDue) => JobRunner.TextOf(job, agent, lateDue);

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
        Room room = await directory.CreateRoomAsync(JobRunner.RoomNameOf(job), members, ct).ConfigureAwait(false);
        jobRooms.Set(job.Id, room.Id);
        events.PublishRoomsChanged();
        return room;
    }
}
