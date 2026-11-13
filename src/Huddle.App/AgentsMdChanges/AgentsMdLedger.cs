using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Agency.Huddle.App.Helpers;
using Agency.Huddle.App.Library;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.AgentsMdChanges;

/// <summary>The last-seen version of each AGENTS.md and the chain of edits since (AgentsMdChanges Spec §6.2). A singleton.</summary>
/// <remarks>
/// A file is read and hashed outside <c>gate</c>; the compare-and-set runs inside it; the ledger file is written outside
/// it. Writes never reorder: every write takes <c>writeGate</c>, and takes its snapshot of the entry only after holding
/// it, so whichever write runs last carries the newest state.
/// </remarks>
internal sealed class AgentsMdLedger
{
    private readonly Lock gate = new();
    private readonly Lock writeGate = new();
    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
    private readonly IOptions<TeamOptions> options;
    private readonly ILogger<AgentsMdLedger> logger;
    private readonly TimeProvider timeProvider;

    /// <summary>Creates the ledger.</summary>
    /// <param name="dispatcher">Starts judgements.</param>
    /// <param name="helper">The AGENTS.md Helper.</param>
    /// <param name="settings">The Helper's settings.</param>
    /// <param name="budget">The Helper's daily cap.</param>
    /// <param name="options">Where the data directory and the ledger limits are read.</param>
    /// <param name="logger">Receives warnings.</param>
    /// <param name="timeProvider">The clock; <see cref="TimeProvider.System"/> when <see langword="null"/>.</param>
    public AgentsMdLedger(
        Func<HelperDispatcher> dispatcher,
        Func<IHelperAgent> helper,
        HelperSettingsResolver settings,
        HelperBudget budget,
        IOptions<TeamOptions> options,
        ILogger<AgentsMdLedger> logger,
        TimeProvider? timeProvider = null)
    {
        this.Dispatcher = dispatcher;
        this.Helper = helper;
        this.Settings = settings;
        this.Budget = budget;
        this.options = options;
        this.logger = logger;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Starts judgements.</summary>
    internal Func<HelperDispatcher> Dispatcher { get; }

    /// <summary>The AGENTS.md Helper.</summary>
    internal Func<IHelperAgent> Helper { get; }

    /// <summary>The Helper's settings.</summary>
    internal HelperSettingsResolver Settings { get; }

    /// <summary>The Helper's daily cap.</summary>
    internal HelperBudget Budget { get; }

    /// <summary>
    /// Re-keys every entry whose file lies under <paramref name="oldRelative"/> to the same path under <paramref name="newRelative"/>,
    /// in the ledger files and in memory. Paths compare as the file system does (<see cref="PathContainment.Comparison"/>), as
    /// <see cref="KeyFor"/> does: where case is folded, a case-only change keeps every key and moves nothing; where it is not,
    /// a case-only change is a real rename and re-keys the entries.
    /// </summary>
    /// <param name="oldRelative">The old folder, relative to the Teams root, with <c>/</c> separators.</param>
    /// <param name="newRelative">The new folder.</param>
    internal void MovePrefix(string oldRelative, string newRelative)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(oldRelative);
        ArgumentException.ThrowIfNullOrWhiteSpace(newRelative);

        if (string.Equals(oldRelative, newRelative, PathContainment.Comparison) || !Directory.Exists(this.Folder))
        {
            return;
        }

        string prefix = oldRelative.TrimEnd('/') + "/";
        lock (this.writeGate)
        {
            foreach (string file in Directory.EnumerateFiles(this.Folder, "*.json").ToList())
            {
                this.MoveOne(file, prefix, newRelative.TrimEnd('/') + "/");
            }
        }
    }

    private void MoveOne(string file, string prefix, string newPrefix)
    {
        try
        {
            AgentsMdLedgerDocument? document = JsonSerializer.Deserialize(File.ReadAllBytes(file), AgentsMdLedgerJson.Default.AgentsMdLedgerDocument);
            if (document?.RelativePath is not { } relative || !relative.StartsWith(prefix, PathContainment.Comparison))
            {
                return;
            }

            string moved = newPrefix + relative[prefix.Length..];
            string oldKey = Path.GetFileNameWithoutExtension(file);
            if (string.Equals(KeyFor(moved), oldKey, StringComparison.OrdinalIgnoreCase))
            {
                // Same key: writing the copy and then deleting the file would delete the entry itself.
                return;
            }

            AgentsMdLedgerDocument copy = new()
            {
                RelativePath = moved,
                FirstSightHash = document.FirstSightHash,
                HeadHash = document.HeadHash,
                HeadText = document.HeadText,
                Revisions = document.Revisions,
            };
            AtomicFile.WriteBytes(Path.Combine(this.Folder, KeyFor(moved) + ".json"), JsonSerializer.SerializeToUtf8Bytes(copy, AgentsMdLedgerJson.Default.AgentsMdLedgerDocument));
            File.Delete(file);
            lock (this.gate)
            {
                _ = this.entries.Remove(oldKey);
                _ = this.entries.Remove(KeyFor(moved));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            this.logger.LogWarning(ex, "Could not move the ledger file '{Path}'; it keeps its old key.", file);
        }
    }

    private string Folder =>Path.Combine(this.options.Value.DataDir, "agents-md-ledger");

    /// <summary>Compares the file's current body with the head, records a first sight, and moves the pending marker. Never queues a judgement.</summary>
    /// <param name="file">The file to observe.</param>
    public AgentsMdObservation Observe(AgentsMdFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        string? raw = this.TryRead(file);
        string key = KeyFor(file.RelativePath);
        if (raw is null || !this.TryEnsureLoaded(key))
        {
            return new AgentsMdObservation(file.RelativePath, string.Empty, FirstSight: false, Pending: false);
        }

        AgentsMdBody body = AgentsMdBody.Parse(raw);

        bool firstSight = false;
        bool pending;
        lock (this.gate)
        {
            if (!this.entries.TryGetValue(key, out Entry? entry))
            {
                this.entries[key] = this.FirstSightEntry(file, body);
                firstSight = true;
                pending = false;
            }
            else
            {
                pending = this.Apply(entry, file, body);
            }
        }

        if (firstSight)
        {
            this.Persist(key);
        }

        return new AgentsMdObservation(file.RelativePath, body.Hash, firstSight, pending);
    }

    /// <summary>The pending edits and when each last changed, for the watcher's debounce.</summary>
    public IReadOnlyList<AgentsMdPendingEdit> PendingEdits()
    {
        lock (this.gate)
        {
            List<AgentsMdPendingEdit> pending = [];
            foreach (Entry entry in this.entries.Values)
            {
                if (entry.PendingFile is not null && entry.PendingHash is not null)
                {
                    pending.Add(new AgentsMdPendingEdit(entry.PendingFile, entry.PendingHash, entry.PendingChangedAt));
                }
            }

            return pending;
        }
    }

    /// <summary>
    /// When the judgement of this file's current text started (in memory only); null when none has, when the file has a newer
    /// text since, and after the judgement was committed.
    /// </summary>
    /// <param name="relativePath">The file.</param>
    public DateTimeOffset? JudgementStarted(string relativePath)
    {
        string key = KeyFor(relativePath);
        this.EnsureLoaded(key);
        lock (this.gate)
        {
            return this.entries.TryGetValue(key, out Entry? entry) && string.Equals(entry.JudgedHash, entry.PendingHash, StringComparison.Ordinal)
                ? entry.JudgementStartedAt
                : null;
        }
    }

    /// <summary>
    /// Starts a judgement of the file's pending edit when Spec §8.6 allows. The checks run in the order of the §8.6
    /// pseudo-code: debounce, absent or oversize body (settled now, no call), same pair running or done, spacing, Helper or
    /// Adapters off, paused Helper (queued so the run reaches OverBudget, but false), then Enqueue outside the lock.
    /// </summary>
    /// <param name="file">The file with a pending edit.</param>
    /// <param name="reason">Whether the watcher's debounce or a Turn asks.</param>
    /// <returns>True when a judgement is running for the pair, so a Turn may wait for it.</returns>
    public bool TryStartJudgement(AgentsMdFile file, JudgementReason reason)
    {
        ArgumentNullException.ThrowIfNull(file);
        string key = KeyFor(file.RelativePath);
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        AgentsMdOptions limits = this.options.Value.AgentsMd;
        string? raw = this.TryEnsureLoaded(key) ? this.TryRead(file) : null;
        if (raw is null)
        {
            return false;
        }

        AgentsMdBody body = AgentsMdBody.Parse(raw);
        if (!this.IsPastDebounce(key, file, body, reason, now, limits))
        {
            return false;
        }

        if (this.SettlesWithoutCall(key, body, limits))
        {
            return false;
        }

        IHelperAgent helper = this.Helper();
        HelperSettings helperSettings = this.Settings.For(helper);
        bool paused = this.Budget.IsPaused(helper.Id, helperSettings.DailyCap);
        AgentsMdChangeInput? input = this.Reserve(file, key, body, now, helperSettings, paused, out bool alreadyRunning);
        if (alreadyRunning)
        {
            return true;
        }

        if (input is null)
        {
            return false;
        }

        return this.EnqueueOutsideLock(key, helper, input) && !paused;
    }

    /// <summary>
    /// The §8.6 checks from "same pair" to "Helper off" under the lock, in order. Returns the input to queue, or null when
    /// the answer is already known; <paramref name="alreadyRunning"/> is true when the pair is running. A paused Helper
    /// gets its input but no reservation.
    /// </summary>
    private AgentsMdChangeInput? Reserve(AgentsMdFile file, string key, AgentsMdBody body, DateTimeOffset now, HelperSettings helperSettings, bool paused, out bool alreadyRunning)
    {
        alreadyRunning = false;
        AgentsMdOptions limits = this.options.Value.AgentsMd;
        lock (this.gate)
        {
            if (!this.entries.TryGetValue(key, out Entry? entry) || string.Equals(body.Hash, entry.HeadHash, StringComparison.Ordinal))
            {
                return null;
            }

            if (string.Equals(entry.RunningFrom, entry.HeadHash, StringComparison.Ordinal) && string.Equals(entry.RunningTo, body.Hash, StringComparison.Ordinal))
            {
                alreadyRunning = true;
                return null;
            }

            bool done = entry.Revisions.Exists(revision => string.Equals(revision.FromHash, entry.HeadHash, StringComparison.Ordinal)
                && string.Equals(revision.ToHash, body.Hash, StringComparison.Ordinal));
            bool spaced = entry.LastJudgement is { } last && (now - last).TotalSeconds < limits.MinJudgeSeconds;
            if (done || spaced || !helperSettings.Enabled || !this.options.Value.Acp.Enabled)
            {
                return null;
            }

            entry.LastJudgement = now;
            entry.JudgedHash = body.Hash;
            if (!paused)
            {
                entry.JudgementStartedAt = now;
                entry.RunningFrom = entry.HeadHash;
                entry.RunningTo = body.Hash;
            }

            return BuildInput(file, entry, body);
        }
    }

    /// <summary>
    /// True unless nothing is pending or a Debounced ask comes before the file has been unchanged for the debounce. A text on
    /// disk that differs from the pending one is a save nobody observed yet: it is recorded as a new save now, so a Debounced
    /// ask restarts its debounce, while a Turn goes on with the disk's text.
    /// </summary>
    private bool IsPastDebounce(string key, AgentsMdFile file, AgentsMdBody body, JudgementReason reason, DateTimeOffset now, AgentsMdOptions limits)
    {
        lock (this.gate)
        {
            if (!this.entries.TryGetValue(key, out Entry? entry) || entry.PendingHash is null)
            {
                return false;
            }

            bool unobserved = !string.Equals(body.Hash, entry.PendingHash, StringComparison.Ordinal);
            if (!this.Apply(entry, file, body))
            {
                return false;
            }

            if (reason != JudgementReason.Debounced)
            {
                return true;
            }

            return !unobserved && (now - entry.PendingChangedAt).TotalSeconds >= limits.DebounceSeconds;
        }
    }

    /// <summary>The Helper's input: the head as the old text and the current body as the new.</summary>
    private static AgentsMdChangeInput BuildInput(AgentsMdFile file, Entry entry, AgentsMdBody body)
    {
        string oldText = entry.HeadText ?? string.Empty; // A head with no text never reaches here: SettlesWithoutCall settles it first.
        AgentsMdComparison comparison = AgentsMdComparison.Between(oldText, body.Text);
        string label = file.Project ?? file.Team ?? "everyone";
        return new AgentsMdChangeInput(file.RelativePath, label, entry.HeadHash, body.Hash, oldText, body.Text, comparison);
    }

    /// <summary>Queues the input outside the lock (it reads SQLite); a failed queue gives the reservation back.</summary>
    private bool EnqueueOutsideLock(string key, IHelperAgent helper, AgentsMdChangeInput input)
    {
        try
        {
            this.Dispatcher().Enqueue(helper.Id, key, input);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            lock (this.gate)
            {
                if (this.entries.TryGetValue(key, out Entry? entry))
                {
                    entry.RunningFrom = null;
                    entry.RunningTo = null;
                    entry.LastJudgement = null;
                    entry.JudgementStartedAt = null;
                    entry.JudgedHash = null;
                }
            }

            this.logger.LogWarning(ex, "Could not queue the judgement of AGENTS.md '{Key}'; the Turn falls back to a comparison.", key);
            return false;
        }
    }

    /// <summary>
    /// Settles at once, with no call, an edit that needs none: an absent body (emptied, whitespace only or deleted), an absent
    /// head, a body over <c>MaxLedgerChars</c> or a head with no text. A Comparison when both texts are known, else Unknown.
    /// Goes through <see cref="Append"/>, so it prunes, completes waiters and clears the running pair like any settling.
    /// </summary>
    /// <returns>True when the edit was settled here.</returns>
    private bool SettlesWithoutCall(string key, AgentsMdBody body, AgentsMdOptions limits)
    {
        List<Waiter> ready;
        lock (this.gate)
        {
            if (!this.entries.TryGetValue(key, out Entry? entry) || string.Equals(body.Hash, entry.HeadHash, StringComparison.Ordinal))
            {
                return false;
            }

            bool fits = body.Text.Length <= limits.MaxLedgerChars;
            bool free = body.Hash.Length == 0 || !fits || entry.HeadHash.Length == 0 || entry.HeadText is null;
            if (!free)
            {
                return false;
            }

            AgentsMdNotice notice = fits && entry.HeadText is not null
                ? ComparisonBetween(entry.HeadText, body.Text, limits)
                : new AgentsMdNotice.Unknown();
            ready = this.Append(entry, body.Hash, fits ? body.Text : null, notice);
        }

        this.Persist(key);
        Complete(ready);
        return true;
    }

    /// <summary>
    /// Settles the edit with a Helper's notice (Spec §8.2): a Verdict for the head's own pair appends a revision and moves the
    /// head; a Verdict for a pair the fallback already settled replaces that revision's notice; anything else is stale.
    /// </summary>
    /// <param name="relativePath">The file.</param>
    /// <param name="fromHash">The head the Helper compared from.</param>
    /// <param name="toHash">The hash of the text it judged.</param>
    /// <param name="newText">The judged text; kept as the head's text when within <c>MaxLedgerChars</c>.</param>
    /// <param name="notice">What a Room is told.</param>
    /// <returns>False when the Verdict is stale.</returns>
    public bool CommitVerdict(string relativePath, string fromHash, string toHash, string newText, AgentsMdNotice notice)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(newText);
        ArgumentNullException.ThrowIfNull(notice);
        string key = KeyFor(relativePath);
        this.EnsureLoaded(key);
        int maxChars = this.options.Value.AgentsMd.MaxLedgerChars;
        List<Waiter> ready;
        lock (this.gate)
        {
            if (!this.entries.TryGetValue(key, out Entry? entry))
            {
                return false;
            }

            if (string.Equals(entry.HeadHash, fromHash, StringComparison.Ordinal))
            {
                ready = this.Append(entry, toHash, newText.Length <= maxChars ? newText : null, notice);
            }
            else
            {
                int index = entry.Revisions.FindLastIndex(revision => string.Equals(revision.FromHash, fromHash, StringComparison.Ordinal)
                    && string.Equals(revision.ToHash, toHash, StringComparison.Ordinal)
                    && !(revision.FromHash.Length == 0 && string.Equals(revision.ToHash, entry.FirstSightHash, StringComparison.Ordinal))
                    && revision.Notice is AgentsMdNotice.Comparison or AgentsMdNotice.Unknown);
                if (index < 0)
                {
                    return false;
                }

                entry.Revisions[index] = entry.Revisions[index] with { Notice = notice };
                ready = [];
            }
        }

        this.Persist(key);
        Complete(ready);
        return true;
    }

    /// <summary>
    /// Settles the pending edit with a plain comparison: the fallback. Reads the disk once and uses that text and its hash as
    /// one unit, because a pending edit stores no text. A head that is already current is left alone.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <returns>The revision that settled the edit, or the latest revision when nothing changed.</returns>
    public AgentsMdRevision CommitComparison(AgentsMdFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        string key = KeyFor(file.RelativePath);
        this.EnsureLoaded(key);
        string? headBeforeRead = this.HeadHashOf(key);
        bool exists = File.Exists(file.FullPath);
        string? raw = this.TryRead(file);
        AgentsMdBody? body = raw is null ? null : AgentsMdBody.Parse(raw);
        AgentsMdOptions limits = this.options.Value.AgentsMd;
        List<Waiter> ready;
        AgentsMdRevision settled;
        lock (this.gate)
        {
            if (!this.entries.TryGetValue(key, out Entry? entry))
            {
                return new AgentsMdRevision(string.Empty, body?.Hash ?? string.Empty, this.timeProvider.GetUtcNow(), new AgentsMdNotice.Unknown());
            }

            // A settling that landed while the file was being read made this read stale: leave its result alone. A move of the head
            // between the two locks cannot be forced in a test without a seam in the file read, so no test pins this line.
            if (body is null || string.Equals(body.Hash, entry.HeadHash, StringComparison.Ordinal) || !string.Equals(headBeforeRead, entry.HeadHash, StringComparison.Ordinal))
            {
                return entry.Revisions[^1];
            }

            bool fits = !exists || body.Text.Length <= limits.MaxLedgerChars;
            AgentsMdNotice notice = fits && entry.HeadText is not null
                ? ComparisonBetween(entry.HeadText, body.Text, limits)
                : new AgentsMdNotice.Unknown();
            ready = this.Append(entry, body.Hash, fits ? body.Text : null, notice);
            settled = entry.Revisions[^1];
        }

        this.Persist(key);
        Complete(ready);
        return settled;
    }

    /// <summary>The key's settled head hash; null when the file has no entry.</summary>
    private string? HeadHashOf(string key)
    {
        lock (this.gate)
        {
            return this.entries.TryGetValue(key, out Entry? entry) ? entry.HeadHash : null;
        }
    }

    /// <summary>
    /// Waits until the head reaches <paramref name="hash"/>, the deadline passes or <paramref name="cancellationToken"/> is
    /// cancelled. It completes in all three cases and never throws for them: the caller falls back to a comparison.
    /// </summary>
    /// <param name="relativePath">The file.</param>
    /// <param name="hash">The head hash to wait for.</param>
    /// <param name="deadline">When to stop waiting.</param>
    /// <param name="cancellationToken">Stops the wait.</param>
    public async Task WaitForHeadAsync(string relativePath, string hash, DateTimeOffset deadline, CancellationToken cancellationToken)
    {
        string key = KeyFor(relativePath);
        this.EnsureLoaded(key);
        Waiter waiter = new(hash);
        lock (this.gate)
        {
            if (!this.entries.TryGetValue(key, out Entry? entry) || string.Equals(entry.HeadHash, hash, StringComparison.Ordinal))
            {
                return;
            }

            entry.Waiters.Add(waiter);
        }

        try
        {
            TimeSpan remaining = deadline - this.timeProvider.GetUtcNow();
            if (remaining <= TimeSpan.Zero)
            {
                return;
            }

            using CancellationTokenSource timeout = new(remaining, this.timeProvider);
            using CancellationTokenSource bound = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            await waiter.Completion.Task.WaitAsync(bound.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The deadline passed or the caller stopped the wait; either way the caller falls back to a comparison.
        }
        finally
        {
            lock (this.gate)
            {
                _ = this.entries.TryGetValue(key, out Entry? entry);
                _ = entry?.Waiters.Remove(waiter);
            }
        }
    }

    /// <summary>Appends a revision from the head to <paramref name="toHash"/> and moves the head; the caller holds <c>gate</c>. Returns the waiters now satisfied.</summary>
    private List<Waiter> Append(Entry entry, string toHash, string? text, AgentsMdNotice notice)
    {
        entry.Revisions.Add(new AgentsMdRevision(entry.HeadHash, toHash, this.timeProvider.GetUtcNow(), notice));
        int surplus = entry.Revisions.Count - Math.Max(1, this.options.Value.AgentsMd.MaxRevisionsKept);
        if (surplus > 0)
        {
            entry.Revisions.RemoveRange(0, surplus);
        }

        entry.HeadHash = toHash;
        entry.HeadText = text;
        entry.RunningFrom = null;
        entry.RunningTo = null;
        entry.JudgementStartedAt = null;
        entry.JudgedHash = null;
        if (string.Equals(entry.PendingHash, toHash, StringComparison.Ordinal))
        {
            entry.PendingFile = null;
            entry.PendingHash = null;
        }

        List<Waiter> ready = entry.Waiters.FindAll(waiter => string.Equals(waiter.Hash, toHash, StringComparison.Ordinal));
        _ = entry.Waiters.RemoveAll(waiter => string.Equals(waiter.Hash, toHash, StringComparison.Ordinal));
        return ready;
    }

    /// <summary>Completes waiters; always called outside <c>gate</c>, so their continuations never run under it.</summary>
    private static void Complete(List<Waiter> ready)
    {
        foreach (Waiter waiter in ready)
        {
            _ = waiter.Completion.TrySetResult();
        }
    }

    /// <summary>A Comparison notice between two texts.</summary>
    private static AgentsMdNotice.Comparison ComparisonBetween(string oldText, string newText, AgentsMdOptions limits)
    {
        (IReadOnlyList<string> lines, int notShown) = AgentsMdComparison.Between(oldText, newText).Lines(limits.MaxComparisonChars);
        return new AgentsMdNotice.Comparison(lines, notShown);
    }

    /// <summary>The hash of this file's first sight, which a session with no heard map counts from.</summary>
    /// <param name="relativePath">The file.</param>
    public string? FirstSightHash(string relativePath)
    {
        string key = KeyFor(relativePath);
        this.EnsureLoaded(key);
        lock (this.gate)
        {
            return this.entries.TryGetValue(key, out Entry? entry) ? entry.FirstSightHash : null;
        }
    }

    /// <summary>The settled revisions after <paramref name="heardHash"/>, oldest first; null when the chain no longer reaches it.</summary>
    /// <param name="relativePath">The file.</param>
    /// <param name="heardHash">The hash a session last heard; empty for the absent file.</param>
    public IReadOnlyList<AgentsMdRevision>? Since(string relativePath, string heardHash)
    {
        string key = KeyFor(relativePath);
        this.EnsureLoaded(key);
        lock (this.gate)
        {
            if (!this.entries.TryGetValue(key, out Entry? entry))
            {
                return null;
            }

            if (string.Equals(heardHash, entry.HeadHash, StringComparison.Ordinal))
            {
                return [];
            }

            int index = entry.Revisions.FindLastIndex(revision => string.Equals(revision.ToHash, heardHash, StringComparison.Ordinal));
            if (index >= 0)
            {
                return [.. entry.Revisions.Skip(index + 1)];
            }

            bool reachesStart = entry.Revisions.Count > 0 && string.Equals(entry.Revisions[0].FromHash, heardHash, StringComparison.Ordinal);
            return reachesStart ? [.. entry.Revisions] : null;
        }
    }

    /// <summary>The ledger file's key: 32 hex characters of the path's SHA-256, case-folded where paths are.</summary>
    /// <param name="relativePath">The file, relative to the Teams root.</param>
    private static string KeyFor(string relativePath)
    {
        string folded = PathContainment.Comparison == StringComparison.OrdinalIgnoreCase ? relativePath.ToUpperInvariant() : relativePath;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(folded)))[..32];
    }

    /// <summary>Moves the pending marker of a known file; the caller holds <c>gate</c>. True when an edit is now pending.</summary>
    private bool Apply(Entry entry, AgentsMdFile file, AgentsMdBody body)
    {
        if (string.Equals(body.Hash, entry.HeadHash, StringComparison.Ordinal))
        {
            entry.PendingFile = null;
            entry.PendingHash = null;
            return false;
        }

        if (!string.Equals(body.Hash, entry.PendingHash, StringComparison.Ordinal))
        {
            entry.PendingFile = file;
            entry.PendingHash = body.Hash;
            entry.PendingChangedAt = this.timeProvider.GetUtcNow();
            entry.JudgementStartedAt = null;
            entry.JudgedHash = null;
        }

        return true;
    }

    /// <summary>A new entry: head set to the body and one Comparison revision from the absent hash.</summary>
    private Entry FirstSightEntry(AgentsMdFile file, AgentsMdBody body)
    {
        Entry entry = new()
        {
            RelativePath = file.RelativePath,
            FirstSightHash = body.Hash,
            HeadHash = body.Hash,
            HeadText = body.Text.Length <= this.options.Value.AgentsMd.MaxLedgerChars ? body.Text : null,
        };
        entry.Revisions.Add(new AgentsMdRevision(string.Empty, body.Hash, this.timeProvider.GetUtcNow(), new AgentsMdNotice.Comparison([], 0)));
        return entry;
    }

    /// <summary>Reads the file's raw text. A missing file is empty text; null when the read fails.</summary>
    private string? TryRead(AgentsMdFile file)
    {
        if (Directory.Exists(file.FullPath))
        {
            this.logger.LogWarning("AGENTS.md '{Path}' is a folder; leaving its ledger entry unchanged.", file.RelativePath);
            return null;
        }

        try
        {
            using FileStream stream = new(file.FullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using StreamReader reader = new(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return string.Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            this.logger.LogWarning(ex, "Could not read AGENTS.md '{Path}'; leaving its ledger entry unchanged.", file.RelativePath);
            return null;
        }
    }

    /// <summary>Loads the key's ledger file into memory unless it is already there; a file that cannot be read now is left as it is.</summary>
    private void EnsureLoaded(string key) => _ = this.TryEnsureLoaded(key);

    /// <summary>
    /// Loads the key's ledger file into memory unless it is already there, reading outside the lock. False when the file could
    /// not be read right now (locked, or access denied): that is not corruption, so the caller changes nothing and the file is
    /// never overwritten by a first sight.
    /// </summary>
    private bool TryEnsureLoaded(string key)
    {
        lock (this.gate)
        {
            if (this.entries.ContainsKey(key))
            {
                return true;
            }
        }

        Entry? loaded = this.TryLoad(key, out bool transient);
        if (transient)
        {
            return false;
        }

        if (loaded is not null)
        {
            lock (this.gate)
            {
                _ = this.entries.TryAdd(key, loaded);
            }
        }

        return true;
    }

    /// <summary>
    /// Reads one ledger file. A corrupt file, including valid JSON of the wrong shape, is logged at Warning and treated as
    /// missing; a read that fails with an I/O error is logged and reported through <paramref name="transient"/> instead.
    /// </summary>
    private Entry? TryLoad(string key, out bool transient)
    {
        transient = false;
        string path = Path.Combine(this.Folder, key + ".json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            AgentsMdLedgerDocument? document = JsonSerializer.Deserialize(stream, AgentsMdLedgerJson.Default.AgentsMdLedgerDocument);
            return (document is null ? null : Entry.From(document)) ?? this.Corrupt(path, null);
        }
        catch (JsonException ex)
        {
            return this.Corrupt(path, ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            this.logger.LogWarning(ex, "Could not read the ledger file '{Path}'; leaving it alone.", path);
            transient = true;
            return null;
        }
    }

    /// <summary>Logs a ledger file that cannot be used; the cost is one first sight.</summary>
    private Entry? Corrupt(string path, Exception? exception)
    {
        this.logger.LogWarning(exception, "Ledger file '{Path}' is unreadable; treating it as missing.", path);
        return null;
    }

    /// <summary>Writes the entry's file. Serialised by <c>writeGate</c>, with the snapshot taken once it is held.</summary>
    private void Persist(string key)
    {
        lock (this.writeGate)
        {
            AgentsMdLedgerDocument document;
            lock (this.gate)
            {
                document = this.entries[key].ToDocument();
            }

            try
            {
                _ = Directory.CreateDirectory(this.Folder);
                AtomicFile.WriteBytes(Path.Combine(this.Folder, key + ".json"), JsonSerializer.SerializeToUtf8Bytes(document, AgentsMdLedgerJson.Default.AgentsMdLedgerDocument));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                this.logger.LogWarning(ex, "Could not write the ledger for '{Path}'.", document.RelativePath);
            }
        }
    }

    /// <summary>A caller of <see cref="WaitForHeadAsync"/> waiting for a head hash; completed outside <c>gate</c>.</summary>
    private sealed class Waiter(string hash)
    {
        public string Hash { get; } = hash;

        public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>One file's in-memory state. The pending edit is not persisted: the next Observe finds it again.</summary>
    private sealed class Entry
    {
        public string RelativePath { get; init; } = string.Empty;

        public string FirstSightHash { get; init; } = string.Empty;

        public string HeadHash { get; set; } = string.Empty;

        public string? HeadText { get; set; }

        public DateTimeOffset? LastJudgement { get; set; }

        public DateTimeOffset? JudgementStartedAt { get; set; }

        public string? JudgedHash { get; set; }

        public string? RunningFrom { get; set; }

        public string? RunningTo { get; set; }

        public List<AgentsMdRevision> Revisions { get; } = [];

        public List<Waiter> Waiters { get; } = [];

        public AgentsMdFile? PendingFile { get; set; }

        public string? PendingHash { get; set; }

        public DateTimeOffset PendingChangedAt { get; set; }

        /// <summary>Builds the entry from a parsed ledger file; null when the file is valid JSON of the wrong shape (no revisions, a null revision or notice, or no hashes).</summary>
        /// <param name="document">The parsed file.</param>
        public static Entry? From(AgentsMdLedgerDocument document)
        {
            if (document.Revisions is null || document.Revisions.Count == 0 || document.HeadHash is null || document.FirstSightHash is null
                || document.Revisions.Exists(static revision => revision?.Notice is null))
            {
                return null;
            }

            Entry entry = new()
            {
                RelativePath = document.RelativePath ?? string.Empty,
                FirstSightHash = document.FirstSightHash,
                HeadHash = document.HeadHash,
                HeadText = document.HeadText,
            };
            entry.Revisions.AddRange(document.Revisions.Select(static revision =>
                new AgentsMdRevision(revision.FromHash, revision.ToHash, revision.SettledUtc, AgentsMdLedgerDocument.ToNotice(revision.Notice))));
            return entry;
        }

        /// <summary>The ledger file's document for this entry.</summary>
        public AgentsMdLedgerDocument ToDocument() => new()
        {
            RelativePath = this.RelativePath,
            FirstSightHash = this.FirstSightHash,
            HeadHash = this.HeadHash,
            HeadText = this.HeadText,
            Revisions =
            [
                .. this.Revisions.Select(static revision => new AgentsMdLedgerDocument.RevisionDocument
                {
                    FromHash = revision.FromHash,
                    ToHash = revision.ToHash,
                    SettledUtc = revision.SettledUtc,
                    Notice = AgentsMdLedgerDocument.FromNotice(revision.Notice),
                }),
            ],
        };
    }
}
