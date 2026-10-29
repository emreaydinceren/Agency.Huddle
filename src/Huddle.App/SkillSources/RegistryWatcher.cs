using Agency.Huddle.SkillPacks.Checking;
using Agency.Huddle.SkillPacks.Files;
using Agency.Huddle.SkillPacks.Reading;
using Agency.Huddle.SkillPacks.Resolving;

namespace Agency.Huddle.App.SkillSources;

/// <summary>
/// Keeps <see cref="SkillRegistry"/> current (§8.5, §6.5 "RegistryWatcher"). It owns when: file watchers on the watch roots,
/// a 500 ms quiet period, and a 30-second poll for what a watcher cannot see. At start it runs the move, repairs the copier's
/// folders and does the first full read, all before any Agent starts (it is registered before them).
/// </summary>
internal sealed class RegistryWatcher : IHostedService, IDisposable
{
    /// <summary>The quiet period after the last event.</summary>
    public static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(500);

    /// <summary>The poll interval.</summary>
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    private readonly SkillRegistry registry;
    private readonly ISkillSourcesMove move;
    private readonly SkillSourcesSettings settings;
    private readonly ILogger<RegistryWatcher> logger;
    private readonly TimeProvider clock;
    private readonly Lock gate = new();
    private readonly HashSet<string> changed = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FileSystemWatcher> watchers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (bool Exists, DateTimeOffset Newest, int Count)> pollState = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> failedRoots = new(StringComparer.OrdinalIgnoreCase);
    private List<WatchRoot> polled = [];
    private ITimer? debounceTimer;
    private ITimer? pollTimer;
    private bool fullReread;
    private bool disposed;

    /// <summary>Creates the watcher; nothing is watched until <see cref="StartAsync"/>.</summary>
    public RegistryWatcher(SkillRegistry registry, ISkillSourcesMove move, SkillSourcesSettings settings, ILogger<RegistryWatcher> logger, TimeProvider? timeProvider = null)
    {
        this.registry = registry;
        this.move = move;
        this.settings = settings;
        this.logger = logger;
        this.clock = timeProvider ?? TimeProvider.System;
        this.settings.Changed += this.OnFolderChanged;
    }

    /// <summary>The roots watched now, for tests and diagnostics.</summary>
    public IReadOnlyList<string> WatchedRoots
    {
        get
        {
            lock (this.gate)
            {
                return [.. this.watchers.Keys];
            }
        }
    }

    /// <summary>The roots polled now.</summary>
    public IReadOnlyList<WatchRoot> PolledRoots
    {
        get
        {
            lock (this.gate)
            {
                return [.. this.polled];
            }
        }
    }

    /// <summary>Start-up (§6.5 "Start-up order"): create the folder, run the move, repair the copier's folders, first full
    /// read, then the watchers and the poll timer.</summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(this.settings.Dir);
        }
        catch (IOException exception)
        {
            this.logger.LogWarning(exception, "The Skill Sources folder {Folder} could not be created.", this.settings.Dir);
        }
        catch (UnauthorizedAccessException exception)
        {
            this.logger.LogWarning(exception, "The Skill Sources folder {Folder} could not be created.", this.settings.Dir);
        }

        try
        {
            await this.move.RunAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The move must never stop Huddle from starting (§6.5 step 3); it runs again at the next start.
            this.logger.LogError(exception, "The move to the Skill Sources folder failed; it runs again at the next start.");
        }

        try
        {
            SkillCopyFolders.Repair(this.settings.SkillsDir, this.logger);
        }
        catch (IOException exception)
        {
            this.logger.LogWarning(exception, "Skills/.trash could not be repaired; it is tried again at the next start.");
        }

        try
        {
            await this.registry.ReloadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException exception)
        {
            this.logger.LogWarning(exception, "The first read of the Skill registry failed; the next change repairs it.");
        }

        this.Replan();
        this.pollTimer = this.clock.CreateTimer(_ => this.Poll(), null, PollInterval, PollInterval);
        this.registry.Changed += this.OnRegistryChanged;
    }

    /// <summary>Stops the watchers and timers.</summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        this.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>Disposes every watcher and timer.</summary>
    public void Dispose()
    {
        lock (this.gate)
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            foreach (FileSystemWatcher watcher in this.watchers.Values)
            {
                watcher.Dispose();
            }

            this.watchers.Clear();
        }

        this.settings.Changed -= this.OnFolderChanged;
        this.registry.Changed -= this.OnRegistryChanged;
        this.debounceTimer?.Dispose();
        this.pollTimer?.Dispose();
    }

    /// <summary>A path changed: queue it and restart the quiet period. Internal so a test can raise an event without a real
    /// file system notification.</summary>
    internal void OnPathChanged(string path)
    {
        lock (this.gate)
        {
            if (this.disposed)
            {
                return;
            }

            this.changed.Add(path);
            this.debounceTimer ??= this.clock.CreateTimer(_ => this.Settle(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            this.debounceTimer.Change(Debounce, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>A watcher failed (buffer overflow, a removed drive): its root moves to polling and the next quiet period
    /// re-reads everything. Internal so a test can raise it.</summary>
    internal void OnWatcherError(string root)
    {
        lock (this.gate)
        {
            if (this.disposed)
            {
                return;
            }

            this.failedRoots.Add(root);
            if (this.watchers.Remove(root, out FileSystemWatcher? watcher))
            {
                watcher.Dispose();
                this.polled.Add(new WatchRoot(root, true));
            }

            this.fullReread = true;
        }

        this.logger.LogWarning("The watcher on {Root} failed; it is polled every 30 seconds instead.", root);
        this.OnPathChanged(root);
    }

    /// <summary>Runs one poll (§8.5 "Polling"): only a difference in existence, newest write time or file count triggers a
    /// rebuild. Internal so a test can poll without waiting.</summary>
    internal void Poll()
    {
        List<WatchRoot> roots;
        lock (this.gate)
        {
            if (this.disposed)
            {
                return;
            }

            roots = [.. this.polled];
        }

        foreach (WatchRoot root in roots)
        {
            (bool, DateTimeOffset, int) now = Signature(root);
            bool known;
            (bool, DateTimeOffset, int) before;
            lock (this.gate)
            {
                known = this.pollState.TryGetValue(root.Path, out before);
                this.pollState[root.Path] = now;
            }

            if (known && before != now)
            {
                lock (this.gate)
                {
                    this.fullReread = true;
                }

                this.OnPathChanged(root.Path);
            }
        }
    }

    /// <summary>Maps changed paths to a rebuild scope (§8.5 "Events").</summary>
    internal static RebuildScope ScopeFor(IReadOnlyCollection<string> paths, string skillSourcesDir, ResolvedRegistry resolved)
    {
        List<string> files = [];
        List<string> folders = [];
        string registryFile = Path.Combine(skillSourcesDir, RegistryReader.FileName);
        foreach (string path in paths)
        {
            if (string.Equals(path, registryFile, StringComparison.OrdinalIgnoreCase))
            {
                return RebuildScope.All;
            }

            if (path.EndsWith(PackFile.Suffix, StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(CatalogFile.Suffix, StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(SourceFile.FileName, StringComparison.OrdinalIgnoreCase)
                || path.EndsWith("NOTES.md", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith("hidden.json", StringComparison.OrdinalIgnoreCase))
            {
                files.Add(path);
                continue;
            }

            SkillEntry? skill = resolved.Skills.FirstOrDefault(skill => skill.FolderPath is not null && Checks.IsUnder(path, skill.FolderPath));
            if (skill?.FolderPath is { } folder)
            {
                folders.Add(folder);
                continue;
            }

            if (path.EndsWith("log.jsonl", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return RebuildScope.All;
        }

        return new RebuildScope(false, files, folders);
    }

    /// <summary>The quiet period ended: rebuild for what changed.</summary>
    private void Settle()
    {
        RebuildScope scope;
        lock (this.gate)
        {
            if (this.disposed || this.changed.Count == 0)
            {
                return;
            }

            scope = this.fullReread ? RebuildScope.All : ScopeFor([.. this.changed], this.settings.Dir, this.registry.Current.Resolved);
            this.changed.Clear();
            this.fullReread = false;
        }

        this.registry.RebuildInBackground(scope);
    }

    /// <summary>After a publish: rebuild the watchers when the resolved paths changed.</summary>
    private void OnRegistryChanged(RegistryChange change)
    {
        this.Replan();
    }

    /// <summary>The Skill Sources folder changed on the Sources tab: rebuild the watchers, then a full read.</summary>
    private void OnFolderChanged()
    {
        try
        {
            Directory.CreateDirectory(this.settings.Dir);
        }
        catch (IOException exception)
        {
            this.logger.LogWarning(exception, "The Skill Sources folder {Folder} could not be created.", this.settings.Dir);
        }

        this.registry.RebuildInBackground(RebuildScope.All);
    }

    /// <summary>Creates and disposes watchers to match the current registry.</summary>
    private void Replan()
    {
        WatchPlan plan = WatchRoots.Plan(this.settings.Dir, this.registry.Current.Resolved, Directory.Exists, IsNetwork);
        lock (this.gate)
        {
            if (this.disposed)
            {
                return;
            }

            HashSet<string> wanted = new(plan.Watched.Select(static root => root.Path).Where(path => !this.failedRoots.Contains(path)), StringComparer.OrdinalIgnoreCase);
            foreach (string stale in this.watchers.Keys.Where(key => !wanted.Contains(key)).ToList())
            {
                this.watchers[stale].Dispose();
                this.watchers.Remove(stale);
            }

            foreach (WatchRoot root in plan.Watched.Where(root => wanted.Contains(root.Path) && !this.watchers.ContainsKey(root.Path)))
            {
                try
                {
                    this.watchers[root.Path] = this.CreateWatcher(root);
                }
                catch (ArgumentException exception)
                {
                    this.logger.LogWarning(exception, "Cannot watch {Root}; it is polled instead.", root.Path);
                }
                catch (IOException exception)
                {
                    this.logger.LogWarning(exception, "Cannot watch {Root}; it is polled instead.", root.Path);
                }
            }

            this.polled = [.. plan.Polled, .. plan.Watched.Where(root => !this.watchers.ContainsKey(root.Path))];
        }
    }

    /// <summary>A file system watcher on one root.</summary>
    private FileSystemWatcher CreateWatcher(WatchRoot root)
    {
        FileSystemWatcher watcher = new(root.Path)
        {
            IncludeSubdirectories = root.Recursive,
            InternalBufferSize = 64 * 1024,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size,
        };
        watcher.Changed += (_, e) => this.OnPathChanged(e.FullPath);
        watcher.Created += (_, e) => this.OnPathChanged(e.FullPath);
        watcher.Deleted += (_, e) => this.OnPathChanged(e.FullPath);
        watcher.Renamed += (_, e) =>
        {
            this.OnPathChanged(e.OldFullPath);
            this.OnPathChanged(e.FullPath);
        };
        watcher.Error += (_, _) => this.OnWatcherError(root.Path);
        watcher.EnableRaisingEvents = true;
        return watcher;
    }

    /// <summary>Existence, newest write time and file count of a root.</summary>
    private static (bool Exists, DateTimeOffset Newest, int Count) Signature(WatchRoot root)
    {
        try
        {
            if (!Directory.Exists(root.Path))
            {
                return (false, DateTimeOffset.MinValue, 0);
            }

            string[] files = Directory.GetFiles(root.Path, "*", new EnumerationOptions { RecurseSubdirectories = root.Recursive, IgnoreInaccessible = true });
            DateTimeOffset newest = files.Length == 0 ? DateTimeOffset.MinValue : files.Max(static file => (DateTimeOffset)File.GetLastWriteTimeUtc(file));
            return (true, newest, files.Length);
        }
        catch (IOException)
        {
            return (false, DateTimeOffset.MinValue, 0);
        }
        catch (UnauthorizedAccessException)
        {
            return (false, DateTimeOffset.MinValue, 0);
        }
    }

    /// <summary>Whether a path is on a network drive, where file watchers miss events.</summary>
    private static bool IsNetwork(string path)
    {
        try
        {
            string? root = Path.GetPathRoot(path);
            return root is not null && root.StartsWith(@"\\", StringComparison.Ordinal) || (root is { Length: > 0 } drive && new DriveInfo(drive).DriveType == DriveType.Network);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
