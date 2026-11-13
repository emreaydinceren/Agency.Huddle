using Agency.Huddle.App.Data;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.Contracts;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.References;

/// <summary>
/// Looks each reference candidate up now (spec §8.2 step 2, §9.1): Tasks in the <see cref="TaskStore"/>, Rooms
/// and Team Rooms in the <see cref="ITeamDirectory"/>, Teams in the <see cref="ITeamCatalog"/> and files through
/// the Library resolver. Tasks and the Library are skipped when their feature is off.
/// </summary>
/// <param name="tasks">Where Tasks live.</param>
/// <param name="prefixes">Which Task id prefixes a Team has carried, so an invoice number is not read as a Task.</param>
/// <param name="directory">Where Rooms and their Members live.</param>
/// <param name="teams">The live Team list.</param>
/// <param name="types">The Team types, for a Team's type label.</param>
/// <param name="library">Decides whether a Library path names an existing file.</param>
/// <param name="options">Supplies the Tasks and Library feature flags.</param>
/// <param name="renames">The rename journal, so a Reference naming a renamed Team by its old name still resolves.</param>
internal sealed class ReferenceResolver(
    TaskStore tasks,
    ITaskPrefixes prefixes,
    ITeamDirectory directory,
    ITeamCatalog teams,
    TeamTypeCatalog types,
    ILibraryReferenceResolver library,
    IOptions<TeamOptions> options,
    ITeamRenames? renames = null)
{

    /// <summary>Resolves candidates in order. A Task or file candidate whose feature is disabled yields nothing, not a gone reference.</summary>
    /// <param name="candidates">What the scanner found.</param>
    /// <param name="ct">Cancels the Room and Team Room lookups.</param>
    /// <returns>One resolved or gone reference per candidate whose feature is on, in candidate order.</returns>
    public Task<IReadOnlyList<ResolvedReference>> ResolveAsync(IReadOnlyList<ReferenceCandidate> candidates, CancellationToken ct) =>
        this.ResolveCoreAsync(candidates, false, ct);

    /// <summary>
    /// Resolves the candidates a scan of a Message's text found, for the snapshot and the Agent's header. A Task id or
    /// file that does not resolve is not a reference (it may be UTF-8, or a path outside the Library), so it is dropped;
    /// a Room or Team link that names nothing stays gone. The caps of D-19 then apply to what resolved.
    /// </summary>
    /// <param name="candidates">What the scanner found.</param>
    /// <param name="ct">Cancels the Room and Team Room lookups.</param>
    /// <returns>The capped references and how many non-file references resolved before the cap.</returns>
    public async Task<ResolvedSet> ResolveCurrentAsync(IReadOnlyList<ReferenceCandidate> candidates, CancellationToken ct)
    {
        IReadOnlyList<ResolvedReference> all = await this.ResolveCoreAsync(candidates, true, ct).ConfigureAwait(false);
        int eligible = all.Count(static reference => reference is not ResolvedLibraryFile);
        return new ResolvedSet(ReferenceResolver.Cap(all), eligible);
    }

    /// <summary>Keeps the first <see cref="ReferenceLimits.MaxPerMessage"/> non-file references and the first <see cref="ReferenceLimits.MaxLibraryFiles"/> files (D-19).</summary>
    /// <param name="resolved">The resolved references in candidate order.</param>
    /// <returns>The kept references, in the same order.</returns>
    private static List<ResolvedReference> Cap(IReadOnlyList<ResolvedReference> resolved)
    {
        List<ResolvedReference> kept = [];
        int others = 0;
        int files = 0;
        foreach (ResolvedReference reference in resolved)
        {
            if (reference is ResolvedLibraryFile)
            {
                files++;
                if (files > ReferenceLimits.MaxLibraryFiles)
                {
                    continue;
                }
            }
            else
            {
                others++;
                if (others > ReferenceLimits.MaxPerMessage)
                {
                    continue;
                }
            }

            kept.Add(reference);
        }

        return kept;
    }

    /// <summary>Resolves each candidate in order, with or without dropping the Task ids and files that name nothing.</summary>
    /// <param name="candidates">What the scanner found.</param>
    /// <param name="dropUnresolvedTasksAndFiles">True to drop an unresolved Task id or file instead of yielding a gone reference.</param>
    /// <param name="ct">Cancels the Room and Team Room lookups.</param>
    /// <returns>The resolved references, in candidate order.</returns>
    private async Task<IReadOnlyList<ResolvedReference>> ResolveCoreAsync(IReadOnlyList<ReferenceCandidate> candidates, bool dropUnresolvedTasksAndFiles, CancellationToken ct)
    {
        List<ResolvedReference> resolved = [];
        foreach (ReferenceCandidate candidate in candidates)
        {
            ResolvedReference? reference = candidate.Kind switch
            {
                ReferenceKind.Task => this.ResolveTask(candidate.Key, dropUnresolvedTasksAndFiles),
                ReferenceKind.Room => await this.ResolveRoomAsync(candidate.Key, ct).ConfigureAwait(false),
                ReferenceKind.Team => await this.ResolveTeamAsync(candidate.Key, ct).ConfigureAwait(false),
                ReferenceKind.LibraryFile => this.ResolveFile(candidate.Key, dropUnresolvedTasksAndFiles),
                _ => null,
            };

            if (reference is not null)
            {
                resolved.Add(reference);
            }
        }

        return resolved;
    }

    /// <summary>Looks a Task id up; null when Tasks are off, or when it is unresolved and the drop flag is set.</summary>
    /// <param name="key">The Task id text.</param>
    /// <param name="dropMissing">True to return null for an unknown Task instead of a gone reference.</param>
    /// <returns>The resolved or gone Task, or null.</returns>
    private ResolvedReference? ResolveTask(string key, bool dropMissing)
    {
        if (!options.Value.Tasks.Enabled)
        {
            return null;
        }

        if (!TaskId.TryParse(key, out TaskId id))
        {
            return dropMissing ? null : new GoneReference(ReferenceKind.Task, key);
        }

        if ((tasks.Get(id) ?? tasks.FindArchived(id)) is not TaskItem task)
        {
            // A prefix no Team ever carried (an invoice number such as INV-2043) is not a Task reference at all.
            return dropMissing || !prefixes.IsKnown(id.Prefix) ? null : new GoneReference(ReferenceKind.Task, key);
        }

        return new ResolvedTask(task.Id, task.Title, task.Status, task.Assignee, task.Location.Team);
    }

    /// <summary>Looks a Room up by id; a missing Room is a gone reference.</summary>
    /// <param name="key">The Room id.</param>
    /// <param name="ct">Cancels the lookups.</param>
    /// <returns>The resolved or gone Room.</returns>
    private async Task<ResolvedReference> ResolveRoomAsync(string key, CancellationToken ct)
    {
        Room? room = await directory.GetRoomAsync(key, ct).ConfigureAwait(false);
        if (room is null)
        {
            return new GoneReference(ReferenceKind.Room, key);
        }

        IReadOnlyList<User> members = await directory.GetRoomMembersAsync(key, ct).ConfigureAwait(false);
        return new ResolvedRoom(room.Id, room.Name, members.Count, room.Team, room.IsTeamRoom);
    }

    /// <summary>Looks a Team up by name, with its type label and Team Room; a missing Team is a gone reference.</summary>
    /// <param name="key">The Team name.</param>
    /// <param name="ct">Cancels the Team Room lookup.</param>
    /// <returns>The resolved or gone Team.</returns>
    private async Task<ResolvedReference> ResolveTeamAsync(string key, CancellationToken ct)
    {
        TeamSummary? team = teams.Find(key);
        if (team is null && renames is not null)
        {
            string current = renames.Resolve(key);
            team = string.Equals(current, key, StringComparison.Ordinal) ? null : teams.Find(current);
        }

        if (team is null)
        {
            return new GoneReference(ReferenceKind.Team, key);
        }

        string? typeId = team.TeamType?.Id;
        string? typeLabel = typeId is null ? null : (types.Find(typeId)?.DisplayName ?? typeId);
        Room? teamRoom = await directory.FindTeamRoomAsync(team.Name, ct).ConfigureAwait(false);
        return new ResolvedTeam(team.Name, typeLabel, team.Members, teamRoom?.Id);
    }

    /// <summary>Asks the Library whether a path names an existing file; null when the Library is off, or when it does not and the drop flag is set.</summary>
    /// <param name="key">The absolute path.</param>
    /// <param name="dropMissing">True to return null for a missing file instead of a gone reference.</param>
    /// <returns>The resolved or gone file, or null.</returns>
    private ResolvedReference? ResolveFile(string key, bool dropMissing)
    {
        if (!options.Value.Library.Enabled)
        {
            return null;
        }

        LibraryReference? reference = library.ResolvePath(key);
        if (reference is not { Exists: true } && renames is not null)
        {
            string teamsRoot = Path.GetFullPath(Path.Combine(options.Value.DataDir, options.Value.Teams.Dir));
            string mapped = RenamedTeamPaths.Map(teamsRoot, key, renames);
            if (!string.Equals(mapped, key, StringComparison.Ordinal) && library.ResolvePath(mapped) is { Exists: true })
            {
                return new ResolvedLibraryFile(mapped, Path.GetFileName(mapped));
            }
        }

        if (reference is { Exists: true })
        {
            return new ResolvedLibraryFile(key, Path.GetFileName(key));
        }

        return dropMissing ? null : new GoneReference(ReferenceKind.LibraryFile, key);
    }
}
