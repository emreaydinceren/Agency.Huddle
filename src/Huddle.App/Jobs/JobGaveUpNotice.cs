using Agency.Huddle.App.Data;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Jobs;

/// <summary>
/// The trace of a one-off Job removed without posting: a <c>job-gave-up</c> Feed item (a Notification for the Human's Job, Activity for an
/// Agent's) and, for an Agent's Job, one Huddle line in its Job Room, or in its one-to-one when that Room is gone. The line Mentions nobody,
/// so it wakes no one and spends no Budget.
/// </summary>
/// <param name="feed">Takes the Feed item.</param>
/// <param name="jobRooms">Remembers which Room is each Job's.</param>
/// <param name="directory">Finds the Room, the Agent and the Human.</param>
/// <param name="chat">Posts the Huddle line.</param>
/// <param name="logger">Records a line that could not be posted.</param>
internal sealed class JobGaveUpNotice(
    IFeedPublisher feed,
    JobRoomStore jobRooms,
    ITeamDirectory directory,
    ChatService chat,
    ILogger<JobGaveUpNotice> logger) : IJobGaveUpNotice
{
    /// <inheritdoc />
    public async Task NoteAsync(Job job, string reason, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(reason);

        string? agentName = job.Owner.AgentName;
        string? roomId = jobRooms.RoomOf(job.Id);
        if (roomId is not null && await directory.GetRoomAsync(roomId, ct).ConfigureAwait(false) is null)
        {
            roomId = null;
        }

        FeedActor actor = agentName is null ? FeedActor.Huddle : new FeedActor(FeedActorKind.Agent, agentName);
        feed.Post(new FeedMessage(FeedSourceType.JobGaveUp, job.Id, actor, agentName is null, new JobGaveUpPayload(job.Id, job.Title, reason, roomId)));
        if (agentName is not null)
        {
            await this.PostLineAsync(agentName, roomId, job.Title, reason, ct).ConfigureAwait(false);
        }
    }

    /// <summary>The line the Agent reads on its next Turn there: its Job Room, else its one-to-one with the Human (none when the Agent is gone).</summary>
    private async Task PostLineAsync(string agentName, string? roomId, string title, string reason, CancellationToken ct)
    {
        string? target = roomId;
        if (target is not null)
        {
            IReadOnlyList<User> members = await directory.GetRoomMembersAsync(target, ct).ConfigureAwait(false);
            if (!members.Any(member => string.Equals(member.Name, agentName, StringComparison.OrdinalIgnoreCase)))
            {
                target = null;
            }
        }

        if (target is null)
        {
            User? agent = await directory.FindUserByNameAsync(agentName, ct).ConfigureAwait(false);
            User human = await directory.GetHumanAsync(ct).ConfigureAwait(false);
            target = agent is { Kind: UserKind.Agent }
                ? (await directory.FindRoomWithExactMembersAsync(human.Id, agent.Id, ct).ConfigureAwait(false))?.Id
                : null;
        }

        if (target is null)
        {
            return;
        }

        try
        {
            _ = await chat.PostAsHuddleAsync(target, LineOf(agentName, title, reason), ct).ConfigureAwait(false);
        }
        catch (ChatException ex)
        {
            logger.LogWarning(ex, "The line saying Job {Title} did not run could not be posted in Room {RoomId}.", title, target);
        }
    }

    /// <summary>The Huddle line: <c>⏰ Nova's Job "Digest" did not run: its Room is gone.</c></summary>
    /// <param name="agentName">The Agent's name.</param>
    /// <param name="title">The Job's title.</param>
    /// <param name="reason">Why it did not run.</param>
    /// <returns>The text.</returns>
    internal static string LineOf(string agentName, string title, string reason) =>
        JobRunner.Mark + " " + agentName + "'s Job \"" + title + "\" did not run: " + reason + ".";
}
