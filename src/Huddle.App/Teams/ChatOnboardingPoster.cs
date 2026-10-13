using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;

namespace Agency.Huddle.App.Teams;

/// <summary>Posts the onboarding prompt as the Human into the manager's direct Room.</summary>
/// <param name="chat">Finds or creates the Room and posts the Message.</param>
/// <param name="directory">Resolves the manager's user.</param>
internal sealed class ChatOnboardingPoster(ChatService chat, ITeamDirectory directory) : IOnboardingPoster
{
    /// <inheritdoc />
    public async Task<bool> PostAsync(string managerName, string text, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managerName);
        ArgumentNullException.ThrowIfNull(text);

        User? manager = await directory.FindUserByNameAsync(managerName, ct).ConfigureAwait(false);
        if (manager is null)
        {
            return false;
        }

        try
        {
            Room room = await chat.EnsureRoomForAsync(manager, ct).ConfigureAwait(false);
            _ = await chat.PostAsync(room.Id, KnownIds.Human, text, ct: ct).ConfigureAwait(false);
            return true;
        }
        catch (ChatException)
        {
            // A refused post (for example a spent Budget) leaves the marker unset so the next start tries again.
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> PostToTeamAsync(string team, string recipientName, string text, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(team);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientName);
        ArgumentNullException.ThrowIfNull(text);

        User? recipient = await directory.FindUserByNameAsync(recipientName, ct).ConfigureAwait(false);
        if (recipient is null)
        {
            return false;
        }

        Room? teamRoom = await directory.FindTeamRoomAsync(team, ct).ConfigureAwait(false);
        if (teamRoom is null || teamRoom.Archived)
        {
            return await this.PostAsync(recipientName, text, ct).ConfigureAwait(false);
        }

        IReadOnlyList<User> members = await directory.GetRoomMembersAsync(teamRoom.Id, ct).ConfigureAwait(false);
        if (!members.Any(member => string.Equals(member.Id, recipient.Id, StringComparison.Ordinal)))
        {
            return await this.PostAsync(recipientName, text, ct).ConfigureAwait(false);
        }

        try
        {
            // In a Room of three or more only a Mention wakes an Agent, so the text opens with one.
            _ = await chat.PostAsync(teamRoom.Id, KnownIds.Human, $"@{recipient.Name} {text}", ct: ct).ConfigureAwait(false);
            return true;
        }
        catch (ChatException)
        {
            // A refused post (for example a spent Budget) puts the cadence state back so the next tick tries again.
            return false;
        }
    }
}
