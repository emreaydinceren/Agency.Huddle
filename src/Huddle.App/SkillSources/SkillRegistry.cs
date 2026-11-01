using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Skills;
using Agency.Huddle.App.Teams;
using Agency.Huddle.SkillPacks.Checking;
using Agency.Huddle.SkillPacks.Files;
using Agency.Huddle.SkillPacks.Reading;
using Agency.Huddle.SkillPacks.Resolving;

namespace Agency.Huddle.App.SkillSources;

/// <summary>
/// The registry as the app uses it (§6.5): one current, immutable picture of SKILLS.md and what it points at, and an event
/// when it changes. It owns what is read; <see cref="RegistryWatcher"/> owns when. It replaces <c>SkillPackStore</c> and
/// <c>SkillsCatalogIndex</c>, and implements <see cref="ISkillAvailability"/> so <c>CardResolver</c> stays a pure static class.
/// </summary>
internal sealed class SkillRegistry : ISkillAvailability, IDisposable
{
    private readonly SkillSourcesSettings settings;
    private readonly SkillStore skills;
    private readonly PersonaStore personas;
    private readonly HiddenStore hidden;
    private readonly SourceLog log;
    private readonly RoleLister roles;
    private readonly ILogger<SkillRegistry> logger;
    private readonly TimeProvider clock;
    private readonly CachingFileReader files;
    private readonly SemaphoreSlim rebuildGate = new(1, 1);
    private readonly Lock pendingGate = new();
    private RebuildScope? pending;
    private RegistrySnapshot current = RegistrySnapshot.Empty;
    private ITimer? retryTimer;
    private bool disposed;

    /// <summary>Wires the registry to the stores it reads; it reads nothing until <see cref="ReloadAsync"/>.</summary>
    public SkillRegistry(
        SkillSourcesSettings settings,
        SkillStore skills,
        PersonaStore personas,
        HiddenStore hidden,
        SourceLog log,
        RoleLister roles,
        ILogger<SkillRegistry> logger,
        TimeProvider? timeProvider = null,
        CachingFileReader? files = null)
    {
        this.settings = settings;
        this.skills = skills;
        this.personas = personas;
        this.hidden = hidden;
        this.log = log;
        this.roles = roles;
        this.logger = logger;
        this.clock = timeProvider ?? TimeProvider.System;
        this.files = files ?? new CachingFileReader();
        this.personas.PersonasChanged += this.OnPersonasChanged;
        this.hidden.Changed += this.OnHiddenChanged;
        this.skills.SkillsChanged += this.OnSkillsChanged;
    }

    /// <summary>Raised on a thread-pool thread after every publish. A subscriber that throws is caught and logged.</summary>
    public event Action<RegistryChange>? Changed;

    /// <summary>The current picture; a reader on any thread sees a whole snapshot.</summary>
    public RegistrySnapshot Current => Volatile.Read(ref this.current);

    /// <summary>The name per-computer paths are matched against.</summary>
    public string ComputerName => this.settings.ComputerName;

    /// <summary>The Skill Sources folder.</summary>
    public string SkillSourcesDir => this.settings.Dir;

    /// <summary>SKILLS.md in that folder.</summary>
    public string RegistryPath => Path.Combine(this.settings.Dir, RegistryReader.FileName);

    /// <summary>The reader rebuilds use, for the callers that read the same files (the copier).</summary>
    public CachingFileReader Files => this.files;

    /// <summary>The state a Pack is shown in: the library's state refined by the Source, holders and <c>seen</c>.</summary>
    public ItemState DisplayState(PackEntry pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        RegistrySnapshot snapshot = this.Current;
        ItemCheckState state = snapshot.Checked.StateOf(pack.StateKey) ?? ItemCheckState.Broken;
        SourceStatus status = snapshot.Resolved.FindSource(pack.Source)?.Status ?? SourceStatus.Active;
        bool update = pack.SkillNames.Any(name => snapshot.Resolved.FindSkill(name)?.HasUpdate == true)
            || (snapshot.PackHeldVersions.TryGetValue(pack.Id, out IReadOnlySet<string>? versions) && versions.Any(version => !string.Equals(version, pack.Version, StringComparison.Ordinal)));
        bool held = snapshot.PackHolders.TryGetValue(pack.Id, out IReadOnlyList<string>? holders) && holders.Count > 0;
        bool isNew = snapshot.NewKeys.Contains(snapshot.Resolved.RowKey(pack));
        return Display(state, status, update, held, isNew);
    }

    /// <summary>The state a Skill is shown in.</summary>
    public ItemState DisplayState(SkillEntry skill)
    {
        ArgumentNullException.ThrowIfNull(skill);
        RegistrySnapshot snapshot = this.Current;
        ItemCheckState state = snapshot.Checked.StateOf(skill.StateKey) ?? ItemCheckState.Broken;
        SourceStatus status = skill.Source is null ? SourceStatus.Active : snapshot.Resolved.FindSource(skill.Source)?.Status ?? SourceStatus.Active;
        bool held = snapshot.SkillHolders.TryGetValue(skill.Name, out IReadOnlyList<string>? holders) && holders.Count > 0;
        return Display(state, status, skill.HasUpdate, held, snapshot.NewKeys.Contains(skill.StateKey));
    }

    /// <summary>Whether <c>hidden.json</c> hides the Pack.</summary>
    public bool IsHidden(PackEntry pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        return this.Current.HiddenPacks.Contains(pack.Id);
    }

    /// <summary>Whether <c>hidden.json</c> hides the Skill.</summary>
    public bool IsHidden(SkillEntry skill)
    {
        ArgumentNullException.ThrowIfNull(skill);
        return this.Current.HiddenSkills.Contains(skill.Name);
    }

    /// <summary>The Packs whose <c>appliesTo</c> names the role, not hidden, in registry order, in any state.</summary>
    public IReadOnlyList<PackEntry> PacksForRole(string roleId)
    {
        RegistrySnapshot snapshot = this.Current;
        return [.. snapshot.Resolved.PacksForRole(roleId).Where(pack => pack.File is not null && !pack.DuplicateId && !snapshot.HiddenPacks.Contains(pack.Id))];
    }

    /// <inheritdoc/>
    public SkillAvailability Of(string skillName)
    {
        ArgumentNullException.ThrowIfNull(skillName);
        if (this.skills.Get(skillName) is not null)
        {
            return SkillAvailability.Present;
        }

        SkillEntry? entry = this.Current.Resolved.FindSkill(skillName);
        if (entry is null)
        {
            return SkillAvailability.Unknown;
        }

        return this.DisplayState(entry) switch
        {
            ItemState.Broken => SkillAvailability.Broken,
            ItemState.Withdrawn => SkillAvailability.Withdrawn,
            ItemState.NotDownloaded => SkillAvailability.NotDownloaded,
            _ => SkillAvailability.Copyable,
        };
    }

    /// <inheritdoc/>
    public PackEntry? FindPack(string packId)
    {
        return this.Current.Resolved.FindPack(packId);
    }

    /// <inheritdoc/>
    public string? SubCategoryOf(string skillName)
    {
        string? sub = this.Current.Resolved.FindSkill(skillName)?.SubCategory;
        return string.IsNullOrWhiteSpace(sub) ? null : sub;
    }

    /// <inheritdoc/>
    public SkillSetupItem? SetupItem(SkillSetupKind kind, string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (kind == SkillSetupKind.Pack)
        {
            if (this.FindPack(name) is not { } pack)
            {
                return null;
            }

            return this.DisplayState(pack) switch
            {
                ItemState.Broken => new SkillSetupItem(kind, name, SkillAvailability.Broken, this.FirstError(pack.StateKey)),
                ItemState.Withdrawn => new SkillSetupItem(kind, name, SkillAvailability.Withdrawn, this.ReasonOf(pack.Source)),
                ItemState.NotDownloaded => new SkillSetupItem(kind, name, SkillAvailability.NotDownloaded, pack.Title),
                _ => null,
            };
        }

        SkillAvailability availability = this.Of(name);
        SkillEntry? entry = this.Current.Resolved.FindSkill(name);
        return availability switch
        {
            SkillAvailability.Present or SkillAvailability.Copyable => null,
            SkillAvailability.Broken => new SkillSetupItem(kind, name, availability, entry is null ? string.Empty : this.FirstError(entry.StateKey)),
            SkillAvailability.Withdrawn => new SkillSetupItem(kind, name, availability, this.ReasonOf(entry?.Source)),
            _ => new SkillSetupItem(kind, name, availability, string.Empty),
        };
    }

    /// <summary>The Agents whose <c>_packs</c> names the Pack.</summary>
    public IReadOnlyList<string> HolderNames(PackEntry pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        return this.Current.PackHolders.TryGetValue(pack.Id, out IReadOnlyList<string>? holders) ? holders : [];
    }

    /// <summary>The Agents whose <c>skills:</c> holds the Skill.</summary>
    public IReadOnlyList<string> HolderNames(SkillEntry skill)
    {
        ArgumentNullException.ThrowIfNull(skill);
        return this.Current.SkillHolders.TryGetValue(skill.Name, out IReadOnlyList<string>? holders) ? holders : [];
    }

    /// <summary>Whether a set of Packs and Skills can be handed to Agents now: Pack Skills expanded, built-in and copied
    /// Skills present, the rest checked against the registry.</summary>
    public SkillReadiness CheckReady(IReadOnlyList<string> packIds, IReadOnlyList<string> skillNames)
    {
        ArgumentNullException.ThrowIfNull(packIds);
        ArgumentNullException.ThrowIfNull(skillNames);
        RegistrySnapshot snapshot = this.Current;
        List<MissingItem> missing = [];
        List<string> names = [.. skillNames];
        foreach (string id in packIds)
        {
            PackEntry? pack = snapshot.Resolved.FindPack(id);
            if (pack is null)
            {
                missing.Add(new MissingItem(id, true, ItemState.Broken, $"Pack \"{id}\" is not in SKILLS.md", id, null));
                continue;
            }

            ItemState state = this.DisplayState(pack);
            if (state is ItemState.Broken or ItemState.Withdrawn)
            {
                missing.Add(new MissingItem(id, true, state, SkillSetupText.PackReason(pack.Title, state, this.FirstError(pack.StateKey), this.ReasonOf(pack.Source)), pack.Title, null));
                continue;
            }

            names.AddRange(pack.SkillNames);
        }

        List<SkillEntry> ready = [];
        foreach (string name in names.Distinct(StringComparer.Ordinal))
        {
            if (this.skills.Get(name) is not null)
            {
                continue;
            }

            SkillEntry? entry = snapshot.Resolved.FindSkill(name);
            if (entry is null)
            {
                missing.Add(new MissingItem(name, false, ItemState.Broken, $"Skill \"{name}\" is not in any catalog", name, null));
                continue;
            }

            ItemState state = this.DisplayState(entry);
            if (state is ItemState.Broken or ItemState.Withdrawn or ItemState.NotDownloaded)
            {
                string? catalog = entry.Claims.Select(static claim => claim.ViaCatalog ?? (claim.Kind == RowKind.Catalog ? claim.Provider["catalog:".Length..] : null)).FirstOrDefault(static id => id is not null);
                missing.Add(new MissingItem(name, false, state, SkillSetupText.SkillReason(name, state, this.FirstError(entry.StateKey), this.ReasonOf(entry.Source)), name, catalog));
                continue;
            }

            ready.Add(entry);
        }

        return new SkillReadiness(ready, missing);
    }

    /// <summary>Re-reads everything with the caches cleared, ignoring the two-second grace (the Reload button, start-up).</summary>
    public Task ReloadAsync(CancellationToken ct)
    {
        return this.RebuildAsync(RebuildScope.All with { IgnoreGrace = true }, ct);
    }

    /// <summary>Rebuilds for <paramref name="scope"/>. Serialised; requests that arrive while one runs merge into one follow-up.
    /// A rebuild that throws for an I/O reason keeps the previous snapshot and publishes nothing.</summary>
    public async Task RebuildAsync(RebuildScope scope, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(scope);
        lock (this.pendingGate)
        {
            this.pending = this.pending is null ? scope : this.pending.Union(scope);
        }

        await this.rebuildGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            RebuildScope? work;
            lock (this.pendingGate)
            {
                work = this.pending;
                this.pending = null;
            }

            if (work is null || this.disposed)
            {
                return;
            }

            await Task.Run(() => this.Build(work, ct), ct).ConfigureAwait(false);
        }
        finally
        {
            this.rebuildGate.Release();
        }
    }

    /// <summary>Marks keys as seen: merges them into <c>seen</c>, prunes keys no longer present, and keeps <c>dir</c>.</summary>
    public Task MarkSeenAsync(IReadOnlyCollection<string> keys, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ct.ThrowIfCancellationRequested();
        RegistrySnapshot snapshot = this.Current;
        HashSet<string> present = AllKeys(snapshot.Resolved);
        HashSet<string> seen = new(this.settings.Seen ?? [], StringComparer.Ordinal);
        seen.UnionWith(keys);
        seen.IntersectWith(present);
        this.settings.SetSeen(seen);
        this.Publish(snapshot with
        {
            Version = snapshot.Version + 1,
            NewKeys = new HashSet<string>(snapshot.NewKeys.Where(key => !seen.Contains(key)), StringComparer.Ordinal),
        }, [], []);
        return Task.CompletedTask;
    }

    /// <summary>The context the app's own checks run with, built from its services at this moment (§6.5).</summary>
    public CheckContext BuildCheckContext()
    {
        List<InstalledSkill> installed = [.. this.skills.All.Select(static skill => new InstalledSkill(
            skill.Name,
            skill.Provenance?.EffectiveOrigin,
            skill.Provenance?.Sha256,
            skill.Source is SkillSource.Default or SkillSource.Overridden))];
        return new CheckContext(
            SkillSourcesSettings.HuddleVersion,
            1,
            this.settings.ComputerName,
            this.settings.DataDir,
            this.settings.Dir,
            this.settings.SkillsDir,
            this.roles.List(),
            installed,
            [.. SkillGrants.Grantable.Order(StringComparer.Ordinal)],
            this.clock.GetLocalNow());
    }

    /// <summary>Stops listening.</summary>
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;
        this.personas.PersonasChanged -= this.OnPersonasChanged;
        this.hidden.Changed -= this.OnHiddenChanged;
        this.skills.SkillsChanged -= this.OnSkillsChanged;
        this.retryTimer?.Dispose();
        this.rebuildGate.Dispose();
    }

    /// <summary>Every key that can be New: row keys and downloaded Skills.</summary>
    internal static HashSet<string> AllKeys(ResolvedRegistry resolved)
    {
        HashSet<string> keys = new(StringComparer.Ordinal);
        foreach (PackEntry pack in resolved.Packs)
        {
            keys.Add(resolved.RowKey(pack));
        }

        foreach (ResolvedCatalog catalog in resolved.Catalogs)
        {
            keys.Add(resolved.RowKey(catalog));
        }

        foreach (SkillEntry skill in resolved.Skills.Where(static skill => skill.PathState == SkillPathState.Present))
        {
            keys.Add(skill.StateKey);
        }

        return keys;
    }

    /// <summary>Applies the precedence of §6.2: Broken, Withdrawn, Not downloaded, Update available, Deprecated, In use, New, Available.</summary>
    private static ItemState Display(ItemCheckState state, SourceStatus status, bool update, bool held, bool isNew)
    {
        if (state == ItemCheckState.Broken)
        {
            return ItemState.Broken;
        }

        if (status == SourceStatus.Withdrawn)
        {
            return ItemState.Withdrawn;
        }

        if (state == ItemCheckState.NotDownloaded)
        {
            return ItemState.NotDownloaded;
        }

        if (update)
        {
            return ItemState.UpdateAvailable;
        }

        if (status is SourceStatus.Deprecated or SourceStatus.Moved)
        {
            return ItemState.Deprecated;
        }

        if (held)
        {
            return ItemState.InUse;
        }

        return isNew ? ItemState.New : ItemState.Available;
    }

    /// <summary>The first error message filed under an item, or empty.</summary>
    private string FirstError(string key)
    {
        RegistrySnapshot snapshot = this.Current;
        string? file = key.StartsWith("pack:", StringComparison.Ordinal)
            ? snapshot.Resolved.FindPack(key["pack:".Length..])?.FilePath
            : snapshot.Resolved.FindSkill(key["skill:".Length..])?.FolderPath;
        Finding? finding = snapshot.Checked.Findings.FirstOrDefault(finding => finding.Severity == Severity.Error && file is not null && finding.File.StartsWith(file, StringComparison.OrdinalIgnoreCase));
        return finding?.Message ?? string.Empty;
    }

    /// <summary>The Source's status reason, or empty.</summary>
    private string ReasonOf(string? sourceId)
    {
        return sourceId is null ? string.Empty : this.Current.Resolved.FindSource(sourceId)?.File?.StatusReason ?? string.Empty;
    }

    /// <summary>One rebuild: read, resolve, check, overlay and publish (§6.5, "A rebuild").</summary>
    private void Build(RebuildScope scope, CancellationToken ct)
    {
        RegistrySnapshot previous = this.Current;
        RegistrySnapshot snapshot;
        try
        {
            this.files.Invalidate(scope);
            Directory.CreateDirectory(this.settings.Dir);
            Registry registry = RegistryReader.Load(this.files, this.settings.Dir, this.settings.ComputerName);
            CheckContext context = this.BuildCheckContext();
            ResolvedRegistry resolved = RegistryResolver.Resolve(registry, this.files, context, new ResolveOptions(scope.IgnoreGrace));
            CheckResult checkedResult = Checks.Run(resolved, context, new CheckOptions(), this.files, ct);
            snapshot = this.Overlay(previous.Version + 1, resolved, checkedResult);
        }
        catch (IOException exception)
        {
            this.logger.LogWarning(exception, "The Skill registry could not be read; the previous picture stays.");
            return;
        }
        catch (UnauthorizedAccessException exception)
        {
            this.logger.LogWarning(exception, "The Skill registry could not be read; the previous picture stays.");
            return;
        }

        HashSet<string> before = AllKeys(previous.Resolved);
        List<string> appeared = [.. AllKeys(snapshot.Resolved).Where(key => !before.Contains(key))];
        List<string> changed = [.. snapshot.Resolved.Skills
            .Where(skill => previous.Resolved.FindSkill(skill.Name) is not { } old
                || !string.Equals(old.PointerSha256, skill.PointerSha256, StringComparison.Ordinal)
                || previous.Checked.StateOf(skill.StateKey) != snapshot.Checked.StateOf(skill.StateKey))
            .Select(static skill => skill.Name)];
        this.Publish(snapshot, appeared, changed);
        this.LogRows(previous, snapshot);
        this.ScheduleRetry(snapshot.Resolved.Retry);
    }

    /// <summary>Adds holders, hidden items and <c>seen</c> to a resolved and checked registry.</summary>
    private RegistrySnapshot Overlay(long version, ResolvedRegistry resolved, CheckResult checkedResult)
    {
        (Dictionary<string, IReadOnlyList<string>> packHolders, Dictionary<string, IReadOnlyList<string>> skillHolders, Dictionary<string, IReadOnlySet<string>> versions) = this.Holders();
        HiddenSet hiddenSet = this.hidden.Read();
        HashSet<string> keys = AllKeys(resolved);
        IReadOnlyList<string>? seen = this.settings.Seen;
        if (seen is null && resolved.Registry.Findings.All(static finding => finding.Code != "R001"))
        {
            this.settings.SetSeen(keys);
            seen = [.. keys];
        }

        HashSet<string> seenSet = new(seen ?? [.. keys], StringComparer.Ordinal);
        return new RegistrySnapshot(
            version,
            resolved,
            checkedResult,
            new HashSet<string>(keys.Where(key => !seenSet.Contains(key)), StringComparer.Ordinal),
            packHolders,
            skillHolders,
            hiddenSet.Packs,
            hiddenSet.Skills,
            versions);
    }

    /// <summary>Counts holders from the Agent files.</summary>
    private (Dictionary<string, IReadOnlyList<string>> Packs, Dictionary<string, IReadOnlyList<string>> Skills, Dictionary<string, IReadOnlySet<string>> Versions) Holders()
    {
        Dictionary<string, List<string>> packs = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, List<string>> skillNames = new(StringComparer.Ordinal);
        Dictionary<string, HashSet<string>> versions = new(StringComparer.OrdinalIgnoreCase);
        foreach (PersonaEntry entry in this.personas.Entries)
        {
            if (!PersonaFrontmatter.TryReadIdentity(entry.Text, out PersonaIdentity? identity, out _))
            {
                continue;
            }

            foreach (VersionedRef pack in identity.Packs ?? [])
            {
                Add(packs, pack.Id, entry.Name);
                if (!versions.TryGetValue(pack.Id, out HashSet<string>? set))
                {
                    set = new HashSet<string>(StringComparer.Ordinal);
                    versions[pack.Id] = set;
                }

                set.Add(pack.Version);
            }

            foreach (string skill in identity.Skills ?? [])
            {
                Add(skillNames, skill, entry.Name);
            }
        }

        return (
            packs.ToDictionary(static pair => pair.Key, static pair => (IReadOnlyList<string>)pair.Value, StringComparer.OrdinalIgnoreCase),
            skillNames.ToDictionary(static pair => pair.Key, static pair => (IReadOnlyList<string>)pair.Value, StringComparer.Ordinal),
            versions.ToDictionary(static pair => pair.Key, static pair => (IReadOnlySet<string>)pair.Value, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>Adds <paramref name="value"/> to the list under <paramref name="key"/>.</summary>
    private static void Add(Dictionary<string, List<string>> map, string key, string value)
    {
        if (!map.TryGetValue(key, out List<string>? list))
        {
            list = [];
            map[key] = list;
        }

        if (!list.Contains(value, StringComparer.Ordinal))
        {
            list.Add(value);
        }
    }

    /// <summary>Swaps the snapshot in and raises <see cref="Changed"/> outside any lock, catching each handler.</summary>
    private void Publish(RegistrySnapshot snapshot, IReadOnlyList<string> appeared, IReadOnlyList<string> changed)
    {
        Interlocked.Exchange(ref this.current, snapshot);
        Action<RegistryChange>? handlers = this.Changed;
        if (handlers is null)
        {
            return;
        }

        RegistryChange change = new(snapshot, appeared, changed);
        // A subscriber is UI or a writer that must not stop the others or the rebuild thread (§6.5 step 6).
        EventRaise.Isolated(handlers, change, exception => this.logger.LogError(exception, "A SkillRegistry.Changed subscriber threw; the others still run."));
    }

    /// <summary>Appends <c>row-appeared</c>, <c>row-broken</c> and <c>row-recovered</c> lines to the Sources' logs.</summary>
    private void LogRows(RegistrySnapshot previous, RegistrySnapshot next)
    {
        if (previous.Version == 0)
        {
            return;
        }

        foreach (PackEntry pack in next.Resolved.Packs.Where(static pack => pack.File is not null))
        {
            ItemCheckState? before = previous.Checked.StateOf(pack.StateKey);
            ItemCheckState? after = next.Checked.StateOf(pack.StateKey);
            string? eventName = (before, after) switch
            {
                (null, _) => "row-appeared",
                (not ItemCheckState.Broken, ItemCheckState.Broken) => "row-broken",
                (ItemCheckState.Broken, not ItemCheckState.Broken) => "row-recovered",
                _ => null,
            };
            if (eventName is not null)
            {
                string? code = eventName == "row-broken" ? next.Checked.Findings.FirstOrDefault(finding => finding.Severity == Severity.Error && pack.FilePath is not null && finding.File.StartsWith(pack.FilePath, StringComparison.OrdinalIgnoreCase))?.Code : null;
                this.log.Append(pack.Source, new LogLine(eventName, $"pack:{pack.Id}", Detail: $"{pack.Title} {pack.Version}", Code: code));
            }
        }
    }

    /// <summary>Reads a half-written file again after the grace, once.</summary>
    private void ScheduleRetry(IReadOnlyList<string> retry)
    {
        if (retry.Count == 0 || this.disposed)
        {
            return;
        }

        this.retryTimer?.Dispose();
        this.retryTimer = this.clock.CreateTimer(_ => this.RebuildInBackground(new RebuildScope(false, retry, [])), null, RegistryResolver.Grace, Timeout.InfiniteTimeSpan);
    }

    /// <summary>Starts a rebuild without awaiting it; failures are logged.</summary>
    internal void RebuildInBackground(RebuildScope scope)
    {
        _ = this.RebuildLoggedAsync(scope);
    }

    /// <summary>A rebuild whose expected failures are logged rather than thrown.</summary>
    private async Task RebuildLoggedAsync(RebuildScope scope)
    {
        try
        {
            await this.RebuildAsync(scope, CancellationToken.None).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            // The host is shutting down; there is nothing left to rebuild for.
        }
        catch (OperationCanceledException)
        {
            // Cancelled with the host; the next start reads everything again.
        }
    }

    /// <summary>An Agent file changed: republish the same registry with fresh holders, no re-read.</summary>
    private void OnPersonasChanged()
    {
        RegistrySnapshot snapshot = this.Current;
        if (snapshot.Version == 0)
        {
            return;
        }

        (Dictionary<string, IReadOnlyList<string>> packs, Dictionary<string, IReadOnlyList<string>> skillHolders, Dictionary<string, IReadOnlySet<string>> versions) = this.Holders();
        this.Publish(snapshot with { Version = snapshot.Version + 1, PackHolders = packs, SkillHolders = skillHolders, PackHeldVersions = versions }, [], []);
    }

    /// <summary><c>hidden.json</c> changed: refresh visibility only.</summary>
    private void OnHiddenChanged()
    {
        RegistrySnapshot snapshot = this.Current;
        HiddenSet set = this.hidden.Read();
        this.Publish(snapshot with { Version = snapshot.Version + 1, HiddenPacks = set.Packs, HiddenSkills = set.Skills }, [], []);
    }

    /// <summary>A copy in <c>Skills/</c> changed, which changes installed hashes: rebuild the checks.</summary>
    private void OnSkillsChanged()
    {
        if (this.Current.Version > 0)
        {
            this.RebuildInBackground(new RebuildScope(false, [], [this.settings.SkillsDir]));
        }
    }
}
