using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Agents;
using Agency.Huddle.App.SkillSources;
using Agency.Huddle.SkillPacks.Checking;
using Agency.Huddle.SkillPacks.Reading;
using Agency.Huddle.SkillPacks.Resolving;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.SkillPacks.RegistryService;

/// <summary>Pins the watch-root rule of §8.5 and <see cref="RegistryWatcher"/>'s start-up, errors and polling.</summary>
public sealed class RegistryWatcherTests
{
    private static readonly string SkillSources = InMemoryFileReader.At("SS");

    /// <summary>A resolved registry whose Skills live in the given folders.</summary>
    private static ResolvedRegistry WithSkillFolders(IEnumerable<string> folders)
    {
        Registry registry = new(SkillSources, [], [], []);
        List<SkillEntry> skills = [.. folders.Select(static (folder, i) => new SkillEntry($"s{i}", "d", null, null, null, null, [], "o", null, folder, [], null, null, [], SkillPathState.Present, []))];
        return new ResolvedRegistry(registry, [], [], skills, [], [], DateTimeOffset.UnixEpoch, []);
    }

    /// <summary>33 distinct roots plus the Skill Sources folder: 32 watchers, the rest polled.</summary>
    [Fact]
    public void WatchRoots_33Roots_CreatesAtMost32AndPollsTheRest()
    {
        ResolvedRegistry registry = WithSkillFolders(Enumerable.Range(0, 33).Select(static i => InMemoryFileReader.At($"clone{i}", "skills", "x")));

        WatchPlan plan = WatchRoots.Plan(SkillSources, registry, static _ => true, static _ => false);

        Assert.Equal(WatchRoots.MaxWatchers, plan.Watched.Count);
        Assert.Equal(2, plan.Polled.Count);
        Assert.Equal(SkillSources, plan.Watched[0].Path);
    }

    /// <summary>Four Skill folders under one clone's <c>skills/</c> give one root; a root under another root collapses.</summary>
    [Fact]
    public void WatchRoots_NestedRoots_Collapse()
    {
        ResolvedRegistry registry = WithSkillFolders([
            InMemoryFileReader.At("anthropics", "skills", "docx"),
            InMemoryFileReader.At("anthropics", "skills", "pdf"),
            InMemoryFileReader.At("anthropics", "skills", "pptx"),
            InMemoryFileReader.At("anthropics", "skills", "xlsx"),
            InMemoryFileReader.At("anthropics", "skills", "deep", "inner")]);

        WatchPlan plan = WatchRoots.Plan(SkillSources, registry, static _ => true, static _ => false);

        Assert.Equal([SkillSources, InMemoryFileReader.At("anthropics", "skills")], plan.Watched.Select(static root => root.Path));
    }

    /// <summary>A root on a network drive, or one that does not exist yet, is polled.</summary>
    [Fact]
    public void WatchRoots_NetworkRoot_IsPolled()
    {
        string network = InMemoryFileReader.At("share", "skills", "x");
        string missing = InMemoryFileReader.At("later", "skills", "y");
        ResolvedRegistry registry = WithSkillFolders([network, missing]);

        WatchPlan plan = WatchRoots.Plan(SkillSources, registry, path => !path.Contains("later", StringComparison.Ordinal), path => path.Contains("share", StringComparison.Ordinal));

        Assert.Equal([SkillSources], plan.Watched.Select(static root => root.Path));
        Assert.Equal(2, plan.Polled.Count);
    }

    /// <summary>A path under SKILLS.md re-reads everything; a pack file and a Skill folder are partial scopes.</summary>
    [Fact]
    public void ScopeFor_MapsPaths()
    {
        string folder = InMemoryFileReader.At("clone", "skills", "docx");
        ResolvedRegistry registry = WithSkillFolders([folder]);

        RebuildScope all = RegistryWatcher.ScopeFor([Path.Combine(SkillSources, "SKILLS.md")], SkillSources, registry);
        RebuildScope partial = RegistryWatcher.ScopeFor([Path.Combine(SkillSources, "packs", "a.skillpack.json"), Path.Combine(folder, "SKILL.md")], SkillSources, registry);

        Assert.True(all.Everything);
        Assert.False(partial.Everything);
        Assert.Single(partial.Files);
        Assert.Equal([folder], partial.SkillFolders);
    }

    /// <summary>A move that records the registry version when it runs.</summary>
    private sealed class RecordingMove(SkillRegistry registry, bool fail = false) : ISkillSourcesMove
    {
        /// <summary>The registry version when the move ran; -1 when it did not.</summary>
        public long VersionAtRun { get; private set; } = -1;

        /// <inheritdoc/>
        public Task RunAsync(CancellationToken ct)
        {
            this.VersionAtRun = registry.Current.Version;
            return fail ? throw new InvalidOperationException("move failed") : Task.CompletedTask;
        }
    }

    /// <summary>Start-up runs the move before the first read, then publishes; a missing Skill Sources folder is created.</summary>
    [Fact]
    public async Task StartAsync_MoveThenFirstRead_CreatesFolder()
    {
        using RegistryHost host = new();
        Directory.Delete(host.Dir, recursive: true);
        RecordingMove move = new(host.Registry);
        using RegistryWatcher watcher = new(host.Registry, move, host.Settings, NullLogger<RegistryWatcher>.Instance, host.Clock);

        await watcher.StartAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, move.VersionAtRun);
        Assert.True(Directory.Exists(host.Dir));
        Assert.Equal(1, host.Registry.Current.Version);
        Assert.Contains(host.Dir, watcher.WatchedRoots);
    }

    /// <summary>A move that throws is logged; the first read still happens and publishes R001.</summary>
    [Fact]
    public async Task StartAsync_MoveThrows_FirstReadStillPublishes()
    {
        using RegistryHost host = new();
        using RegistryWatcher watcher = new(host.Registry, new RecordingMove(host.Registry, fail: true), host.Settings, NullLogger<RegistryWatcher>.Instance, host.Clock);

        await watcher.StartAsync(TestContext.Current.CancellationToken);

        Assert.Equal("R001", Assert.Single(host.Registry.Current.Checked.Findings, static finding => finding.Severity == Severity.Error).Code);
    }

    /// <summary>A watcher error moves its root to polling and the next quiet period re-reads everything.</summary>
    [Fact]
    public async Task Watcher_Error_MovesRootToPollingAndFullReread()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RegistryHost host = new();
        using RegistryWatcher watcher = new(host.Registry, new NoSkillSourcesMove(), host.Settings, NullLogger<RegistryWatcher>.Instance, host.Clock);
        await watcher.StartAsync(ct);
        TaskCompletionSource rebuilt = new(TaskCreationOptions.RunContinuationsAsynchronously);
        host.Registry.Changed += _ => rebuilt.TrySetResult();

        watcher.OnWatcherError(host.Dir);
        host.Clock.Advance(RegistryWatcher.Debounce);
        await rebuilt.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);

        Assert.DoesNotContain(host.Dir, watcher.WatchedRoots);
        Assert.Contains(watcher.PolledRoots, root => root.Path == host.Dir);
    }

    /// <summary>A polled path that appears later triggers a rebuild on the next poll.</summary>
    [Fact]
    public async Task Poll_PathAppearsLater_TriggersRebuild()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RegistryHost host = new();
        string folder = Path.Combine(host.Clone, "skills", "docx");
        host.Source();
        host.Pack("documents", "docx", folder);
        host.SkillsMd(["packs/documents.skillpack.json"]);
        using RegistryWatcher watcher = new(host.Registry, new NoSkillSourcesMove(), host.Settings, NullLogger<RegistryWatcher>.Instance, host.Clock);
        await watcher.StartAsync(ct);
        Assert.Equal(ItemCheckState.NotDownloaded, host.Registry.Current.Checked.StateOf("skill:docx"));
        watcher.Poll();
        TaskCompletionSource rebuilt = new(TaskCreationOptions.RunContinuationsAsynchronously);
        host.Registry.Changed += _ => rebuilt.TrySetResult();

        host.CloneSkill("docx");
        watcher.Poll();
        host.Clock.Advance(RegistryWatcher.Debounce);
        await rebuilt.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);

        Assert.Equal(ItemCheckState.Usable, host.Registry.Current.Checked.StateOf("skill:docx"));
    }

    /// <summary>The watcher is a hosted service that starts before the seeder and the supervisor.</summary>
    [Fact]
    public void AddTeamServices_RegistryWatcherStartsBeforePersonaSupervisor()
    {
        using TempDataDir dataDir = new();
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Team:DataDir"] = dataDir.Path }).Build();
        ServiceCollection services = new();
        services.AddLogging();
        services.AddTeamServices(configuration);
        using ServiceProvider provider = services.BuildServiceProvider();

        List<IHostedService> hosted = [.. provider.GetServices<IHostedService>()];

        int watcher = hosted.FindIndex(static service => service is RegistryWatcher);
        int seeder = hosted.FindIndex(static service => service is BuiltinAgentSeeder);
        int supervisor = hosted.FindIndex(static service => service is PersonaSupervisor);
        Assert.InRange(watcher, 0, seeder - 1);
        Assert.True(watcher < supervisor || supervisor < 0);
    }
}
