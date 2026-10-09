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
}
