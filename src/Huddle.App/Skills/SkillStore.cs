using System.Collections.Frozen;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Skills;

/// <summary>
/// Joins <see cref="SkillCatalog"/>'s shipped defaults with a Human's on-disk overrides under
/// <c>{DataDir}/{Acp:SkillsDir}</c>, resolving every Skill's current files and publishing them as
/// one immutable snapshot (Spec §6.2). This is the single source of truth for "which Skills exist
/// and what they say right now" — every reader (<c>read_skill</c>, Settings › Skills, the Skill
/// Index) goes through this store and never reads <see cref="SkillCatalog"/> or disk itself.
/// </summary>
/// <remarks>
/// <para>
/// <b>State model.</b> Mirrors <see cref="Agency.Huddle.App.Prompts.PromptStore"/>'s approach: the
/// fully resolved state is held as one immutable <see cref="SkillSnapshot"/> behind a single
/// <see langword="volatile"/> field, so readers take no lock — a <see langword="volatile"/> field
/// is all a single-writer/many-readers publish of an immutable object needs.
/// </para>
/// <para>
/// <b>Current scope.</b> Constructs the store, creates the Skills directory, and builds the
/// read-side snapshot (<see cref="All"/>, <see cref="Get(string)"/>, <see cref="Issues"/>,
/// <see cref="ReadFile(string, string)"/>) by merging <see cref="SkillCatalog.All"/> with whatever
/// Skill folders exist on disk, per file (Spec §6.2), including the Overridden-invalid-<c>SKILL.md</c>
/// fallback and the flat-files constraint (Spec §8.1). A <see cref="FileSystemWatcher"/> then
/// rebuilds on every filesystem change under a 500 ms debounce, raising <see cref="SkillsChanged"/>
/// once the new snapshot is published (Spec §6.2, §9, §12 F-6).
/// </para>
/// </remarks>
internal sealed class SkillStore : IDisposable
{
    // A watcher event can fire several times for one logical change (a temp-file write plus a
    // rename, several partial writes), so raising SkillsChanged straight off FileSystemWatcher would
    // thrash every observer. Coalesce a burst of events into one rebuild per pause in activity - the
    // same reasoning and the same value PromptStore.WatcherDebounceMilliseconds documents.
    private const int WatcherDebounceMilliseconds = 500;

    // FileSystemWatcher buffers events in a fixed-size kernel window and the OS drops events outright
    // - no exception, no log, nothing - when it overflows; it raises the Error event instead. See
    // PromptStore.WatcherInternalBufferSize's remarks. A Skills directory can hold several Skill
    // folders each with several files, so this is sized the same as PersonaStore's Teams watcher
    // rather than PromptStore's single-file one.
    private const int WatcherInternalBufferSize = 64 * 1024;

    // A watcher event can fire while a file is still mid-write, which surfaces here as an IOException
    // from Rebuild()'s own file reads (a sharing violation, or a directory enumerated mid-rename).
    // Retrying a few times with a short pause lets a same-process, in-place write settle before this
    // gives up, the same reasoning PromptStore.WatcherReadRetryAttempts documents for its own,
    // narrower (single-file) rebuild.
    private const int RebuildRetryAttempts = 3;
    private const int RebuildRetryDelayMilliseconds = 20;

    private readonly string skillsDir;
    private readonly ILogger<SkillStore> logger;
    private readonly Lock writeGate = new();
    private readonly FileSystemWatcher watcher;
    private readonly Timer debounceTimer;
    private bool disposed;

    // The whole resolved state - every Skill plus every Issue found building it - swapped in as one
    // immutable reference. Bundled into a single record rather than two separate fields so a reader
    // can never observe a new set of Skills paired with a stale set of Issues (or the reverse): the
    // same reasoning PromptStore's remarks give for publishing one snapshot at a time. volatile: the
    // watcher's debounced rebuild is a second writer, on a different (timer) thread, so a reader must
    // never observe a torn or reordered write - see the State model remarks above.
    private volatile SkillSnapshot snapshot;

    /// <summary>Builds and creates the Skills directory, resolves every Skill against it, and starts watching it.</summary>
    /// <param name="options">Supplies <see cref="TeamOptions.DataDir"/> and <see cref="Acp.AcpOptions.SkillsDir"/>.</param>
    /// <param name="logger">Used to report problems found while resolving Skills.</param>
    public SkillStore(IOptions<TeamOptions> options, ILogger<SkillStore> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        this.logger = logger;
        this.skillsDir = Path.Combine(options.Value.DataDir, options.Value.Acp.SkillsDir);

        // FileSystemWatcher throws when asked to watch a directory that does not exist yet, so this
        // directory is created eagerly here - the same reasoning {DataDir}/avatars/ is created
        // eagerly for AvatarStore.
        Directory.CreateDirectory(this.skillsDir);

        this.snapshot = this.Rebuild();

        this.debounceTimer = new Timer(this.OnDebounceElapsed, null, Timeout.Infinite, Timeout.Infinite);

        this.watcher = new FileSystemWatcher(this.skillsDir, "*")
        {
            IncludeSubdirectories = true,
            InternalBufferSize = WatcherInternalBufferSize,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
        };
        this.watcher.Changed += this.OnWatcherEvent;
        this.watcher.Created += this.OnWatcherEvent;
        this.watcher.Deleted += this.OnWatcherEvent;
        this.watcher.Renamed += this.OnWatcherEvent;
        this.watcher.Error += this.OnWatcherError;
        this.watcher.EnableRaisingEvents = true;
    }

    /// <summary>
    /// Raised after a filesystem change under <see cref="skillsDir"/> has settled through the
    /// debounce and a rebuilt snapshot has already been published — never before, and never from
    /// inside <see cref="writeGate"/>, so a subscriber that reads this store from inside its own
    /// handler sees the state the change just produced, and a subscriber that takes a lock of its
    /// own cannot deadlock against a concurrent rebuild.
    /// </summary>
    internal event Action? SkillsChanged;

    /// <summary>Every currently resolved Skill, ordered ordinally by <see cref="Skill.Name"/>.</summary>
    public IReadOnlyList<Skill> All => this.snapshot.All;

    /// <summary>
    /// The Skills directory this store resolves and watches - <c>{DataDir}/{Acp:SkillsDir}</c> - the
    /// same path a Skill's own <see cref="Skill.FolderPath"/> is built under. Settings › Skills reads
    /// this rather than recomputing it, the same reasoning <see cref="Skill.FolderPath"/> itself
    /// exists for: there is exactly one place that knows this path (Spec §6.7).
    /// </summary>
    internal string SkillsDirectory => this.skillsDir;

    /// <summary>Every problem found while resolving the current set of Skills.</summary>
    public IReadOnlyList<SkillIssue> Issues => this.snapshot.Issues;

    /// <summary>Looks up one Skill by name in the current snapshot.</summary>
    /// <param name="name">The Skill's name (its folder name).</param>
    /// <returns>The resolved Skill, or <see langword="null"/> when no Skill of that name exists.</returns>
    public Skill? Get(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return this.snapshot.ByName.TryGetValue(name, out Skill? skill) ? skill : null;
    }

    /// <summary>Returns one Skill's file text, or <see langword="null"/> when the Skill or the file does not exist.</summary>
    /// <remarks>
    /// <paramref name="file"/> is looked up as a literal key, ordinally, in the current snapshot's
    /// <see cref="SkillSnapshot.ResolvedFiles"/> — the only place this method reads from. It is never
    /// passed to <see cref="Path.Combine(string, string)"/>, any other <see cref="Path"/> member, or
    /// anything else that touches disk: this method performs no filesystem access at all, only a
    /// dictionary lookup against text the constructor (or a rebuild) already read. That is the whole
    /// argument behind Spec §12 F-7 — a traversal segment (<c>../../Teams/x.md</c>), a drive-rooted or
    /// absolute path, or any string that is not exactly one of the Skill's known file names simply
    /// fails the lookup and returns <see langword="null"/>. There is no path for it to escape onto,
    /// because no path is ever built from it.
    /// </remarks>
    /// <param name="skill">The Skill's name.</param>
    /// <param name="file">The file name, exactly as it appears in that Skill's <see cref="Skill.Files"/>.</param>
    /// <returns>
    /// The file's resolved text; or <see langword="null"/> when no such Skill exists, the Skill has
    /// no file of that name, or the file was dropped for failing validation (Spec §8.1 rule 7, e.g.
    /// over the 64 KB size limit) and so never survived into the resolved snapshot.
    /// </returns>
    internal string? ReadFile(string skill, string file)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skill);
        ArgumentException.ThrowIfNullOrWhiteSpace(file);

        return this.snapshot.ResolvedFiles.TryGetValue(skill, out FrozenDictionary<string, string>? files)
            && files.TryGetValue(file, out string? text)
            ? text
            : null;
    }

    /// <summary>
    /// Deletes an Overridden Skill's on-disk folder and rebuilds, so the shipped default applies
    /// again immediately (Spec §6.2, use case U10).
    /// </summary>
    /// <remarks>
    /// The folder deleted is <see cref="Skill.FolderPath"/> read from the CURRENT snapshot — never a
    /// path built from <paramref name="name"/> at the call site — the same argument
    /// <see cref="ReadFile(string, string)"/>'s remarks give for why this cannot be tricked into
    /// deleting anything outside <see cref="skillsDir"/> (Spec §6.7 Constraints). The rebuild happens
    /// synchronously, under the same lock, before this method returns, so a caller that immediately
    /// calls <see cref="Get(string)"/> afterwards already sees the Default Skill; the watcher's own
    /// debounced rebuild that follows this delete is redundant but harmless.
    /// </remarks>
    /// <param name="name">The Skill's name.</param>
    /// <exception cref="InvalidOperationException"><paramref name="name"/> names a <see cref="SkillSource.Yours"/> Skill, which has no shipped default to restore.</exception>
    internal void RestoreDefault(string name)
    {
        Action? changed;

        lock (this.writeGate)
        {
            Skill? skill = this.Get(name);
            if (skill is null || skill.FolderPath is null)
            {
                // An unknown name, or a Skill with no on-disk folder at all (already Default): both
                // have nothing to restore, so both are a no-op rather than an error.
                return;
            }

            if (skill.Source == SkillSource.Yours)
            {
                throw new InvalidOperationException(
                    $"'{name}' is a Skill you wrote, with no shipped default; there is nothing to restore it to.");
            }

            Directory.Delete(skill.FolderPath, recursive: true);
            this.snapshot = this.Rebuild();
            changed = this.SkillsChanged;
        }

        changed?.Invoke();
    }

    /// <summary>Resolves a list of Skill names against the current snapshot.</summary>
    /// <remarks>
    /// Pure over the snapshot: no lock, no rebuild, no change to any state. Reads
    /// <see cref="snapshot"/> into a local exactly once, at the top, so every name in
    /// <paramref name="names"/> is resolved against the same snapshot — reading the
    /// <see langword="volatile"/> field again per name could otherwise let a concurrent watcher
    /// rebuild publish a new snapshot midway through this call, resolving part of the list
    /// against one snapshot and the rest against another. A name repeated in
    /// <paramref name="names"/> is collapsed to its first occurrence, ordinally.
    /// </remarks>
    /// <param name="names">The Skill names to resolve, in the order a Persona lists them.</param>
    /// <returns>Known names as Skills, in first-occurrence order; each unknown name as a Warning reading <c>"Skill 'x' does not exist."</c>.</returns>
    internal SkillResolution Resolve(IReadOnlyList<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);

        SkillSnapshot current = this.snapshot;
        HashSet<string> seen = new(StringComparer.Ordinal);
        List<Skill> skills = [];
        List<string> warnings = [];

        foreach (string name in names)
        {
            if (!seen.Add(name))
            {
                continue;
            }

            if (current.ByName.TryGetValue(name, out Skill? skill))
            {
                skills.Add(skill);
            }
            else
            {
                warnings.Add($"Skill '{name}' does not exist.");
            }
        }

        return new SkillResolution(skills, warnings);
    }

    /// <summary>
    /// Merges <see cref="SkillCatalog.All"/> with whatever Skill folders currently exist on disk and
    /// builds the resolved snapshot.
    /// </summary>
    /// <remarks>
    /// A Skill's files are the default files overlaid by whatever files its disk folder carries, per
    /// file (Spec §6.2), and its <see cref="SkillSource"/> follows from whether a default and/or a
    /// disk folder exist. A Skill whose merged files fail <see cref="SkillValidator.Validate"/> is
    /// handled per Spec §8.1's last two lines: a Yours Skill (no default to fall back to) is omitted
    /// from the snapshot, its Errors kept in <see cref="Issues"/>; an Overridden Skill instead falls
    /// back to the default <c>SKILL.md</c> — keeping any disk overrides of its OTHER files — and each
    /// Error the invalid override caused is recorded as a Warning instead, naming the Skill and the
    /// original message, so a Human editing an override sees exactly why their <c>SKILL.md</c> was
    /// not used rather than the Skill silently reverting with no trace.
    /// </remarks>
    /// <returns>The newly built snapshot; does not itself publish it.</returns>
    private SkillSnapshot Rebuild()
    {
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> defaults = SkillCatalog.All;
        (Dictionary<string, Dictionary<string, string>> onDisk, List<SkillIssue> issues) = this.ScanDisk();

        HashSet<string> names = new(StringComparer.Ordinal);
        names.UnionWith(defaults.Keys);
        names.UnionWith(onDisk.Keys);

        List<Skill> all = [];
        Dictionary<string, FrozenDictionary<string, string>> resolvedFiles = new(StringComparer.Ordinal);

        foreach (string name in names.Order(StringComparer.Ordinal))
        {
            defaults.TryGetValue(name, out IReadOnlyDictionary<string, string>? defaultFiles);
            onDisk.TryGetValue(name, out Dictionary<string, string>? diskFiles);

            Dictionary<string, string> merged = MergeFiles(defaultFiles, diskFiles);
            SkillSource source = defaultFiles is not null && diskFiles is not null
                ? SkillSource.Overridden
                : defaultFiles is not null ? SkillSource.Default : SkillSource.Yours;
            string? folderPath = diskFiles is not null ? Path.Combine(this.skillsDir, name) : null;

            SkillValidation validation = SkillValidator.Validate(name, merged);

            if (validation.HasErrors && defaultFiles is not null)
            {
                foreach (SkillIssue original in validation.Issues)
                {
                    if (original.Severity == SkillIssueSeverity.Error)
                    {
                        issues.Add(new SkillIssue(
                            name,
                            $"Your SKILL.md for '{name}' was not used: {original.Message}",
                            SkillIssueSeverity.Warning));
                    }
                }

                merged = new Dictionary<string, string>(merged, StringComparer.Ordinal)
                {
                    ["SKILL.md"] = defaultFiles["SKILL.md"],
                };
                validation = SkillValidator.Validate(name, merged);
            }

            issues.AddRange(validation.Issues);

            if (validation.HasErrors || validation.Description is not string description)
            {
                // validation.Description is only null alongside an Error, so the pattern match above
                // is unreachable once HasErrors is false - it lets this branch avoid a null-forgiving
                // operator rather than adding a real second failure case. HasErrors itself can only
                // still be true here for a Yours Skill: an Overridden Skill's fallback above always
                // re-validates against the default SKILL.md, which SkillCatalogTests already pins as
                // error-free for every shipped Skill.
                continue;
            }

            all.Add(new Skill(name, description, validation.Tools, validation.Files, source, folderPath));
            resolvedFiles[name] = BuildResolvedFiles(merged, validation.Files);
        }

        FrozenDictionary<string, Skill> byName = all.ToFrozenDictionary(skill => skill.Name, StringComparer.Ordinal);

        this.LogIssues(issues);

        return new SkillSnapshot(all, byName, resolvedFiles.ToFrozenDictionary(StringComparer.Ordinal), issues);
    }

    /// <summary>Logs every Issue found while resolving Skills, at a level matching its severity.</summary>
    /// <param name="issues">The Issues found by the <see cref="Rebuild"/> that just ran.</param>
    private void LogIssues(IReadOnlyList<SkillIssue> issues)
    {
        foreach (SkillIssue issue in issues)
        {
            LogLevel level = issue.Severity switch
            {
                SkillIssueSeverity.Error => LogLevel.Error,
                SkillIssueSeverity.Warning => LogLevel.Warning,
                SkillIssueSeverity.Info => LogLevel.Information,
                _ => LogLevel.Information,
            };

            if (this.logger.IsEnabled(level))
            {
                this.logger.Log(level, "Skill '{Skill}': {Message}", issue.Skill, issue.Message);
            }
        }
    }

    /// <summary>Overlays <paramref name="diskFiles"/> onto <paramref name="defaultFiles"/>, disk winning per file name.</summary>
    /// <param name="defaultFiles">The Skill's shipped default files, or <see langword="null"/> when it has none.</param>
    /// <param name="diskFiles">The Skill's on-disk files, or <see langword="null"/> when it has no folder on disk.</param>
    /// <returns>One merged file name → text map.</returns>
    private static Dictionary<string, string> MergeFiles(
        IReadOnlyDictionary<string, string>? defaultFiles,
        IReadOnlyDictionary<string, string>? diskFiles)
    {
        Dictionary<string, string> merged = new(StringComparer.Ordinal);

        if (defaultFiles is not null)
        {
            foreach ((string fileName, string text) in defaultFiles)
            {
                merged[fileName] = text;
            }
        }

        if (diskFiles is not null)
        {
            foreach ((string fileName, string text) in diskFiles)
            {
                merged[fileName] = text;
            }
        }

        return merged;
    }

    /// <summary>
    /// Narrows <paramref name="merged"/> down to only the file names <paramref name="survivingNames"/>
    /// lists, so a file <see cref="SkillValidator.Validate"/> dropped (Spec §8.1 rule 7 — a
    /// sub-folder's contents, or a file over the 64 KB limit) is never readable through
    /// <see cref="ReadFile"/> even though its text is still sitting in <paramref name="merged"/>.
    /// </summary>
    /// <param name="merged">Every one of the Skill's merged file texts, before the rule 7 filter.</param>
    /// <param name="survivingNames"><see cref="SkillValidation.Files"/> — the file names that passed rule 7.</param>
    /// <returns>Only the surviving file name → text entries, frozen.</returns>
    private static FrozenDictionary<string, string> BuildResolvedFiles(
        Dictionary<string, string> merged,
        IReadOnlyList<string> survivingNames)
    {
        Dictionary<string, string> surviving = new(survivingNames.Count, StringComparer.Ordinal);

        foreach (string fileName in survivingNames)
        {
            if (merged.TryGetValue(fileName, out string? text))
            {
                surviving[fileName] = text;
            }
        }

        return surviving.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <summary>
    /// Scans <see cref="skillsDir"/> for Skill folders: each immediate sub-directory is a Skill name,
    /// and each <c>*.md</c> file directly inside it (no recursion into further sub-directories) is
    /// one of that Skill's files, read as UTF-8 and normalised to <c>\n</c> line endings so disk text
    /// compares equal to <see cref="SkillCatalog"/>'s embedded text. A Skill folder's own
    /// sub-directory is not one of its files at all (Spec §6.2 Constraints: "Supporting files are
    /// flat") — it and everything inside it are ignored, and one Info Issue is recorded per
    /// sub-directory found, naming it, so it does not simply vanish with no trace.
    /// </summary>
    /// <returns>Skill name → (file name → text), both levels keyed ordinally, plus every sub-directory Issue found.</returns>
    private (Dictionary<string, Dictionary<string, string>> BySkill, List<SkillIssue> Issues) ScanDisk()
    {
        Dictionary<string, Dictionary<string, string>> bySkill = new(StringComparer.Ordinal);
        List<SkillIssue> issues = [];

        foreach (string folder in Directory.EnumerateDirectories(this.skillsDir))
        {
            string skillName = Path.GetFileName(folder);
            Dictionary<string, string> files = new(StringComparer.Ordinal);

            foreach (string filePath in Directory.EnumerateFiles(folder, "*.md", SearchOption.TopDirectoryOnly))
            {
                string fileName = Path.GetFileName(filePath);
                files[fileName] = File.ReadAllText(filePath).ReplaceLineEndings("\n");
            }

            foreach (string subFolder in Directory.EnumerateDirectories(folder))
            {
                string subFolderName = Path.GetFileName(subFolder);
                issues.Add(new SkillIssue(
                    skillName,
                    $"'{subFolderName}' is a sub-folder inside '{skillName}'; Skill files are flat, so it and everything inside it are ignored.",
                    SkillIssueSeverity.Info));
            }

            bySkill[skillName] = files;
        }

        return (bySkill, issues);
    }

    // Every FileSystemWatcher event - Changed, Created, Deleted, Renamed - is handled identically:
    // (re)start the debounce timer. Which exact path or change fired is irrelevant, because Rebuild()
    // always does a full rescan of skillsDir rather than trying to apply one incremental delta.
    private void OnWatcherEvent(object sender, FileSystemEventArgs e)
    {
        lock (this.writeGate)
        {
            // Checked BEFORE logging or touching the timer - see the class remarks and
            // docs/agencyteam/known-limits.md's PersonaStore.OnWatcherError entry: logging first and
            // checking disposed second means a watcher event that fires during host teardown, after
            // this store (and possibly the logger it holds) has been disposed, crashes the test host
            // instead of harmlessly no-op'ing.
            if (this.disposed)
            {
                return;
            }

            this.debounceTimer.Change(WatcherDebounceMilliseconds, Timeout.Infinite);
        }
    }

    // FileSystemWatcher raises this instead of a normal change event when its internal buffer
    // overflows and the OS drops events - see WatcherInternalBufferSize's remarks. There is no way to
    // know which change was dropped, so the only correct response is the same one a normal change
    // takes: schedule a refresh on the existing debounce path rather than trust whatever the
    // watcher's view of the world now is.
    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        lock (this.writeGate)
        {
            // Same ordering as OnWatcherEvent, and for the same reason: disposed is checked before
            // this ever calls into the logger.
            if (this.disposed)
            {
                return;
            }

            this.logger.LogWarning(
                e.GetException(),
                "SkillStore's FileSystemWatcher reported an error (likely a dropped-event buffer overflow); scheduling a refresh.");
            this.debounceTimer.Change(WatcherDebounceMilliseconds, Timeout.Infinite);
        }
    }

    private void OnDebounceElapsed(object? state)
    {
        Action? changed;

        lock (this.writeGate)
        {
            // Checked and captured under the same lock Dispose() takes, so a Dispose() racing this
            // callback either finishes first (this returns without doing anything) or this finishes
            // its rebuild and capture before Dispose() can flip the flag - never both, and never a
            // rebuild that runs after the watcher has been torn down.
            if (this.disposed)
            {
                return;
            }

            SkillSnapshot? rebuilt = this.TryRebuildWithRetries();
            if (rebuilt is null)
            {
                // Every retry failed; TryRebuildWithRetries already logged why. The previous snapshot
                // is left exactly as it was, and there is nothing new to tell a subscriber about.
                return;
            }

            // Published BEFORE the delegate capture below: a subscriber reading this store from
            // inside its own SkillsChanged handler must see the state this rebuild just produced, not
            // whatever was true before it - the same ordering PromptStore.OnDebounceElapsed documents
            // and depends on.
            this.snapshot = rebuilt;
            changed = this.SkillsChanged;
        }

        changed?.Invoke();
    }

    /// <summary>
    /// Attempts <see cref="Rebuild"/> up to <see cref="RebuildRetryAttempts"/> times, pausing
    /// <see cref="RebuildRetryDelayMilliseconds"/> between attempts on <see cref="IOException"/> - a
    /// watcher event can fire while a file is still mid-write, which surfaces as a transient sharing
    /// violation or a directory enumerated mid-rename. If every attempt fails, this logs a Warning and
    /// returns <see langword="null"/> rather than letting the exception propagate onto the timer
    /// thread, so <see cref="OnDebounceElapsed"/> can leave the previous snapshot in place.
    /// </summary>
    /// <returns>The freshly built snapshot, or <see langword="null"/> if every attempt failed.</returns>
    private SkillSnapshot? TryRebuildWithRetries()
    {
        for (int attempt = 1; attempt <= RebuildRetryAttempts; attempt++)
        {
            try
            {
                return this.Rebuild();
            }
            catch (IOException) when (attempt < RebuildRetryAttempts)
            {
                Thread.Sleep(RebuildRetryDelayMilliseconds);
            }
            catch (IOException ex)
            {
                this.logger.LogWarning(
                    ex,
                    "Failed to rebuild Skills from '{SkillsDir}' after {Attempts} attempts; keeping the previously resolved Skills.",
                    this.skillsDir,
                    RebuildRetryAttempts);
                return null;
            }
        }

        return null;
    }

    /// <summary>Stops watching <see cref="skillsDir"/> and releases the debounce timer. Idempotent.</summary>
    public void Dispose()
    {
        lock (this.writeGate)
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
        }

        this.watcher.EnableRaisingEvents = false;
        this.watcher.Changed -= this.OnWatcherEvent;
        this.watcher.Created -= this.OnWatcherEvent;
        this.watcher.Deleted -= this.OnWatcherEvent;
        this.watcher.Renamed -= this.OnWatcherEvent;
        this.watcher.Error -= this.OnWatcherError;
        this.watcher.Dispose();
        this.debounceTimer.Dispose();
    }

    /// <summary>
    /// One immutable, fully resolved snapshot: every Skill (in both list and by-name-lookup form),
    /// every Skill's merged file texts, and every Issue found while building it. Swapped in as a
    /// whole by <see cref="snapshot"/> so readers never see one part of a rebuild without the rest.
    /// </summary>
    /// <param name="All">Every resolved Skill, already ordered ordinally by <see cref="Skill.Name"/>.</param>
    /// <param name="ByName">The same Skills, keyed by <see cref="Skill.Name"/> for <see cref="Get(string)"/>.</param>
    /// <param name="ResolvedFiles">
    /// Each Skill's resolved file name → text map, already narrowed to the files that survived
    /// validation (see <see cref="BuildResolvedFiles"/>) — what <see cref="ReadFile(string, string)"/>
    /// looks up.
    /// </param>
    /// <param name="Issues">Every problem found while resolving this snapshot's Skills.</param>
    private sealed record SkillSnapshot(
        IReadOnlyList<Skill> All,
        FrozenDictionary<string, Skill> ByName,
        FrozenDictionary<string, FrozenDictionary<string, string>> ResolvedFiles,
        IReadOnlyList<SkillIssue> Issues);
}
