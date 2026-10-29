using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;

namespace Agency.Huddle.App.Profile;

/// <summary>
/// Moves everything keyed by the Human's Name when the Human renames themselves: the Avatar entry,
/// each Task's <c>creator:</c>/<c>assignee:</c>, each saved View's assignee filter, and the Human's
/// Team Directory row, then repaints every Room's member list. The Human's counterpart of
/// <see cref="Acp.PersonaRenameCascade"/>, without the parts only an Agent has (Work Dir, Room
/// Sessions, spend, commands, auto-named Rooms - a Room's derived name joins Agent names only).
/// </summary>
/// <remarks>
/// Transcripts are never rewritten: a Message keeps the <c>senderName</c> it was posted with, and the
/// UI shows the Human's current Name by matching <c>senderId</c> instead (<c>rules.md</c>). Each step
/// is caught on its own, so a failed file write never skips the directory rename.
/// </remarks>
/// <param name="store">The source of <see cref="HumanProfileStore.HumanRenamed"/>.</param>
/// <param name="avatars">Moves the Avatar entry to the new Name.</param>
/// <param name="tasks">Rewrites the Human's Name in Task files.</param>
/// <param name="views">Rewrites the Human's Name in saved View filters.</param>
/// <param name="directory">Renames the Human's row in place.</param>
/// <param name="roomEvents">Repaints member lists once the row is renamed.</param>
/// <param name="logger">Records a step that failed.</param>
internal sealed class HumanRenameCascade(
    HumanProfileStore store,
    AvatarStore avatars,
    TaskService tasks,
    ViewStore views,
    ITeamDirectory directory,
    RoomEvents roomEvents,
    ILogger<HumanRenameCascade> logger) : IHostedService, IDisposable
{
    private bool subscribed;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        store.HumanRenamed += this.OnHumanRenamed;
        this.subscribed = true;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        this.Unsubscribe();
        return Task.CompletedTask;
    }

    /// <summary>Unsubscribes from the store, which outlives this instance.</summary>
    public void Dispose()
    {
        this.Unsubscribe();
    }

    /// <summary>
    /// Runs the whole cascade from <paramref name="oldName"/> to <paramref name="newName"/>. Called for
    /// each <see cref="HumanProfileStore.HumanRenamed"/>, and at startup when the Human's row carries a
    /// different Name from the profile - a rename made while the app was stopped.
    /// </summary>
    /// <param name="oldName">The Name the Human had.</param>
    /// <param name="newName">The Name the Human has now.</param>
    public void Run(string oldName, string newName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(oldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);

        try
        {
            avatars.Rename(oldName, newName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not move the Human's Avatar from '{OldName}' to '{NewName}'.", oldName, newName);
        }

        // TaskService.RenameAgent never throws; only the View store's file write can.
        tasks.RenameAgent(oldName, newName);

        try
        {
            views.RenameAgent(oldName, newName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not rename the Human in saved Views from '{OldName}' to '{NewName}'.", oldName, newName);
        }

        if (!directory.RenameHuman(newName))
        {
            logger.LogWarning("The Team Directory refused to rename the Human from '{OldName}' to '{NewName}'; the Name may belong to an Agent.", oldName, newName);
        }

        roomEvents.PublishRoomsChanged();
    }

    /// <summary>Runs the cascade for one rename.</summary>
    /// <param name="renamed">The old and new Name.</param>
    private void OnHumanRenamed(HumanRenamed renamed) => this.Run(renamed.OldName, renamed.NewName);

    /// <summary>Unsubscribes once.</summary>
    private void Unsubscribe()
    {
        if (!this.subscribed)
        {
            return;
        }

        store.HumanRenamed -= this.OnHumanRenamed;
        this.subscribed = false;
    }
}
