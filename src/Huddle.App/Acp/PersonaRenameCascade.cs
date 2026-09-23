using Microsoft.Extensions.Options;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// Cascades a Persona rename into the Team Directory, and a Persona removal into its Avatar.
/// Subscribes to <see cref="PersonaStore.PersonaRenamed"/> and, for the Agent that Name used to
/// identify: renames its Team Directory row in place, re-derives any Room that was still carrying its
/// auto-name, and moves its Work Dir — see
/// <c>docs/adr/0011-a-rename-moves-the-teammate-not-its-history.md</c> for why this is a rename in
/// place rather than a stop-and-start under a new id. Also moves the renamed Persona's Avatar entry
/// and its <see cref="FileStateStore"/> file under the same subscription (FC §6.12), and subscribes
/// to <see cref="PersonaStore.PersonaRemoved"/> to delete a removed Persona's Avatar entry, image
/// file and <see cref="FileStateStore"/> file - see <see cref="OnPersonaRemoved"/>'s doc comment for
/// why a removal cascades here even though <c>docs/agencyteam/rules.md</c> row 38 says a removal does
/// not cascade to Agents, Rooms or Transcripts.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two halves, two urgencies.</b> Renaming the Team Directory row happens synchronously, inline
/// in <see cref="OnPersonaRenamed"/>, because it is a race against <see cref="PersonaStore"/> itself:
/// <see cref="PersonaStore.PersonaRenamed"/> is raised, to completion, strictly before
/// <see cref="PersonaStore.PersonasChanged"/>, and <c>PersonaSupervisor</c> reacts to the latter by
/// starting a runner under the new Name. If <see cref="ITeamDirectory.RenameUser"/> has not already
/// completed by then, the restarted runner's <c>hello</c> mints a brand-new user id instead of
/// reusing this one, and the ghost ADR-0011 exists to remove comes back. Everything after that point
/// — the Rooms, the Work Dir — races nothing: a Room name landing a moment later is simply repainted
/// by <see cref="RoomEvents.RoomsChanged"/>, so it runs detached.
/// </para>
/// <para>
/// <b>Works with <c>Acp:Enabled</c> false</b>, deliberately unlike <c>PersonaSupervisor</c>: the
/// Agent row this cascades from may exist from an earlier session, or from a raw pipe client, so a
/// rename still has to cascade with no agent process running at all. Registered as its own
/// <see cref="IHostedService"/> for exactly that reason, rather than folded into
/// <c>PersonaSupervisor</c>, whose <c>ExecuteAsync</c> returns immediately when Acp is off.
/// </para>
/// </remarks>
/// <param name="teamDirectory">Renames the Agent's row and re-derives its Rooms.</param>
/// <param name="personaStore">The source of <see cref="PersonaStore.PersonaRenamed"/> and <see cref="PersonaStore.PersonaRemoved"/>.</param>
/// <param name="roomEvents">Published once, after any Room this cascade renamed.</param>
/// <param name="avatars">Moves a renamed Persona's Avatar entry, and deletes a removed Persona's Avatar entry and image file.</param>
/// <param name="fileState">Moves a renamed Persona's <see cref="FileStateStore"/> file, and deletes a removed Persona's file — FC §6.12.</param>
/// <param name="roomSessions">Moves a renamed Persona's <see cref="RoomSessionStore"/> file, and deletes a removed Persona's file — RS §6.13.</param>
/// <param name="options">Supplies <see cref="TeamOptions.DataDir"/> and <see cref="AcpOptions.WorkDir"/>, which together locate the Work Dir to move.</param>
/// <param name="timeProvider">Drives the backoff between Work Dir move attempts, so a test can control it without a real delay.</param>
/// <param name="logger">Records a rejected rename, a Work Dir that could not be moved, a file state move that failed, a Room Session move that failed, and any failure in the detached half of the cascade.</param>
internal sealed partial class PersonaRenameCascade(
    ITeamDirectory teamDirectory,
    PersonaStore personaStore,
    RoomEvents roomEvents,
    AvatarStore avatars,
    FileStateStore fileState,
    RoomSessionStore roomSessions,
    IOptions<TeamOptions> options,
    TimeProvider timeProvider,
    ILogger<PersonaRenameCascade> logger) : IHostedService, IDisposable
{
    // The runner it raced against restarts within milliseconds and inherits the Work Dir as its cwd,
    // so a handful of short retries covers the ordinary case (the old process has not yet exited)
    // without leaving a hand-edited file locked out for long.
    private const int MaxWorkDirMoveAttempts = 5;
    private static readonly TimeSpan WorkDirMoveRetryDelay = TimeSpan.FromMilliseconds(250);

    private readonly TeamOptions teamOptions = options?.Value ?? throw new ArgumentNullException(nameof(options));
    private bool subscribed;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        personaStore.PersonaRenamed += this.OnPersonaRenamed;
        personaStore.PersonaRemoved += this.OnPersonaRemoved;
        this.subscribed = true;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        this.Unsubscribe();
        return Task.CompletedTask;
    }

    /// <summary>Unsubscribes from <see cref="PersonaStore.PersonaRenamed"/> and <see cref="PersonaStore.PersonaRemoved"/> — the store outlives this instance, so a leaked subscription must not.</summary>
    public void Dispose()
    {
        this.Unsubscribe();
    }

    private void Unsubscribe()
    {
        if (!this.subscribed)
        {
            return;
        }

        personaStore.PersonaRenamed -= this.OnPersonaRenamed;
        personaStore.PersonaRemoved -= this.OnPersonaRemoved;
        this.subscribed = false;
    }

    /// <summary>
    /// Renames the Agent's Team Directory row synchronously, then hands the rest of the cascade to a
    /// detached task. See the type's remarks for why the split falls exactly here.
    /// </summary>
    /// <param name="renamed">The old and the new Name of the Persona that changed.</param>
    private void OnPersonaRenamed(PersonaRenamed renamed)
    {
        // FIRST, and deliberately ABOVE the early return below: an Avatar exists whether or not an
        // Agent has ever connected, so gating it on the Team Directory row would silently lose the
        // avatar of a Teammate that has never run - which, with Acp:Enabled false by default, is
        // every Teammate in a stock installation.
        //
        // Synchronous, not detached: like the Team Directory row rename below, this races the
        // repaint PersonasChanged triggers - the Teammate card and /teammates both read AvatarStore
        // on render - and unlike a Room name, an Avatar has no later event that would correct a
        // stale read. Detaching it would let the OLD Name's avatar render, or none at all, for
        // however long the detached task takes to run.
        avatars.Rename(renamed.OldName, renamed.NewName);

        // Same placement and the same reason as the Avatar rename immediately above (FC §6.12): file
        // state exists whether or not an Agent has ever connected, so this must sit ABOVE the "no
        // Agent row" early return below, or a stock installation (Acp:Enabled false by default) would
        // never reach it. Caught rather than allowed to propagate: a failed file move must never skip
        // the race-critical ITeamDirectory.RenameUser call below it, which is what actually keeps
        // ADR-0011's guarantee (the same Team Directory row, under its new Name).
        try
        {
            fileState.Rename(renamed.OldName, renamed.NewName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogFileStateMoveFailed(logger, renamed.OldName, renamed.NewName, ex);
        }

        // Same placement and the same reason again (RS §6.13): a stored Room Session entry exists
        // whether or not an Agent has ever connected, so this must sit ABOVE the "no Agent row" early
        // return too, or a stock installation would never move it. Its own try/catch, separate from
        // the file state one above, so a failure moving one store never skips the other or the
        // race-critical Team Directory rename below.
        try
        {
            roomSessions.Rename(renamed.OldName, renamed.NewName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogRoomSessionsMoveFailed(logger, renamed.OldName, renamed.NewName, ex);
        }

        var user = teamDirectory.FindUserByName(renamed.OldName);
        if (user is null || user.Kind != UserKind.Agent)
        {
            // No Agent has ever registered under the old Name - the normal case when Acp:Enabled is
            // false, or when this Persona has simply never connected. There is nothing to cascade.
            return;
        }

        if (!teamDirectory.RenameUser(user.Id, renamed.NewName))
        {
            // The likeliest cause is that the new Name is already held by a different Agent. A
            // partial cascade (Rooms and Work Dir moved under a Name the users row never took) is
            // worse than none, so stop here.
            LogRenameRejected(logger, renamed.OldName, renamed.NewName);
            return;
        }

        _ = this.CascadeDetachedAsync(user.Id, renamed.OldName, renamed.NewName);
    }

    /// <summary>
    /// Deletes the removed Persona's Avatar entry and image file. <c>docs/agencyteam/rules.md</c> row
    /// 38 says a removal deliberately does NOT cascade to the Agent, its Rooms or its Transcripts -
    /// those are chat facts that outlive the Persona that created them - but that same row cascades
    /// the Model and the Effort, because both are part of the Persona itself: "leaving either row
    /// behind would silently resurrect an old setting if a Persona of the same name were created
    /// later." An Avatar is the same kind of thing, not a chat fact, so leaving it behind would
    /// silently resurrect an old face on a new Persona that happened to reuse the Name - this handler
    /// exists to prevent exactly that.
    /// </summary>
    /// <param name="removed">The Name of the Persona that no longer exists.</param>
    private void OnPersonaRemoved(PersonaRemoved removed)
    {
        avatars.Remove(removed.Name);

        try
        {
            fileState.Remove(removed.Name);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogFileStateRemoveFailed(logger, removed.Name, ex);
        }

        try
        {
            roomSessions.Remove(removed.Name);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogRoomSessionsRemoveFailed(logger, removed.Name, ex);
        }
    }

    /// <summary>
    /// The half of the cascade that races nothing: re-deriving auto-named Rooms and moving the Work
    /// Dir. Fire-and-forget from <see cref="OnPersonaRenamed"/>, so any failure has no caller left to
    /// observe it.
    /// </summary>
    private async Task CascadeDetachedAsync(string userId, string oldName, string newName)
    {
        try
        {
            await this.RenameAutoNamedRoomsAsync(userId, oldName).ConfigureAwait(false);
            await this.MoveWorkDirAsync(oldName, newName).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // This is a file-watcher-driven background path with nothing left to observe a thrown
            // exception: PersonaRenamed is raised from inside PersonaStore's debounce callback, and
            // an exception escaping this detached task would go to the process' unobserved-exception
            // handler, not back to any caller. Logging and swallowing is the least-bad outcome - a
            // partially-applied cascade here is recoverable (the Team Directory rename already
            // committed; a Room name or a Work Dir left behind is a cosmetic gap, not data loss) -
            // whereas the alternative, an unhandled exception on a background task, risks tearing
            // down the process for a cosmetic failure.
            LogCascadeFailed(logger, oldName, newName, ex);
        }
    }

    /// <summary>
    /// Re-derives the name of every Room the renamed Agent belongs to that was still carrying its
    /// auto-name, and leaves alone every Room the Human named by hand. Applies the same comparison
    /// <see cref="ChatService.InviteAsync"/> already makes, rather than inventing a second rule for
    /// when a Room may be renamed out from under somebody.
    /// </summary>
    private async Task RenameAutoNamedRoomsAsync(string userId, string oldName)
    {
        var rooms = await teamDirectory.GetRoomsForUserAsync(userId).ConfigureAwait(false);

        foreach (var room in rooms)
        {
            var members = await teamDirectory.GetRoomMembersAsync(room.Id).ConfigureAwait(false);

            // The Team Directory rename already committed, so `members` carries the NEW Name. To
            // decide whether the Room was still auto-named, reconstruct what RoomNaming.Derive would
            // have produced BEFORE the rename: the same Members, with the renamed Agent's Name
            // substituted back to what it used to be.
            var membersBeforeRename = members.Select(m => m.Id == userId ? m with { Name = oldName } : m);
            var derivationBeforeRename = RoomNaming.Derive(membersBeforeRename);

            if (!string.Equals(room.Name, derivationBeforeRename, StringComparison.Ordinal))
            {
                // The Human named this Room by hand; leave it alone.
                continue;
            }

            await teamDirectory.RenameRoomAsync(room.Id, RoomNaming.Derive(members)).ConfigureAwait(false);
        }

        // Published unconditionally, even when no Room was renamed. The Room view renders its Member
        // list by Name, and Chat.razor subscribes to RoomsChanged but NOT to PersonasChanged - so a
        // Teammate whose Rooms the Human had all named by hand would otherwise keep showing the old
        // Name in the header until some unrelated event happened to repaint it. The Agent's Name
        // changing is a fact about every Room it belongs to, whatever the Rooms are called.
        roomEvents.PublishRoomsChanged();
    }

    /// <summary>
    /// Moves the renamed Agent's Work Dir from <c>{oldName}</c> to <c>{newName}</c>. The Work Dir is
    /// the agent process' <c>cwd</c>, not a jail, and the Adapter auto-loads <c>CLAUDE.md</c> and
    /// <c>.claude/settings.json</c> from it - leaving it behind would silently discard whatever the
    /// Teammate had written for itself.
    /// </summary>
    private async Task MoveWorkDirAsync(string oldName, string newName)
    {
        var workDirRoot = Path.Combine(this.teamOptions.DataDir, this.teamOptions.Acp.WorkDir);
        var source = Path.Combine(workDirRoot, oldName);
        var target = Path.Combine(workDirRoot, newName);

        if (!Directory.Exists(source))
        {
            // Normal: the Work Dir is created on first start, and this Persona may never have run.
            return;
        }

        if (Directory.Exists(target))
        {
            // Never merge - the target may belong to an unrelated Persona of that Name already.
            LogWorkDirTargetExists(logger, oldName, newName);
            return;
        }

        for (var attempt = 1; attempt <= MaxWorkDirMoveAttempts; attempt++)
        {
            try
            {
                Directory.Move(source, target);
                return;
            }
            catch (IOException) when (attempt < MaxWorkDirMoveAttempts)
            {
                // The old agent process may still hold this directory as its cwd - PersonaSupervisor
                // stops it asynchronously, after PersonasChanged, so the stop can still be in flight
                // here. Back off and try again rather than giving up on the first collision.
                await Task.Delay(WorkDirMoveRetryDelay, timeProvider, CancellationToken.None).ConfigureAwait(false);
            }
            catch (IOException ex)
            {
                LogWorkDirMoveFailed(logger, oldName, newName, ex);
                return;
            }
        }
    }

    /// <summary>Logs that a Team Directory rename was rejected, most likely because the new Name is already held by another Agent.</summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not rename Agent '{OldName}' to '{NewName}' in the Team Directory - the new Name may already be held by another Agent. The cascade stopped here; no Room or Work Dir was touched.")]
    private static partial void LogRenameRejected(ILogger logger, string oldName, string newName);

    /// <summary>Logs that the detached half of a rename cascade (Rooms, Work Dir) failed.</summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "The rename cascade for '{OldName}' -> '{NewName}' failed after the Team Directory rename had already committed.")]
    private static partial void LogCascadeFailed(ILogger logger, string oldName, string newName, Exception exception);

    /// <summary>Logs that a Work Dir move was skipped because the target directory already exists.</summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Did not move the Work Dir for '{OldName}' to '{NewName}': a directory already exists at the target. Both were left in place.")]
    private static partial void LogWorkDirTargetExists(ILogger logger, string oldName, string newName);

    /// <summary>Logs that a Work Dir move failed after every retry.</summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not move the Work Dir for '{OldName}' to '{NewName}' after several attempts.")]
    private static partial void LogWorkDirMoveFailed(ILogger logger, string oldName, string newName, Exception exception);

    /// <summary>Logs that moving a renamed Persona's File Changes state failed. The Team Directory rename proceeds regardless.</summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not move File Changes state for '{OldName}' to '{NewName}'.")]
    private static partial void LogFileStateMoveFailed(ILogger logger, string oldName, string newName, Exception exception);

    /// <summary>Logs that deleting a removed Persona's File Changes state failed.</summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not remove File Changes state for '{Name}'.")]
    private static partial void LogFileStateRemoveFailed(ILogger logger, string name, Exception exception);

    /// <summary>Logs that moving a renamed Persona's Room Session state failed.</summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not move Room Session state for '{OldName}' to '{NewName}'.")]
    private static partial void LogRoomSessionsMoveFailed(ILogger logger, string oldName, string newName, Exception exception);

    /// <summary>Logs that deleting a removed Persona's Room Session state failed.</summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not remove Room Session state for '{Name}'.")]
    private static partial void LogRoomSessionsRemoveFailed(ILogger logger, string name, Exception exception);
}
