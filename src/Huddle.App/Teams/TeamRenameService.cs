using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.SecondBrain;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// Renames a Team (Team Rename Spec §5.2-5.5, as settled in corrections-batch2 tr5): validates the new name, journals the
/// rename, pauses Task writes and the Team Room reconciler, moves the folders, renames the store rows, runs the step-6
/// rewrites, rewrites every member's label and finishes. Once the row is Done and the pauses and the scope are released it posts
/// the Team Room line (step 8, <c>room.teamRenamed</c>); a failed post is logged and never fails the rename. Renames and
/// archives serialise on one <see cref="TeamOperationGate"/>.
/// </summary>
internal sealed partial class TeamRenameService(
    LibraryRootStore roots,
    ITeamCatalog catalog,
    ITeamMembership membership,
    ITeamFolders folders,
    TaskStore tasks,
    TaskIdAllocator prefixes,
    CadenceStateStore cadences,
    PersonaStore personas,
    ITeamDirectory directory,
    IArchiveFileSystem files,
    ITeamRenames renames,
    TeamRoomReconciler reconciler,
    IEnumerable<ITeamRenameRewrite> rewrites,
    TeamOperationGate gate,
    ILogger<TeamRenameService> logger,
    IFeedPublisher? feed = null,
    ChatService? chat = null,
    IPromptSource? prompts = null)
{
    /// <summary>The prefix of the temporary folder a case-only rename passes through; it is not an archive dot prefix.</summary>
    internal const string RenamingPrefix = ".huddle-renaming-";

    private const int StepAttempts = 3;
    private const int LabelRetries = 3;

    private readonly IReadOnlyList<ITeamRenameRewrite> rewriteList = [.. rewrites];

    /// <summary>Renames the Team.</summary>
    /// <param name="oldName">The Team's name, compared ignoring case.</param>
    /// <param name="newName">The new name.</param>
    /// <param name="ct">Cancels the wait for the gate and the awaited work.</param>
    /// <returns>Renamed, or the plain sentence saying why not.</returns>
    public async Task<TeamRenameOutcome> RenameTeamAsync(string oldName, string newName, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(oldName);
        ArgumentNullException.ThrowIfNull(newName);

        using IDisposable entered = await gate.EnterAsync(ct).ConfigureAwait(false);
        TeamSummary? summary = catalog.Find(oldName);
        if (summary is null)
        {
            return Refuse($"There is no Team named \"{oldName}\".");
        }

        string? problem = this.Validate(summary.Name, newName);
        if (problem is not null)
        {
            return Refuse(problem);
        }

        return await this.RenameLockedAsync(summary.Name, newName, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Start-up recovery (§5.4): for each Pending Team row, in id order, decides from disk how far the move got (Old still there:
    /// it never happened, so the row is discarded; the temporary folder: finish the move; New: finish the archived folder), then
    /// rolls forward and completes. Task writes are paused as in a rename; the reconciler has not started, so it is not suspended.
    /// A row that still fails stays Pending and is logged.
    /// </summary>
    /// <param name="ct">Cancels the wait for the gate and the awaited work.</param>
    public async Task RecoverAsync(CancellationToken ct)
    {
        using IDisposable entered = await gate.EnterAsync(ct).ConfigureAwait(false);
        foreach (TeamRename row in renames.Pending().OrderBy(static r => r.Id))
        {
            List<IDisposable> pauses = [];
            bool completed;
            try
            {
                this.PauseWrites(row, pauses);
                completed = await this.RecoverRowAsync(row, ct).ConfigureAwait(false);
            }
            finally
            {
                foreach (IDisposable pause in pauses)
                {
                    pause.Dispose();
                }
            }

            if (completed)
            {
                await this.PostRenamedLineAsync(row, ct).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Step 8: tells the Team Room, if it has one, that the Team was renamed. A failure is logged and swallowed.</summary>
    private Task PostRoomLineAsync(string oldName, string newName, CancellationToken ct) =>
        this.PostLineAsync(newName, "room.teamRenamed", [("oldName", oldName), ("newName", newName)], ct);

    /// <summary>Posts one rendered Prompt into the Team's Room, if it has one. A failure is logged and swallowed.</summary>
    /// <param name="team">The Team whose Room is told.</param>
    /// <param name="key">The Prompt key.</param>
    /// <param name="values">The Prompt's placeholder values.</param>
    /// <param name="ct">Cancels the post.</param>
    private async Task PostLineAsync(string team, string key, (string Name, string Value)[] values, CancellationToken ct)
    {
        if (chat is null || prompts is null)
        {
            return;
        }

        try
        {
            Room? room = await directory.FindTeamRoomAsync(team, ct).ConfigureAwait(false);
            if (room is not null)
            {
                _ = await chat.PostAsHuddleAsync(room.Id, prompts.Say(key, values), ct).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            // Includes a cancellation: the rename is already Done, so a cancelled post must not fail it.
            logger.LogWarning(ex, "The Team Room line {Key} for {Team} could not be posted.", key, team);
        }
    }

    private async Task<bool> RecoverRowAsync(TeamRename row, CancellationToken ct)
    {
        bool neverMoved;
        string? failure = row.Kind == TeamRenameKind.Project
            ? this.FinishProjectMoveFromDisk(row, out neverMoved)
            : this.FinishMovesFromDisk(row, out neverMoved);
        if (neverMoved)
        {
            renames.Discard(row.Id);
            return false;
        }

        failure ??= await this.RollForwardAsync(row, ct).ConfigureAwait(false);
        if (failure is null)
        {
            renames.Complete(row.Id);
            return true;
        }

        logger.LogWarning("Recovering the rename of {Team} to {NewName} left it Pending: {Problem}", row.OldName, row.NewName, failure);
        this.NotifyUnfinished(row, failure);
        return false;
    }

    /// <summary>Posts the one Feed Notification (§5.4) that a rename from step 5 on did not finish.</summary>
    /// <param name="row">The Pending row.</param>
    /// <param name="problem">What failed; a trailing "Huddle will finish it at the next start." is not repeated.</param>
    private void NotifyUnfinished(TeamRename row, string problem)
    {
        if (feed is null)
        {
            return;
        }

        string what = problem.Replace(" Huddle will finish it at the next start.", string.Empty, StringComparison.Ordinal).TrimEnd('.', ' ');
        bool project = row.Kind == TeamRenameKind.Project;
        string subject = project ? $"Project {row.OldName} to {row.NewName} in {row.Team}" : $"{row.OldName} to {row.NewName}";
        string text = $"Renaming {subject} did not finish: {what}. Huddle will try again at the next start.";
        string team = project ? row.Team : row.NewName;
        try
        {
            feed.Post(new FeedMessage(FeedSourceType.TeamRenameStuck, "team-rename:" + team, FeedActor.Huddle, true, new TeamRenameStuckPayload(team, text)));
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            logger.LogWarning(ex, "The Feed Notification for the unfinished rename of {Team} could not be posted.", row.OldName);
        }
    }

    /// <summary>Reads the folders and finishes the move that a crash cut short.</summary>
    /// <param name="row">The Pending row.</param>
    /// <param name="neverMoved">Set when <c>Teams/Old</c> is still there with no temporary folder and no <c>Teams/New</c>.</param>
    /// <returns>The problem sentence, or <see langword="null"/> when the folders are as the row says.</returns>
    private string? FinishMovesFromDisk(TeamRename row, out bool neverMoved)
    {
        string teamsRoot = this.RootPath(LibraryRootKind.Teams);
        string archiveRoot = this.RootPath(LibraryRootKind.Archive);
        bool caseOnly = CaseOnly(row.OldName, row.NewName);
        (string? tempOnDisk, bool newThere, bool oldThere) = this.ReadDisk(teamsRoot, row.OldName, row.NewName);
        neverMoved = oldThere && tempOnDisk is null && !newThere;
        if (neverMoved)
        {
            return null;
        }

        if (tempOnDisk is null && !newThere)
        {
            return $"Neither {row.OldName} nor {row.NewName} has a folder to finish.";
        }

        return folders.RunExclusive(() =>
        {
            string? failure = tempOnDisk is null
                ? null
                : this.MoveOne(teamsRoot, tempOnDisk, row.NewName, false, "The Team folder");
            return failure ?? this.FinishArchivedMove(archiveRoot, row, caseOnly);
        });
    }

    /// <summary>Reads how far a folder rename under <paramref name="parent"/> got: the temporary folder, and whether the new and the old name are there (a case-only rename compares the spelling).</summary>
    /// <param name="parent">The folder that holds the renamed one.</param>
    /// <param name="oldName">The old name.</param>
    /// <param name="newName">The new name.</param>
    private (string? TempOnDisk, bool NewThere, bool OldThere) ReadDisk(string parent, string oldName, string newName)
    {
        bool caseOnly = CaseOnly(oldName, newName);
        string? tempOnDisk = this.Spelling(parent, RenamingPrefix + newName);
        string? newSpelling = this.Spelling(parent, newName);
        bool newRespelled = newSpelling is not null && string.Equals(newSpelling, newName, StringComparison.Ordinal);
        bool newThere = newSpelling is not null && (!caseOnly || newRespelled);
        bool oldThere = caseOnly ? newSpelling is not null && !newRespelled : this.Spelling(parent, oldName) is not null;
        return (tempOnDisk, newThere, oldThere);
    }

    /// <summary>Finishes <c>Archive/Old</c>: from the temporary folder a case-only crash left, or from the old spelling (found ignoring case).</summary>
    private string? FinishArchivedMove(string archiveRoot, TeamRename row, bool caseOnly)
    {
        string? archivedTemp = this.Spelling(archiveRoot, RenamingPrefix + row.NewName);
        if (archivedTemp is not null)
        {
            return this.MoveOne(archiveRoot, archivedTemp, row.NewName, false, "The archived folder");
        }

        string? archivedOld = this.Spelling(archiveRoot, row.OldName);
        return archivedOld is null || (caseOnly && string.Equals(archivedOld, row.NewName, StringComparison.Ordinal))
            ? null
            : this.MoveOne(archiveRoot, archivedOld, row.NewName, caseOnly, "The archived folder");
    }

    private static TeamRenameOutcome Refuse(string error) => new(false, error);

    private static bool CaseOnly(string oldName, string newName) =>
        string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase);

    private async Task<TeamRenameOutcome> RenameLockedAsync(string oldName, string newName, CancellationToken ct)
    {
        bool caseOnly = CaseOnly(oldName, newName);
        long id = renames.Begin(TeamRenameKind.Team, oldName, oldName, newName);
        TeamRename row = renames.Pending().First(r => r.Id == id);
        RenameScope scope;
        try
        {
            scope = await this.EnterScopeAsync(row, ct).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Nothing has moved yet, so the row is dropped; the scope entry released whatever it had taken.
            renames.Discard(id);
            throw;
        }

        TeamRenameOutcome outcome = await this.FinishInScopeAsync(row, scope, caseOnly, ct).ConfigureAwait(false);
        if (outcome.Renamed)
        {
            await this.PostRoomLineAsync(oldName, newName, ct).ConfigureAwait(false);
        }

        return outcome;
    }

    /// <summary>Steps 3 to 7 inside the entered scope; the scope is released before this returns.</summary>
    private async Task<TeamRenameOutcome> FinishInScopeAsync(TeamRename row, RenameScope scope, bool caseOnly, CancellationToken ct)
    {
        string oldName = row.OldName;
        string newName = row.NewName;
        long id = row.Id;
        bool finished = false;
        try
        {
            string? moveFailure = this.MoveFolders(oldName, newName, caseOnly, out bool undoFailed);
            if (undoFailed)
            {
                return Refuse($"{moveFailure} The Team folder could not be put back, so Huddle will finish the rename at the next start.");
            }

            if (moveFailure is not null)
            {
                renames.Discard(id);
                return Refuse(moveFailure);
            }

            string? error = await this.RollForwardAsync(row, ct).ConfigureAwait(false);
            if (error is null)
            {
                renames.Complete(id);
                finished = true;
            }
            else
            {
                this.NotifyUnfinished(row, error);
            }

            return new TeamRenameOutcome(finished, error);
        }
        finally
        {
            await scope.ReleaseAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Steps 5 to 7: the store rows, the rewrites and the labels, each retried; the first that still fails is the error.</summary>
    private async Task<string?> RollForwardAsync(TeamRename row, CancellationToken ct)
    {
        if (row.Kind == TeamRenameKind.Project)
        {
            return await this.RollForwardProjectAsync(row, ct).ConfigureAwait(false);
        }

        string? error = await this.RetryAsync(
            "the Room, Task prefix and cadence rows",
            async () =>
            {
                _ = await directory.RenameTeamScopeAsync(row.OldName, row.NewName, ct).ConfigureAwait(false);
                prefixes.RenameTeam(row.OldName, row.NewName);
                cadences.Rename(row.OldName, row.NewName);
            }).ConfigureAwait(false);
        if (error is not null)
        {
            return error;
        }

        error = await this.RunRewritesAsync(row, ct).ConfigureAwait(false);
        return error ?? this.RewriteLabels(row);
    }

    /// <summary>Runs every step-6 rewrite in registration order, each retried.</summary>
    /// <returns>The first error sentence, or <see langword="null"/> when all ran.</returns>
    private async Task<string?> RunRewritesAsync(TeamRename row, CancellationToken ct)
    {
        foreach (ITeamRenameRewrite rewrite in this.rewriteList)
        {
            string? error = await this.RetryAsync("the files that mention the Team", () => rewrite.RunAsync(row, ct)).ConfigureAwait(false);
            if (error is not null)
            {
                return error;
            }
        }

        return null;
    }

    /// <summary>Step 7: rewrites the labels, then re-checks every Persona and retries while one still holds the old name.</summary>
    private string? RewriteLabels(TeamRename row)
    {
        for (int attempt = 0; attempt <= LabelRetries; attempt++)
        {
            try
            {
                membership.RenameLabel(row.OldName, row.NewName);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ChatException)
            {
                logger.LogWarning(ex, "Rewriting the {Team} labels failed; checking and retrying.", row.OldName);
            }

            if (this.PersonasStillHolding(row).Count == 0)
            {
                return null;
            }
        }

        string names = string.Join(", ", this.PersonasStillHolding(row));
        logger.LogWarning("The rename of {Team} left the old label on {Personas}.", row.OldName, names);
        return $"{row.OldName} was renamed to {row.NewName}, but the Team label of {names} could not be rewritten. Huddle will finish it at the next start.";
    }

    private List<string> PersonasStillHolding(TeamRename row)
    {
        List<string> holding = [];
        foreach (string name in personas.ListNames())
        {
            Persona? persona = personas.Get(name);
            if (persona is not null
                && PersonaFrontmatter.TryReadIdentity(persona.Text, out PersonaIdentity? identity, out _)
                && identity.Teams.Any(t => string.Equals(t, row.OldName, StringComparison.OrdinalIgnoreCase) && !string.Equals(t, row.NewName, StringComparison.Ordinal)))
            {
                holding.Add(name);
            }
        }

        return holding;
    }

    /// <summary>Runs a step up to <see cref="StepAttempts"/> times.</summary>
    /// <returns>The error sentence when every attempt failed; otherwise <see langword="null"/>.</returns>
    private async Task<string?> RetryAsync(string what, Func<Task> step)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                await step().ConfigureAwait(false);
                return null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Renaming {What} failed.", what);
                if (attempt >= StepAttempts)
                {
                    return $"The Team was renamed on disk, but {what} could not be updated: {ex.Message} Huddle will finish it at the next start.";
                }
            }
        }
    }

    /// <summary>The one rule both a Team and a Project rename share: none starts while any row is Pending.</summary>
    /// <returns>The refusal sentence, or <see langword="null"/> when no row is Pending.</returns>
    private string? PendingRefusal() =>
        // The Turn hook hears "the highest id"; a lower id finished later would be missed.
        renames.Pending().Count > 0 ? "Another rename is still finishing. Try again after Huddle restarts." : null;

    /// <summary>Step 1: the name rules (§5.5), including the journal and the prefix table.</summary>
    /// <returns>The refusal sentence, or <see langword="null"/> when the name is acceptable.</returns>
    private string? Validate(string oldName, string newName)
    {
        if (this.PendingRefusal() is { } pending)
        {
            return pending;
        }

        if (string.Equals(oldName, newName, StringComparison.Ordinal))
        {
            return $"The Team is already named \"{oldName}\".";
        }

        bool caseOnly = CaseOnly(oldName, newName);
        List<TeamSummary> others = [.. catalog.Teams.Where(t => !caseOnly || !string.Equals(t.Name, oldName, StringComparison.OrdinalIgnoreCase))];
        List<string> archived = [.. this.ArchivedTeamNames().Where(a => !string.Equals(a, oldName, StringComparison.OrdinalIgnoreCase))];
        string? problem = TeamNames.ValidateTeamName(newName, others, archived);
        if (problem is not null)
        {
            return problem;
        }

        if (caseOnly)
        {
            return null;
        }

        if (renames.IsReserved(newName) && !string.Equals(renames.Resolve(newName), oldName, StringComparison.OrdinalIgnoreCase))
        {
            return $"The name \"{newName}\" was used by an earlier Team rename and is kept for its old links. Pick another name.";
        }

        return prefixes.HasPrefix(newName)
            ? $"The name \"{newName}\" still holds Task ids of an earlier Team of that name. Pick another name."
            : null;
    }

    private List<string> ArchivedTeamNames()
    {
        string archiveRoot = this.RootPath(LibraryRootKind.Archive);
        return [.. files.ListEntries(archiveRoot).Where(e => files.FileExists(Path.Combine(archiveRoot, e, TeamPaths.TeamFileName)))];
    }

    private string RootPath(LibraryRootKind kind) => roots.Roots.First(r => r.Kind == kind).FullPath;

    /// <summary>Step 3: the pauses (old, new and every Team a rewrite names) and the reconciler suspension.</summary>
    private async Task<RenameScope> EnterScopeAsync(TeamRename row, CancellationToken ct)
    {
        List<IDisposable> pauses = [];
        try
        {
            this.PauseWrites(row, pauses);
            IAsyncDisposable suspension = await reconciler.SuspendAsync(ct).ConfigureAwait(false);
            return new RenameScope(pauses, suspension);
        }
        catch (Exception)
        {
            // Whatever was paused before the failure (a cancelled wait for the reconciler) is lifted again.
            foreach (IDisposable pause in pauses)
            {
                pause.Dispose();
            }

            throw;
        }
    }

    /// <summary>Pauses Task writes for the old and new names and every Team a rewrite names.</summary>
    /// <param name="row">The rename.</param>
    /// <param name="pauses">Receives each pause as it is taken, so a caller can lift them when a later one fails.</param>
    private void PauseWrites(TeamRename row, List<IDisposable> pauses)
    {
        List<string> names = row.Kind == TeamRenameKind.Project ? [row.Team] : [row.OldName, row.NewName];
        foreach (ITeamRenameRewrite rewrite in this.rewriteList)
        {
            names.AddRange(rewrite.TeamsTouched(row));
        }

        foreach (string name in names.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            pauses.Add(tasks.PauseWrites(name));
        }
    }

    /// <summary>Step 4: moves <c>Teams/Old</c>, then <c>Archive/Old</c> when it exists; a failure puts back what moved.</summary>
    /// <param name="oldName">The Team's current name.</param>
    /// <param name="newName">The new name.</param>
    /// <param name="caseOnly">Whether only the case differs.</param>
    /// <param name="undoFailed">Set when the Teams folder moved but could not be put back after the archived move failed.</param>
    /// <returns>The refusal sentence, or <see langword="null"/> when both moved.</returns>
    private string? MoveFolders(string oldName, string newName, bool caseOnly, out bool undoFailed)
    {
        undoFailed = false;
        string teamsRoot = this.RootPath(LibraryRootKind.Teams);
        string archiveRoot = this.RootPath(LibraryRootKind.Archive);
        string? onDisk = this.Spelling(teamsRoot, oldName);
        if (onDisk is null)
        {
            return $"{oldName} has no folder to rename.";
        }

        string? archived = this.Spelling(archiveRoot, oldName);
        bool stranded = false;
        string? result = folders.RunExclusive(() =>
        {
            string? failure = this.MoveOne(teamsRoot, onDisk, newName, caseOnly, "The Team folder");
            if (failure is not null || archived is null)
            {
                return failure;
            }

            failure = this.MoveOne(archiveRoot, archived, newName, caseOnly, "The archived folder");
            if (failure is not null)
            {
                stranded = this.MoveOne(teamsRoot, newName, onDisk, caseOnly, "The Team folder") is not null;
            }

            return failure;
        });
        undoFailed = stranded;
        return result;
    }

    private string? Spelling(string parent, string name) =>
        files.ListEntries(parent).FirstOrDefault(e => string.Equals(e, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Renames <c>parent/from</c> to <c>parent/to</c>, through a temporary name when only the case differs.</summary>
    private string? MoveOne(string parent, string from, string to, bool caseOnly, string what)
    {
        try
        {
            ArchiveMoveResult result = caseOnly
                ? this.MoveViaTemp(parent, from, to)
                : ArchiveMove.Rename(Path.Combine(parent, from), Path.Combine(parent, to), files);
            return result == ArchiveMoveResult.Moved
                ? null
                : $"{what} \"{from}\" has files open in another program. Close them and try again.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $"{what} \"{from}\" could not be renamed: {ex.Message}";
        }
    }

    private ArchiveMoveResult MoveViaTemp(string parent, string from, string to)
    {
        string temp = Path.Combine(parent, RenamingPrefix + to);
        ArchiveMoveResult first = ArchiveMove.Rename(Path.Combine(parent, from), temp, files);
        return first == ArchiveMoveResult.Moved
            ? ArchiveMove.Rename(temp, Path.Combine(parent, to), files)
            : first;
    }

    /// <summary>The pauses and the reconciler suspension, lifted in the step-9 order: pauses, then the suspension.</summary>
    private sealed class RenameScope(List<IDisposable> pauses, IAsyncDisposable suspension)
    {
        /// <summary>Lifts every pause, then releases the reconciler (one reconcile).</summary>
        public async Task ReleaseAsync()
        {
            foreach (IDisposable pause in pauses)
            {
                pause.Dispose();
            }

            await suspension.DisposeAsync().ConfigureAwait(false);
        }
    }
}
