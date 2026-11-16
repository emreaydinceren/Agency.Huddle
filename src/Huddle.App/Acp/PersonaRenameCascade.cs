using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// Cascades a Persona rename into the Team Directory, and a Persona removal into its Avatar.
/// Subscribes to <see cref="PersonaStore.PersonaRenamed"/> and, for the Agent that Name used to
/// identify: renames its Team Directory row in place, re-derives any Room that was still carrying its
/// auto-name, and moves its Work Dir — see
/// <c>docs/adr/0011-a-rename-moves-the-agent-not-its-history.md</c> for why this is a rename in
/// place rather than a stop-and-start under a new id. Also moves the renamed Persona's Avatar entry
/// and its <see cref="FileStateStore"/> file under the same subscription (FC §6.12), and subscribes
/// to <see cref="PersonaStore.PersonaRemoved"/> to delete a removed Persona's Avatar entry, image
/// file and <see cref="FileStateStore"/> file - see <see cref="OnPersonaRemoved"/>'s doc comment for
/// why a removal cascades here even though <c>docs/engineering/rules.md</c> row 38 says a removal does
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
/// <param name="agentPaths">Locates the Work Dir to move.</param>
/// <param name="timeProvider">Drives the backoff between Work Dir move attempts, so a test can control it without a real delay.</param>
/// <param name="logger">Records a rejected rename, a Work Dir that could not be moved, a file state move that failed, a Room Session move that failed, and any failure in the detached half of the cascade.</param>
/// <param name="tasks">Rewrites <c>creator:</c>/<c>assignee:</c> in a renamed Persona's Task files — Spec §9.6.</param>
/// <param name="views">Rewrites a renamed Persona's assignee filter in every saved View — Spec §9.6.</param>
/// <param name="folderMoves">Signals an Agent-folder move as pending before it starts and settled once it finishes, so <c>DotAcpAgentHostFactory</c> can wait for it instead of racing it — corrections-B2 item 20.</param>
/// <param name="spend">Moves a renamed Persona's display-only Spend, and forgets a removed Persona's — the Turn detail spec, section 6.6.</param>
/// <param name="lastCatchUp">Moves a renamed Persona's catch-up records and notes, and deletes a removed Persona's.</param>
/// <param name="commands">Moves a renamed Persona's offered Adapter commands, and forgets a removed Persona's — the Commands spec, section 6.3.</param>
internal sealed partial class PersonaRenameCascade(
    ITeamDirectory teamDirectory,
    PersonaStore personaStore,
    RoomEvents roomEvents,
    AvatarStore avatars,
    FileStateStore fileState,
    RoomSessionStore roomSessions,
    AgentPaths agentPaths,
    TimeProvider timeProvider,
    ILogger<PersonaRenameCascade> logger,
    TaskService tasks,
    ViewStore views,
    AgentFolderMoves folderMoves,
    PersonaSpend spend,
    PersonaCommands commands,
    LastCatchUpStore lastCatchUp) : IHostedService, IDisposable
{
    // The runner it raced against restarts within milliseconds and inherits the Work Dir as its cwd,
    // so a handful of short retries covers the ordinary case (the old process has not yet exited)
    // without leaving a hand-edited file locked out for long.
    private const int MaxWorkDirMoveAttempts = 5;
    private static readonly TimeSpan WorkDirMoveRetryDelay = TimeSpan.FromMilliseconds(250);

    /// <summary>How many times a removed Agent's browser profile delete is tried while its browser shuts down.</summary>
    private const int MaxProfileDeleteAttempts = 20;

    /// <summary>The wait between two profile delete attempts.</summary>
    private static readonly TimeSpan ProfileDeleteRetryDelay = TimeSpan.FromMilliseconds(500);

    private bool subscribed;

    // PersonaStore raises PersonaRemoved for the OLD Name right after PersonaRenamed (it diffs Names), so a removal handler cannot tell a
    // rename from a delete by itself. The old Names of renames not yet followed by their removal are kept here, so the removal that is
    // really a rename never deletes a browser profile the detached move is about to carry to the new Name.
    private readonly Lock renamedAwayGate = new();
    private readonly HashSet<string> renamedAway = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        personaStore.PersonaRenamed += this.OnPersonaRenamed;
        personaStore.PersonaRemoved += this.OnPersonaRemoved;
        this.subscribed = true;

        // The startup sweep of browser profile folders no Persona owns: a removal while the app was off, or one whose delete failed on a
        // locked folder, leaves one behind, and the sign-ins in it must not wait for a same-named Persona to be created.
        this.SweepBrowserProfiles();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Deletes every folder under <c>browser-profiles</c> whose name no Persona has. The name match ignores case on every OS: it refuses
    /// a delete, so a folder that differs only by case from a Persona's Name is kept even on a case-sensitive file system. Never throws; each
    /// failure is logged and the sweep goes on.
    /// </summary>
    internal void SweepBrowserProfiles()
    {
        try
        {
            string root = agentPaths.BrowserProfilesRoot;

            // A Persona file that failed to load still names an Agent the Human may fix; sweeping now would delete its sign-ins.
            if (!Directory.Exists(root) || personaStore.RejectedFiles.Count > 0)
            {
                return;
            }

            HashSet<string> names = new(personaStore.ListNames(), StringComparer.OrdinalIgnoreCase);
            foreach (string folder in Directory.EnumerateDirectories(root))
            {
                string name = Path.GetFileName(folder);
                if (!names.Contains(name))
                {
                    this.DeleteBrowserProfile(folder, name);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogBrowserProfileSweepFailed(logger, ex);
        }
    }

    /// <summary>
    /// Deletes a removed Agent's browser profile, retrying while the browser its stopping host started still holds files, so a same-named
    /// Agent hired soon after never inherits the sign-ins. The first attempt runs before the first await, so an unlocked folder is gone on return.
    /// After the last attempt the failure is logged and the startup sweep tries again.
    /// </summary>
    /// <param name="folder">The folder to delete.</param>
    /// <param name="name">The Persona Name the folder is for, for the failure log.</param>
    /// <returns>A task that ends when the folder is gone or the attempts ran out.</returns>
    private async Task DeleteBrowserProfileWithRetryAsync(string folder, string name)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                BrowserProfileFolders.DeleteTree(folder);
                return;
            }
            catch (Exception ex) when ((ex is IOException or UnauthorizedAccessException) && attempt < MaxProfileDeleteAttempts)
            {
                await Task.Delay(ProfileDeleteRetryDelay).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                LogBrowserProfileDeleteFailed(logger, name, ex);
                return;
            }
        }
    }
    /// <summary>Deletes one browser profile folder, logging a failure instead of throwing.</summary>
    /// <param name="folder">The folder to delete.</param>
    /// <param name="name">The Persona Name the folder is for, for the failure log.</param>
    private void DeleteBrowserProfile(string folder, string name)
    {
        try
        {
            BrowserProfileFolders.DeleteTree(folder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogBrowserProfileDeleteFailed(logger, name, ex);
        }
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
        lock (this.renamedAwayGate)
        {
            _ = this.renamedAway.Remove(renamed.NewName);

            // A case-only rename raises no removal (the Names match ignoring case), so nothing would ever consume the marker.
            if (!string.Equals(renamed.OldName, renamed.NewName, StringComparison.OrdinalIgnoreCase))
            {
                _ = this.renamedAway.Add(renamed.OldName);
            }
        }

        this.RenamePersonaScopedState(renamed);
        this.RenameTaskAndViewReferences(renamed);

        // corrections-B2 item 18: ABOVE the "no Agent row" early return below, same reasoning as the
        // Avatar/file-state/Room-Session/Task/View moves above it - the Agent folder exists (once
        // any Work Dir has ever been created inside it, or a definition file placed there) whether or
        // not an Agent has ever registered, so gating the move on the Team Directory row would leave
        // a stock installation (Acp:Enabled false by default) never moving it at all.
        //
        // Begun synchronously, here, rather than inside the detached task below: corrections-B2 item
        // 20 needs the pending state visible to a concurrent DotAcpAgentHostFactory.StartAsync the
        // instant this method returns, not after the detached task has had a chance to run.
        var folderMoveHandle = folderMoves.Begin(renamed.NewName);
        _ = this.MoveAgentFolderDetachedAsync(folderMoveHandle, renamed.OldName, renamed.NewName);

        var user = teamDirectory.FindUserByName(renamed.OldName);
        if (user is null || user.Kind != UserKind.Agent)
        {
            // No Agent has ever registered under the old Name - the normal case when Acp:Enabled is
            // false, or when this Persona has simply never connected. There is nothing left to cascade.
            return;
        }

        if (!teamDirectory.RenameUser(user.Id, renamed.NewName))
        {
            // The likeliest cause is that the new Name is already held by a different Agent. A
            // partial cascade (a Room renamed under a Name the users row never took) is worse than
            // none, so stop here - the Agent folder still moves above, independently.
            LogRenameRejected(logger, renamed.OldName, renamed.NewName);
            return;
        }

        _ = this.CascadeDetachedAsync(user.Id, renamed.OldName, renamed.NewName);
    }

    /// <summary>
    /// Moves the Avatar, Spend, offered commands, File Changes state and Room Session state of the
    /// renamed Persona. Runs ABOVE the "no Agent row" early return of <see cref="OnPersonaRenamed"/>.
    /// </summary>
    /// <param name="renamed">The old and the new Name of the Persona that changed.</param>
    private void RenamePersonaScopedState(PersonaRenamed renamed)
    {
        // FIRST, and deliberately ABOVE the early return in OnPersonaRenamed: an Avatar exists whether or not an
        // Agent has ever connected, so gating it on the Team Directory row would silently lose the
        // avatar of an Agent that has never run - which, with Acp:Enabled false by default, is
        // every Agent in a stock installation.
        //
        // Synchronous, not detached: like the Team Directory row rename below, this races the
        // repaint PersonasChanged triggers - the Agent card and /agents both read AvatarStore
        // on render - and unlike a Room name, an Avatar has no later event that would correct a
        // stale read. Detaching it would let the OLD Name's avatar render, or none at all, for
        // however long the detached task takes to run.
        avatars.Rename(renamed.OldName, renamed.NewName);

        // Spend is per Persona Name and exists whether or not an Agent row does, so, like the Avatar
        // just above, it must sit ABOVE the "no Agent row" early return below: that guard fires for
        // every Agent in a stock installation, where Acp:Enabled is false. In memory only, so it
        // cannot fail or block.
        spend.Rename(renamed.OldName, renamed.NewName);

        // The offered Adapter commands are the same kind of thing: per Persona Name, in memory, and
        // present whether or not an Agent row exists, so they sit above the same early return.
        commands.Rename(renamed.OldName, renamed.NewName);

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
            lastCatchUp.Rename(renamed.OldName, renamed.NewName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogLastCatchUpMoveFailed(logger, renamed.OldName, renamed.NewName, ex);
        }

        try
        {
            roomSessions.Rename(renamed.OldName, renamed.NewName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogRoomSessionsMoveFailed(logger, renamed.OldName, renamed.NewName, ex);
        }
    }

    /// <summary>
    /// Rewrites the renamed Persona's Task and saved-View references. Runs ABOVE the "no Agent row"
    /// early return of <see cref="OnPersonaRenamed"/>.
    /// </summary>
    /// <param name="renamed">The old and the new Name of the Persona that changed.</param>
    private void RenameTaskAndViewReferences(PersonaRenamed renamed)
    {
        // Same placement and the same reason again (Spec §9.6): a Task's creator/assignee, and a
        // saved View's assignee filter, both exist whether or not an Agent has ever connected, so
        // this must sit ABOVE the "no Agent row" early return too. Two separate try/catch blocks, so
        // a failure renaming Task files never skips the View rename or the race-critical Team
        // Directory rename below - and, unlike the moves above, TaskService.RenameAgent itself
        // already never throws (Settled corrections-B2 D6 item 11), so this catch only guards
        // against ViewStore.RenameAgent's own file I/O.
        try
        {
            tasks.RenameAgent(renamed.OldName, renamed.NewName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogTaskRenameFailed(logger, renamed.OldName, renamed.NewName, ex);
        }

        try
        {
            views.RenameAgent(renamed.OldName, renamed.NewName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogViewRenameFailed(logger, renamed.OldName, renamed.NewName, ex);
        }
    }

    /// <summary>
    /// Deletes the removed Persona's Avatar entry and image file. <c>docs/engineering/rules.md</c> row
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
        spend.Forget(removed.Name);
        commands.Forget(removed.Name);

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
            lastCatchUp.Remove(removed.Name);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogLastCatchUpRemoveFailed(logger, removed.Name, ex);
        }

        try
        {
            roomSessions.Remove(removed.Name);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogRoomSessionsRemoveFailed(logger, removed.Name, ex);
        }

        // The Agent's browser sign-ins go with it (#551, owner decision), unless this "removal" is the old Name of a rename, whose profile the
        // move carries to the new Name. A folder still locked by a running browser is left for the startup sweep.
        bool wasRenamed;
        lock (this.renamedAwayGate)
        {
            wasRenamed = this.renamedAway.Remove(removed.Name);
        }

        if (!wasRenamed)
        {
            // The Agent's host is still stopping, so its browser may hold the folder for a moment: the first try runs now, the rest retry briefly.
            _ = this.DeleteBrowserProfileWithRetryAsync(agentPaths.BrowserProfile(removed.Name), removed.Name);
        }
    }

    /// <summary>
    /// The half of the cascade that races nothing: re-deriving auto-named Rooms. Fire-and-forget from
    /// <see cref="OnPersonaRenamed"/>, so any failure has no caller left to observe it.
    /// </summary>
    private async Task CascadeDetachedAsync(string userId, string oldName, string newName)
    {
        try
        {
            await this.RenameAutoNamedRoomsAsync(userId, oldName).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // This is a file-watcher-driven background path with nothing left to observe a thrown
            // exception: PersonaRenamed is raised from inside PersonaStore's debounce callback, and
            // an exception escaping this detached task would go to the process' unobserved-exception
            // handler, not back to any caller. Logging and swallowing is the least-bad outcome - a
            // partially-applied cascade here is recoverable (the Team Directory rename already
            // committed; a Room name left behind is a cosmetic gap, not data loss) - whereas the
            // alternative, an unhandled exception on a background task, risks tearing down the
            // process for a cosmetic failure.
            LogCascadeFailed(logger, oldName, newName, ex);
        }
    }

    /// <summary>
    /// Moves the Agent folder, then releases <paramref name="handle"/> so any
    /// <see cref="AgentFolderMoves.WhenSettledAsync"/> waiter proceeds - whether the move
    /// succeeded, was skipped, or gave up. Fire-and-forget from <see cref="OnPersonaRenamed"/>, so any
    /// failure has no caller left to observe it - the same reasoning <see cref="CascadeDetachedAsync"/>
    /// gives for its own catch.
    /// </summary>
    private async Task MoveAgentFolderDetachedAsync(TaskCompletionSource handle, string oldName, string newName)
    {
        try
        {
            await this.MoveAgentFolderAsync(oldName, newName).ConfigureAwait(false);
            await this.MoveBrowserProfileAsync(oldName, newName).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogCascadeFailed(logger, oldName, newName, ex);
        }
        finally
        {
            folderMoves.Complete(handle);
        }
    }

    /// <summary>
    /// Re-derives the name of every Room the renamed Agent belongs to that was still carrying its
    /// auto-name, and leaves alone every Room the Human named by hand. Applies the same comparison
    /// <see cref="ChatService.InviteAsync(string, string, CancellationToken)"/> already makes, rather than inventing a second rule for
    /// when a Room may be renamed out from under somebody.
    /// </summary>
    private async Task RenameAutoNamedRoomsAsync(string userId, string oldName)
    {
        var rooms = await teamDirectory.GetRoomsForUserAsync(userId).ConfigureAwait(false);

        foreach (var room in rooms)
        {
            if (room.IsTeamRoom)
            {
                // A Team Room's name is its Team's (ADR-0052), whatever the derivation would say.
                continue;
            }

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
        // Agent whose Rooms the Human had all named by hand would otherwise keep showing the old
        // Name in the header until some unrelated event happened to repaint it. The Agent's Name
        // changing is a fact about every Room it belongs to, whatever the Rooms are called.
        roomEvents.PublishRoomsChanged();
    }

    /// <summary>
    /// Moves the renamed Agent's whole Agent folder from <c>{oldName}</c> to <c>{newName}</c>, then
    /// renames the definition file the move carried along unchanged (<c>{oldName}.md</c>) to
    /// <c>{newName}.md</c> - corrections-B2 item 21: <see cref="PersonaStore.Update"/> already wrote
    /// the new content to the OLD path before <see cref="PersonaStore.PersonaRenamed"/> was raised, so
    /// this is the only place that ever renames the file itself, and it happens exactly once. The
    /// folder holds the Work Dir - the agent process' <c>cwd</c>, not a jail, from which the Adapter
    /// auto-loads <c>CLAUDE.md</c> and <c>.claude/settings.json</c> - so leaving it behind would
    /// silently discard whatever the Agent had written for itself.
    /// </summary>
    private async Task MoveAgentFolderAsync(string oldName, string newName)
    {
        var source = agentPaths.AgentFolder(oldName);
        var target = agentPaths.AgentFolder(newName);

        if (!Directory.Exists(source))
        {
            // Normal: the folder is created on first Add/first start, and this Persona may never
            // have had either happen.
            return;
        }

        // corrections-B2 item 22: on a case-insensitive file system (Windows), a case-only rename such
        // as Nova -> NOVA makes Directory.Exists(target) true even though target IS source - the
        // "never merge" check below would wrongly treat this as a collision with someone else's
        // folder. Route a case-only rename through a temporary sibling name instead, on every OS, so
        // the same code path is exercised everywhere rather than only under Windows' case-folding.
        var caseOnlyChange = !string.Equals(oldName, newName, StringComparison.Ordinal)
            && string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase);

        if (!caseOnlyChange && Directory.Exists(target))
        {
            // Never merge - the target may belong to an unrelated Persona of that Name already.
            LogWorkDirTargetExists(logger, oldName, newName);
            return;
        }

        if (!await this.TryMoveDirectoryWithRetryAsync(source, target, caseOnlyChange, ex => LogWorkDirMoveFailed(logger, oldName, newName, ex)).ConfigureAwait(false))
        {
            return;
        }

        this.FinishFolderMove(target, oldName, newName);
    }

    /// <summary>
    /// Moves the renamed Agent's browser profile folder from <c>browser-profiles/{oldName}</c> to <c>browser-profiles/{newName}</c>, with the
    /// same retry as the Agent folder move and inside the same <see cref="AgentFolderMoves"/> gate, so a restart under the new Name never
    /// opens the browser on an empty profile while the sign-ins are still under the old one. A missing source is nothing to do, and an existing
    /// target is logged and left alone, as for the Agent folder.
    /// </summary>
    /// <param name="oldName">The Persona's old Name.</param>
    /// <param name="newName">The Persona's new Name.</param>
    private async Task MoveBrowserProfileAsync(string oldName, string newName)
    {
        string source = agentPaths.BrowserProfile(oldName);
        string target = agentPaths.BrowserProfile(newName);
        if (!Directory.Exists(source))
        {
            return;
        }

        bool caseOnlyChange = !string.Equals(oldName, newName, StringComparison.Ordinal)
            && string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase);
        if (!caseOnlyChange && Directory.Exists(target))
        {
            LogBrowserProfileTargetExists(logger, oldName, newName);
            return;
        }

        _ = await this.TryMoveDirectoryWithRetryAsync(source, target, caseOnlyChange, ex => LogBrowserProfileMoveFailed(logger, oldName, newName, ex)).ConfigureAwait(false);
    }

    /// <summary>Moves the folder, retrying a locked directory a few times before giving up.</summary>
    /// <param name="source">The old folder.</param>
    /// <param name="target">The new folder.</param>
    /// <param name="caseOnlyChange">Whether the rename only changes letter case, so the move goes through a temporary sibling.</param>
    /// <param name="logFailure">Logs the failure of the last attempt.</param>
    /// <returns><see langword="true"/> when the folder moved; <see langword="false"/> when every attempt failed and the failure was logged.</returns>
    private async Task<bool> TryMoveDirectoryWithRetryAsync(string source, string target, bool caseOnlyChange, Action<IOException> logFailure)
    {
        for (var attempt = 1; attempt <= MaxWorkDirMoveAttempts; attempt++)
        {
            try
            {
                if (caseOnlyChange)
                {
                    var sibling = source + ".renaming-" + Guid.NewGuid().ToString("N");
                    Directory.Move(source, sibling);
                    Directory.Move(sibling, target);
                }
                else
                {
                    Directory.Move(source, target);
                }

                break;
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
                logFailure(ex);
                return false;
            }
        }

        return true;
    }

    /// <summary>Renames the moved definition file, ensures the new Work Dir exists and rescans the Persona index.</summary>
    /// <param name="target">The new Agent folder.</param>
    /// <param name="oldName">The Persona's old Name.</param>
    /// <param name="newName">The Persona's new Name.</param>
    private void FinishFolderMove(string target, string oldName, string newName)
    {
        // The definition file travelled inside the folder still named for the OLD Name - rename it
        // now, exactly once, only if PersonaStore.Update actually left it there under that name.
        var movedDefinition = Path.Combine(target, $"{oldName}.md");
        if (File.Exists(movedDefinition))
        {
            File.Move(movedDefinition, agentPaths.DefinitionFile(newName));
        }

        // Always present after a completed move, whether or not the old folder ever had one - the
        // same "always created" guarantee DotAcpAgentHostFactory.StartAsync gives a freshly started
        // Persona (FC §6.15), now also given to a renamed one that has never started, so a File
        // Changes watch or a later start finds it ready either way.
        Directory.CreateDirectory(agentPaths.WorkDir(newName));

        // This store's own cached index still points at the pre-move path the instant the code
        // above returns - see RefreshIndexAndNotify's remarks. Rescan now rather than leave a
        // subsequent Update (an Agent renamed again right away) to fail writing to a folder this
        // move already renamed away, or wait out the file system watcher's own debounce.
        personaStore.RescanNow();
    }

    /// <summary>Logs that a Team Directory rename was rejected, most likely because the new Name is already held by another Agent.</summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not rename Agent '{OldName}' to '{NewName}' in the Team Directory - the new Name may already be held by another Agent. The cascade stopped here; no Room or Work Dir was touched.")]
    private static partial void LogRenameRejected(ILogger logger, string oldName, string newName);

    /// <summary>Logs that the detached half of a rename cascade (Rooms, Agent folder) failed.</summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "The rename cascade for '{OldName}' -> '{NewName}' failed after the Team Directory rename had already committed.")]
    private static partial void LogCascadeFailed(ILogger logger, string oldName, string newName, Exception exception);

    /// <summary>Logs that an Agent folder move was skipped because the target directory already exists.</summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Did not move the Agent folder for '{OldName}' to '{NewName}': a directory already exists at the target. Both were left in place.")]
    private static partial void LogWorkDirTargetExists(ILogger logger, string oldName, string newName);

    /// <summary>Logs that an Agent folder move failed after every retry.</summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not move the Agent folder for '{OldName}' to '{NewName}' after several attempts.")]
    private static partial void LogWorkDirMoveFailed(ILogger logger, string oldName, string newName, Exception exception);

    /// <summary>Logs that a browser profile folder move was skipped because the target already exists.</summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Did not move the browser profile for '{OldName}' to '{NewName}': a folder already exists at the target. Both were left in place.")]
    private static partial void LogBrowserProfileTargetExists(ILogger logger, string oldName, string newName);

    /// <summary>Logs that a browser profile folder move failed after every retry.</summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not move the browser profile for '{OldName}' to '{NewName}' after several attempts.")]
    private static partial void LogBrowserProfileMoveFailed(ILogger logger, string oldName, string newName, Exception exception);

    /// <summary>Logs that deleting a browser profile folder failed. The startup sweep tries again.</summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not delete the browser profile for '{Name}'.")]
    private static partial void LogBrowserProfileDeleteFailed(ILogger logger, string name, Exception exception);

    /// <summary>Logs that the startup sweep of browser profile folders could not list the folder.</summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not sweep the browser profile folders.")]
    private static partial void LogBrowserProfileSweepFailed(ILogger logger, Exception exception);

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

    /// <summary>Logs that moving a renamed Persona's catch-up records failed. The rename proceeds regardless.</summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not move catch-up records for '{OldName}' to '{NewName}'.")]
    private static partial void LogLastCatchUpMoveFailed(ILogger logger, string oldName, string newName, Exception exception);

    /// <summary>Logs that deleting a removed Persona's catch-up records failed. The removal proceeds regardless.</summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not delete catch-up records for '{Name}'.")]
    private static partial void LogLastCatchUpRemoveFailed(ILogger logger, string name, Exception exception);

    /// <summary>Logs that renaming an Agent's Task files failed (Spec §9.6). The Team Directory rename proceeds regardless.</summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not rename Task files for '{OldName}' to '{NewName}'.")]
    private static partial void LogTaskRenameFailed(ILogger logger, string oldName, string newName, Exception exception);

    /// <summary>Logs that renaming an Agent's saved View filters failed (Spec §9.6). The Team Directory rename proceeds regardless.</summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not rename View filters for '{OldName}' to '{NewName}'.")]
    private static partial void LogViewRenameFailed(ILogger logger, string oldName, string newName, Exception exception);
}
