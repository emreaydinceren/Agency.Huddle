using Agency.Huddle.App.SkillSources;
using Agency.Huddle.App.Teams;
using Agency.Huddle.SkillPacks.Checking;
using Agency.Huddle.SkillPacks.Resolving;

namespace Agency.Huddle.Tests.SkillPacks.RegistryService;

/// <summary>Pins <see cref="SkillRegistry"/> (§6.5): versions, appeared keys, the New overlay, holders without a re-read,
/// readiness, availability, merged rebuilds and a throwing subscriber.</summary>
public sealed class SkillRegistryTests
{
    /// <summary>A host with one Pack whose one Skill is downloaded.</summary>
    private static RegistryHost WithPack()
    {
        RegistryHost host = new();
        host.Source();
        host.Pack("documents", "docx", host.CloneSkill("docx"));
        host.SkillsMd(["packs/documents.skillpack.json"]);
        return host;
    }

    /// <summary>Reload publishes version 1 with the Pack usable.</summary>
    [Fact]
    public async Task Reload_PublishesFirstSnapshot()
    {
        using RegistryHost host = WithPack();

        await host.Registry.ReloadAsync(TestContext.Current.CancellationToken);

        RegistrySnapshot snapshot = host.Registry.Current;
        Assert.Equal(1, snapshot.Version);
        PackEntry pack = Assert.Single(snapshot.Resolved.Packs);
        Assert.Equal(ItemState.Available, host.Registry.DisplayState(pack));
        Assert.DoesNotContain(snapshot.Checked.Findings, static finding => finding.Severity == Severity.Error);
    }

    /// <summary>The first complete read writes the <c>seen</c> baseline, so nothing is New.</summary>
    [Fact]
    public async Task FirstRead_WritesSeenBaseline_NothingNew()
    {
        using RegistryHost host = WithPack();

        await host.Registry.ReloadAsync(TestContext.Current.CancellationToken);

        Assert.Empty(host.Registry.Current.NewKeys);
        Assert.NotNull(host.Settings.Seen);
        Assert.Contains("skill:docx", host.Settings.Seen);
    }

    /// <summary>A row added after the baseline is New and is in the change's appeared keys; marking it seen clears it.</summary>
    [Fact]
    public async Task NewRow_IsNewUntilMarkedSeen()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RegistryHost host = WithPack();
        await host.Registry.ReloadAsync(ct);
        List<RegistryChange> changes = [];
        host.Registry.Changed += changes.Add;
        host.Pack("sheets", "xlsx", host.CloneSkill("xlsx"));
        host.SkillsMd(["packs/documents.skillpack.json", "packs/sheets.skillpack.json"]);

        await host.Registry.RebuildAsync(RebuildScope.All, ct);

        PackEntry sheets = host.Registry.FindPack("sheets") ?? throw new InvalidOperationException("sheets is missing");
        Assert.Equal(ItemState.New, host.Registry.DisplayState(sheets));
        Assert.Contains(host.Registry.Current.Resolved.RowKey(sheets), changes[^1].AppearedKeys);

        await host.Registry.MarkSeenAsync([host.Registry.Current.Resolved.RowKey(sheets), "skill:xlsx"], ct);

        Assert.Equal(ItemState.Available, host.Registry.DisplayState(sheets));
    }

    /// <summary>A Skill's state reads both its check state and the New overlay by its <c>skill:</c> key: a Skill added after
    /// the baseline is New, Available once seen, and In use once an Agent holds it.</summary>
    [Fact]
    public async Task SkillDisplayState_NewThenAvailableThenInUse()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RegistryHost host = WithPack();
        await host.Registry.ReloadAsync(ct);
        host.Pack("sheets", "xlsx", host.CloneSkill("xlsx"));
        host.SkillsMd(["packs/documents.skillpack.json", "packs/sheets.skillpack.json"]);
        await host.Registry.RebuildAsync(RebuildScope.All, ct);
        SkillEntry xlsx = host.Registry.Current.Resolved.FindSkill("xlsx") ?? throw new InvalidOperationException("xlsx is missing");

        ItemState fresh = host.Registry.DisplayState(xlsx);
        await host.Registry.MarkSeenAsync(["skill:xlsx"], ct);
        ItemState seen = host.Registry.DisplayState(xlsx);
        host.Agent("Jane", ["xlsx"], []);
        ItemState held = host.Registry.DisplayState(xlsx);

        Assert.Equal([ItemState.New, ItemState.Available, ItemState.InUse], [fresh, seen, held]);
    }

    /// <summary>An Agent holding the Pack makes it In use; the republish reuses the resolved registry.</summary>
    [Fact]
    public async Task Holders_RepublishedWithoutReread()
    {
        using RegistryHost host = WithPack();
        await host.Registry.ReloadAsync(TestContext.Current.CancellationToken);
        ResolvedRegistry resolved = host.Registry.Current.Resolved;

        host.Agent("Jane", ["docx"], [new VersionedRef("documents", "1.0.0")]);

        PackEntry pack = host.Registry.FindPack("documents") ?? throw new InvalidOperationException("documents is missing");
        Assert.Same(resolved, host.Registry.Current.Resolved);
        Assert.Equal(["Jane"], host.Registry.HolderNames(pack));
        Assert.Equal(ItemState.InUse, host.Registry.DisplayState(pack));
    }

    /// <summary>A holder whose <c>_packs</c> version differs from the file shows Update available.</summary>
    [Fact]
    public async Task HolderOnOldVersion_UpdateAvailable()
    {
        using RegistryHost host = WithPack();
        await host.Registry.ReloadAsync(TestContext.Current.CancellationToken);

        host.Agent("Jane", ["docx"], [new VersionedRef("documents", "0.9.0")]);

        PackEntry pack = host.Registry.FindPack("documents") ?? throw new InvalidOperationException("documents is missing");
        Assert.Equal(ItemState.UpdateAvailable, host.Registry.DisplayState(pack));
    }

    /// <summary>Availability: a built-in Skill is Present, a downloaded one Copyable, an empty path Not downloaded, anything else Unknown.</summary>
    [Fact]
    public async Task Of_MapsEveryCase()
    {
        using RegistryHost host = WithPack();
        host.Pack("later", "pdf", folder: null);
        host.SkillsMd(["packs/documents.skillpack.json", "packs/later.skillpack.json"]);

        await host.Registry.ReloadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(SkillAvailability.Present, host.Registry.Of("team-building"));
        Assert.Equal(SkillAvailability.Copyable, host.Registry.Of("docx"));
        Assert.Equal(SkillAvailability.NotDownloaded, host.Registry.Of("pdf"));
        Assert.Equal(SkillAvailability.Unknown, host.Registry.Of("no-such-skill"));
    }

    /// <summary>CheckReady expands Pack Skills and names what is missing.</summary>
    [Fact]
    public async Task CheckReady_ExpandsPacksAndNamesMissing()
    {
        using RegistryHost host = WithPack();
        host.Pack("later", "pdf", folder: null);
        host.SkillsMd(["packs/documents.skillpack.json", "packs/later.skillpack.json"]);
        await host.Registry.ReloadAsync(TestContext.Current.CancellationToken);

        SkillReadiness ready = host.Registry.CheckReady(["documents", "later", "absent"], ["team-building"]);

        Assert.Equal(["docx"], ready.Ready.Select(static skill => skill.Name));
        Assert.Equal(["absent", "pdf"], ready.Missing.Select(static item => item.Name).Order(StringComparer.Ordinal));
        Assert.False(ready.IsReady);
    }

    /// <summary>
    /// A burst of rebuild requests while one runs merges into at most one follow-up. The first publish is held open until
    /// every request is in, so the burst arrives while a rebuild runs however fast the machine is; without that, a quick
    /// first build finishes before the last request arrives and a second or third build starts (seen on a loaded CI runner).
    /// </summary>
    [Fact]
    public async Task Rebuild_BurstOfScopes_MergesIntoOneFollowUp()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RegistryHost host = WithPack();
        using ManualResetEventSlim firstPublishReached = new(false);
        using ManualResetEventSlim burstIssued = new(false);
        int publishes = 0;
        host.Registry.Changed += _ =>
        {
            if (Interlocked.Increment(ref publishes) == 1)
            {
                firstPublishReached.Set();
                burstIssued.Wait(ct);
            }
        };

        Task first = host.Registry.RebuildAsync(new RebuildScope(false, ["f0"], []), ct);
        Assert.True(firstPublishReached.Wait(TimeSpan.FromSeconds(30), ct));
        List<Task> burst = [first, .. Enumerable.Range(1, 9).Select(i => host.Registry.RebuildAsync(new RebuildScope(false, [$"f{i}"], []), ct))];
        burstIssued.Set();
        await Task.WhenAll(burst);

        Assert.InRange(publishes, 1, 2);
    }

    /// <summary>Deleting SKILLS.md publishes R001 and no rows.</summary>
    [Fact]
    public async Task Rebuild_SkillsMdDeleted_PublishesR001AndNoRows()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RegistryHost host = WithPack();
        await host.Registry.ReloadAsync(ct);

        File.Delete(host.Registry.RegistryPath);
        await host.Registry.RebuildAsync(RebuildScope.All, ct);

        Assert.Empty(host.Registry.Current.Resolved.Packs);
        Assert.Equal("R001", Assert.Single(host.Registry.Current.Checked.Findings, static finding => finding.Severity == Severity.Error).Code);
    }

    /// <summary>A subscriber that throws does not stop the others.</summary>
    [Fact]
    public async Task ThrowingSubscriber_OthersStillRun()
    {
        using RegistryHost host = WithPack();
        bool second = false;
        host.Registry.Changed += _ => throw new InvalidOperationException("boom");
        host.Registry.Changed += _ => second = true;

        await host.Registry.ReloadAsync(TestContext.Current.CancellationToken);

        Assert.True(second);
    }

    /// <summary>The app's context marks shipped Skills as built in and carries the configured computer name.</summary>
    [Fact]
    public void BuildCheckContext_BuiltInAndComputer()
    {
        using RegistryHost host = new("LAPTOP");

        CheckContext context = host.Registry.BuildCheckContext();

        Assert.Equal("LAPTOP", context.ComputerName);
        Assert.Contains(context.InstalledSkills, static skill => skill.Name == "team-building" && skill.BuiltIn);
        Assert.Contains("propose_sprint", context.AppToolNames);
        Assert.Contains(context.Roles, static role => role.Id == "dev");
    }

    /// <summary>A pack file that fails to parse right after a write is not Broken; two seconds later it is read again.</summary>
    [Fact]
    public async Task HalfWrittenFile_RereadAfterGrace()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RegistryHost host = WithPack();
        await host.Registry.ReloadAsync(ct);
        string path = Path.Combine(host.Dir, "packs", "documents.skillpack.json");
        File.WriteAllText(path, "{ \"schemaVersion\": 1, \"id\": \"docu");
        host.Clock.SetUtcNow(new DateTimeOffset(File.GetLastWriteTimeUtc(path)).AddMilliseconds(500));
        TaskCompletionSource reread = new(TaskCreationOptions.RunContinuationsAsynchronously);

        await host.Registry.RebuildAsync(new RebuildScope(false, [path], []), ct);
        Assert.Equal([path], host.Registry.Current.Resolved.Retry);
        Assert.Equal(ItemCheckState.NotDownloaded, host.Registry.Current.Checked.StateOf("row:pack:0"));
        host.Registry.Changed += _ => reread.TrySetResult();
        host.Clock.Advance(TimeSpan.FromSeconds(3));
        await reread.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);

        Assert.Equal(ItemCheckState.Broken, host.Registry.Current.Checked.StateOf("row:pack:0"));
    }
}
