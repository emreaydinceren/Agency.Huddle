using Microsoft.Extensions.Options;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;

namespace Agency.Huddle.App.FileChanges;

/// <summary>
/// The start scan of every Watched Folder for one Turn, carried from <see cref="FileChangeTracker.CollectAsync"/>
/// to <see cref="FileChangeTracker.CommitAsync"/> so a Turn scans each folder once, not twice, per FC §6.7.
/// </summary>
/// <param name="Report">The File Changes block for this Turn, already capped.</param>
/// <param name="Folders">
/// Every resolved Watched Folder, in order: own Work Dir, then declared, then subscribed. A plan
/// addition beyond FC §6.7's own listing: the commit needs each entry's full path.
/// </param>
/// <param name="Scans">Each folder's start scan, keyed by entry, compared <see cref="StringComparer.OrdinalIgnoreCase"/>.</param>
internal sealed record CollectedChanges(
    FileChangesReport Report,
    IReadOnlyList<WatchedFolder> Folders,
    IReadOnlyDictionary<string, ScanResult> Scans);

/// <summary>
/// Collects and commits one Agent's File Changes per Room, per FC §6.7. A singleton, like
/// <see cref="Agency.Huddle.App.Acp.RoomFollows"/>: the <c>watch_folder</c>/<c>unwatch_folder</c>
/// tools and every runner share it through DI.
/// </summary>
internal sealed class FileChangeTracker(
    FileStateStore store,
    PersonaStore personas,
    ITeamDirectory directory,
    WatchedFolderResolver resolver,
    IOptions<TeamOptions> options,
    ILogger<FileChangeTracker> logger)
{
    /// <summary>
    /// Turn start. Scans every Watched Folder and compares each with this Room's baseline. Saves
    /// nothing. With no baseline for <paramref name="roomId"/> yet, the report is empty (F7): this
    /// is the Agent's first Turn here.
    /// </summary>
    /// <param name="agentName">The Agent's Name, whose own Work Dir is always the first Watched Folder.</param>
    /// <param name="roomId">The Room this Turn runs in.</param>
    /// <param name="declared">The Persona's <c>watches</c> frontmatter entries, read by the caller.</param>
    /// <param name="cancellationToken">Cancels the scan.</param>
    internal async Task<CollectedChanges> CollectAsync(
        string agentName,
        string roomId,
        IReadOnlyList<string> declared,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        ArgumentNullException.ThrowIfNull(declared);

        IReadOnlyList<WatchedFolder> folders = this.ResolveFolders(agentName, declared);

        Dictionary<string, ScanResult> scans = new(StringComparer.OrdinalIgnoreCase);
        foreach (WatchedFolder folder in folders)
        {
            ScanResult scan = await Task.Run(() => FolderScanner.Scan(folder.FullPath, options.Value.FileChanges), cancellationToken).ConfigureAwait(false);
            scans[folder.Entry] = scan;
        }

        FileState? state = store.Load(agentName);
        if (state is null || !state.Rooms.TryGetValue(roomId, out RoomBaseline? roomBaseline))
        {
            return new CollectedChanges(FileChangesReport.Empty, folders, scans);
        }

        List<FileChange> changes = [];
        List<string> uncheckedFolders = [];

        foreach (WatchedFolder folder in folders)
        {
            ScanResult scan = scans[folder.Entry];

            if (scan.Outcome == ScanOutcome.TooLarge)
            {
                uncheckedFolders.Add(folder.FullPath);
                continue;
            }

            if (roomBaseline.Folders.TryGetValue(folder.Entry, out FolderSnapshot? previousSnapshot))
            {
                changes.AddRange(FileStateDiff.Compare(previousSnapshot, scan.Snapshot, folder.FullPath));
            }
        }

        int maxListed = options.Value.FileChanges.MaxListed;
        List<FileChange> listed = changes.Count > maxListed ? changes[..maxListed] : changes;
        int notListed = changes.Count - listed.Count;

        FileChangesReport report = new(listed, notListed, uncheckedFolders, options.Value.FileChanges.MaxFilesPerFolder);
        return new CollectedChanges(report, folders, scans);
    }

    /// <summary>
    /// Turn end. Saves this Room's baseline: the start scan carried in <paramref name="collected"/>,
    /// plus the current state of <paramref name="touched"/> paths, per FC §6.7. Every other Room's
    /// baseline is left as it was, except a Room <see cref="ITeamDirectory.GetRoomAsync"/> no longer
    /// finds, whose baseline is dropped (E-1c).
    /// </summary>
    /// <param name="agentName">The Agent's Name.</param>
    /// <param name="roomId">The Room this Turn ran in.</param>
    /// <param name="collected">This Turn's <see cref="CollectAsync"/> result.</param>
    /// <param name="touched">Paths this Agent's own tool calls touched this Turn.</param>
    /// <param name="cancellationToken">Cancels the Room-existence checks.</param>
    internal async Task CommitAsync(
        string agentName,
        string roomId,
        CollectedChanges collected,
        IReadOnlyCollection<string> touched,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        ArgumentNullException.ThrowIfNull(collected);
        ArgumentNullException.ThrowIfNull(touched);

        FileState? current = store.Load(agentName);

        HashSet<string> deletedRoomIds = [];
        if (current is not null)
        {
            foreach (string otherRoomId in current.Rooms.Keys)
            {
                if (string.Equals(otherRoomId, roomId, StringComparison.Ordinal))
                {
                    continue;
                }

                Room? room = await directory.GetRoomAsync(otherRoomId, cancellationToken).ConfigureAwait(false);
                if (room is null)
                {
                    deletedRoomIds.Add(otherRoomId);
                }
            }
        }

        store.Update(agentName, state => BuildNewState(state, roomId, collected, touched, deletedRoomIds));
    }

    /// <summary>
    /// <c>watch_folder</c>. Resolves <paramref name="entry"/> and, unless it is already watched
    /// (own Work Dir, a Persona frontmatter entry, or already subscribed under any spelling),
    /// saves it to <paramref name="agentName"/>'s <c>subscribed</c> list, per FC §6.9.
    /// </summary>
    /// <param name="agentName">The Agent's Name.</param>
    /// <param name="entry">The folder entry as given to the tool.</param>
    /// <returns>The tool's result text.</returns>
    internal string Subscribe(string agentName, string entry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry);

        if (!resolver.TryResolve(entry, personas.ListNames(), out WatchedFolder? folder, out string? reason))
        {
            return reason;
        }

        if (this.IsOwnWorkDir(agentName, folder.FullPath))
        {
            return $"Already watching '{entry}' ({folder.FullPath}). Nothing to do.";
        }

        if (this.TryFindDeclaredEntry(agentName, folder.FullPath, out string? declaredEntry))
        {
            return $"Already watching '{declaredEntry}' ({folder.FullPath}). Nothing to do.";
        }

        string result = $"Now watching '{entry}' ({folder.FullPath}). From your next Turn, files added, changed or deleted there are listed at the top of your prompt. This lasts until you call unwatch_folder, including after a restart.";
        store.Update(agentName, state =>
        {
            FileState baseline = state ?? FileState.Empty;

            foreach (string existing in baseline.Subscribed)
            {
                if (resolver.TryResolve(existing, personas.ListNames(), out WatchedFolder? existingFolder, out _)
                    && string.Equals(existingFolder.FullPath, folder.FullPath, StringComparison.OrdinalIgnoreCase))
                {
                    result = $"Already watching '{existing}' ({folder.FullPath}). Nothing to do.";
                    return baseline;
                }
            }

            List<string> subscribed = [.. baseline.Subscribed, entry];
            return baseline with { Subscribed = subscribed };
        });

        return result;
    }

    /// <summary>
    /// <c>unwatch_folder</c>. Removes <paramref name="entry"/> from <paramref name="agentName"/>'s
    /// <c>subscribed</c> list and its snapshot from every Room, per FC §6.9.
    /// </summary>
    /// <param name="agentName">The Agent's Name.</param>
    /// <param name="entry">The folder entry as given to the tool.</param>
    /// <returns>The tool's result text.</returns>
    internal string Unsubscribe(string agentName, string entry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry);

        if (!resolver.TryResolve(entry, personas.ListNames(), out WatchedFolder? folder, out string? reason))
        {
            return reason;
        }

        if (this.IsOwnWorkDir(agentName, folder.FullPath))
        {
            return "Your own folder is always watched.";
        }

        if (this.TryFindDeclaredEntry(agentName, folder.FullPath, out string? declaredEntry))
        {
            return $"'{declaredEntry}' is watched because your Persona lists it. Only the Human can change that.";
        }

        string result = $"You are not watching '{entry}'.";
        store.Update(agentName, state =>
        {
            if (state is null)
            {
                return FileState.Empty;
            }

            string? matched = null;
            foreach (string existing in state.Subscribed)
            {
                if (resolver.TryResolve(existing, personas.ListNames(), out WatchedFolder? existingFolder, out _)
                    && string.Equals(existingFolder.FullPath, folder.FullPath, StringComparison.OrdinalIgnoreCase))
                {
                    matched = existing;
                    break;
                }
            }

            if (matched is null)
            {
                return state;
            }

            result = $"Stopped watching '{matched}'.";
            List<string> subscribed = [.. state.Subscribed.Where(item => !string.Equals(item, matched, StringComparison.Ordinal))];

            Dictionary<string, RoomBaseline> rooms = new(StringComparer.Ordinal);
            foreach ((string roomId, RoomBaseline roomBaseline) in state.Rooms)
            {
                Dictionary<string, FolderSnapshot> roomFolders = new(StringComparer.OrdinalIgnoreCase);
                foreach ((string folderEntry, FolderSnapshot snapshot) in roomBaseline.Folders)
                {
                    if (!string.Equals(folderEntry, matched, StringComparison.OrdinalIgnoreCase))
                    {
                        roomFolders[folderEntry] = snapshot;
                    }
                }

                rooms[roomId] = new RoomBaseline(roomFolders);
            }

            return state with { Subscribed = subscribed, Rooms = rooms };
        });

        return result;
    }

    /// <summary>
    /// Checks Persona <c>watches</c> entries at startup for <see cref="Agency.Huddle.App.Acp.PersonaSupervisor"/>'s
    /// Degraded report, per FC §6.10. The message is the same fixed text for every unresolvable
    /// entry, regardless of the resolver's own specific reason, and also fires for a bare word that
    /// resolves cleanly but is almost certainly a mistyped Teammate Name - see
    /// <see cref="IsLikelyMistypedTeammateName"/>. Either way the entry is still watched (E-2: its
    /// folder, or the Teammate meant, may exist later); this only ever adds a warning.
    /// </summary>
    /// <param name="declared">The Persona's <c>watches</c> frontmatter entries.</param>
    /// <returns>One warning per entry that does not resolve, or looks like a typo.</returns>
    internal IReadOnlyList<string> CheckDeclared(IReadOnlyList<string> declared)
    {
        ArgumentNullException.ThrowIfNull(declared);

        List<string> warnings = [];
        string dataDirName = Path.GetFileName(options.Value.DataDir);
        IReadOnlyCollection<string> teammateNames = personas.ListNames();

        foreach (string entry in declared)
        {
            if (!resolver.TryResolve(entry, teammateNames, out WatchedFolder? folder, out _)
                || IsLikelyMistypedTeammateName(entry, folder, teammateNames))
            {
                warnings.Add($"Watched folder '{entry}' is not a Teammate or a folder inside {dataDirName}.");
            }
        }

        return warnings;
    }

    /// <summary>
    /// Whether <paramref name="entry"/> resolved cleanly only because FC §6.3's own fallback rule
    /// treats a bare word with no other meaning as relative to <c>DataDir</c> - so a mistyped
    /// Teammate Name such as <c>Nope</c> resolves to <c>{DataDir}/Nope</c> and would otherwise never
    /// warn, even though FC §6.10's own example is exactly this case. Multi-segment entries
    /// (<c>Shared/missing</c>) and explicitly relative ones (<c>./missing</c>) are left alone: those
    /// name a real path someone intends to create, which is not what this checks for (E-2).
    /// </summary>
    /// <param name="entry">The entry as written in frontmatter.</param>
    /// <param name="folder">What <paramref name="entry"/> resolved to.</param>
    /// <param name="teammateNames">Every Teammate's Name, so a genuine match is never flagged.</param>
    private static bool IsLikelyMistypedTeammateName(string entry, WatchedFolder folder, IReadOnlyCollection<string> teammateNames)
    {
        if (entry.Contains('/', StringComparison.Ordinal) || entry.Contains('\\', StringComparison.Ordinal))
        {
            return false;
        }

        if (entry.StartsWith("./", StringComparison.Ordinal) || entry.StartsWith(".\\", StringComparison.Ordinal))
        {
            return false;
        }

        if (Path.IsPathFullyQualified(entry))
        {
            return false;
        }

        if (teammateNames.Any(name => string.Equals(name, entry, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return !Directory.Exists(folder.FullPath);
    }

    /// <summary>Whether <paramref name="fullPath"/> is <paramref name="agentName"/>'s own Work Dir.</summary>
    private bool IsOwnWorkDir(string agentName, string fullPath)
    {
        string ownFullPath = Path.Combine(options.Value.DataDir, options.Value.Acp.WorkDir, agentName);
        return string.Equals(ownFullPath, fullPath, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Finds the declared <c>watches</c> entry, if any, resolving to <paramref name="fullPath"/>.</summary>
    private bool TryFindDeclaredEntry(string agentName, string fullPath, out string? declaredEntry)
    {
        declaredEntry = null;
        Persona? persona = personas.Get(agentName);
        if (persona is null
            || !PersonaFrontmatter.TryReadIdentity(persona.Text, out PersonaIdentity? identity, out _)
            || identity.Watches is not { Count: > 0 } watches)
        {
            return false;
        }

        foreach (string entry in watches)
        {
            if (resolver.TryResolve(entry, personas.ListNames(), out WatchedFolder? folder, out _)
                && string.Equals(folder.FullPath, fullPath, StringComparison.OrdinalIgnoreCase))
            {
                declaredEntry = entry;
                return true;
            }
        }

        return false;
    }

    /// <summary>Resolves this Agent's Watched Folders: own Work Dir, then declared, then subscribed, deduplicated by full path (finding kept the first entry).</summary>
    private List<WatchedFolder> ResolveFolders(string agentName, IReadOnlyList<string> declared)
    {
        List<WatchedFolder> folders = [];
        HashSet<string> seenFullPaths = new(StringComparer.OrdinalIgnoreCase);

        WatchedFolder own = new(agentName, Path.Combine(options.Value.DataDir, options.Value.Acp.WorkDir, agentName));
        folders.Add(own);
        seenFullPaths.Add(own.FullPath);

        IReadOnlyList<string> subscribed = store.Load(agentName)?.Subscribed ?? [];
        foreach (string entry in declared.Concat(subscribed))
        {
            if (!resolver.TryResolve(entry, personas.ListNames(), out WatchedFolder? folder, out string? reason))
            {
                logger.LogWarning("Watched Folder entry '{Entry}' for Agent '{AgentName}' could not be resolved: {Reason}", entry, agentName, reason);
                continue;
            }

            if (seenFullPaths.Add(folder.FullPath))
            {
                folders.Add(folder);
            }
        }

        return folders;
    }

    /// <summary>Builds this Agent's new saved state for <see cref="CommitAsync"/>, run inside <see cref="FileStateStore.Update"/>.</summary>
    private static FileState BuildNewState(
        FileState? state,
        string roomId,
        CollectedChanges collected,
        IReadOnlyCollection<string> touched,
        IReadOnlyCollection<string> deletedRoomIds)
    {
        FileState baseline = state ?? FileState.Empty;

        Dictionary<string, FolderSnapshot> newFolders = new(StringComparer.OrdinalIgnoreCase);
        baseline.Rooms.TryGetValue(roomId, out RoomBaseline? previousRoomBaseline);

        foreach (WatchedFolder folder in collected.Folders)
        {
            ScanResult scan = collected.Scans[folder.Entry];

            if (scan.Outcome == ScanOutcome.TooLarge)
            {
                if (previousRoomBaseline is not null && previousRoomBaseline.Folders.TryGetValue(folder.Entry, out FolderSnapshot? previousSnapshot))
                {
                    newFolders[folder.Entry] = previousSnapshot;
                }

                continue;
            }

            Dictionary<string, FileEntry> files = new(scan.Snapshot.Files, FolderSnapshot.PathComparer);
            string prefix = folder.FullPath + Path.DirectorySeparatorChar;

            foreach (string touchedPath in touched)
            {
                if (!touchedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string relativePath = Path.GetRelativePath(folder.FullPath, touchedPath);
                if (File.Exists(touchedPath))
                {
                    FileInfo info = new(touchedPath);
                    files[relativePath] = new FileEntry(info.Length, new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero));
                }
                else
                {
                    files.Remove(relativePath);
                }
            }

            newFolders[folder.Entry] = new FolderSnapshot(files);
        }

        Dictionary<string, RoomBaseline> newRooms = new(StringComparer.Ordinal);
        foreach ((string existingRoomId, RoomBaseline existingBaseline) in baseline.Rooms)
        {
            if (string.Equals(existingRoomId, roomId, StringComparison.Ordinal) || deletedRoomIds.Contains(existingRoomId))
            {
                continue;
            }

            newRooms[existingRoomId] = existingBaseline;
        }

        newRooms[roomId] = new RoomBaseline(newFolders);

        return baseline with { Rooms = newRooms };
    }
}
