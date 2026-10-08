using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Tasks;
using Agency.Huddle.Tests.Ui.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>The cadence service wakes a manager for digests and a Retro agent for retros on schedule or count, once per period (OR-3, RT-2, NF-8).</summary>
public sealed class CadenceServiceTests
{
    private const string Team = "Platform";
    private const string Manager = "Mia";
    private const string RetroAgent = "Rae";
    private const string NoRetroNote = "No Retro agent is hired on this Team, so please run this retro yourself.\n\n";

    private const string Manifest = """
        { "schemaVersion": 1, "id": "web", "version": "1.2.0", "displayName": "Web", "order": 1,
          "workspace": { "kind": "none", "label": "None", "required": false },
          "processes": [{ "id": "flow", "file": "processes/flow.md", "default": true }],
          "retroRole": "retro",
          "roles": [
            { "id": "lead", "title": "Lead", "candidate": "candidates/lead.md", "manager": true, "required": true },
            { "id": "retro", "title": "Retro", "candidate": "candidates/retro.md", "maxPerTeam": 1 }
          ] }
        """;

    private static readonly DateTimeOffset Start = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The first time a Team is seen only sets the baseline: nothing is posted, even with a cadence due on paper.</summary>
    [Fact]
    public async Task Tick_FirstSight_SetsBaselineAndPostsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness harness = new();
        harness.WriteProcess(Both);

        await harness.Service().TickAsync(ct);

        Assert.Empty(harness.Poster.Posts);
        Assert.NotNull(harness.State.Get(Team)?.LastDigestUtc);
    }

    /// <summary>A one-day digest does not fire before the day has passed.</summary>
    [Fact]
    public async Task Tick_DigestBeforeOneDay_PostsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness harness = new();
        harness.WriteProcess(Both);
        CadenceService service = harness.Service();
        await service.TickAsync(ct);

        harness.Clock.UtcNow = Start.AddHours(23).AddMinutes(59);
        await service.TickAsync(ct);

        Assert.Empty(harness.Poster.Posts);
    }

    /// <summary>At one day the digest goes to the manager, rendered from prompt.digest with the Team and the baseline date.</summary>
    [Fact]
    public async Task Tick_DigestAtOneDay_PostsToManager()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness harness = new();
        harness.WriteProcess(Both);
        CadenceService service = harness.Service();
        await service.TickAsync(ct);

        harness.Clock.UtcNow = Start.AddDays(1);
        await service.TickAsync(ct);

        Assert.Equal([(Manager, "prompt.digest|Platform|2026-10-08")], harness.Poster.Posts);
    }

    /// <summary>After a digest fires the next one waits another full period.</summary>
    [Fact]
    public async Task Tick_DigestFired_DoesNotRepeatWithinThePeriod()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness harness = new();
        harness.WriteProcess(Both);
        CadenceService service = harness.Service();
        await service.TickAsync(ct);
        harness.Clock.UtcNow = Start.AddDays(1);
        await service.TickAsync(ct);

        harness.Clock.UtcNow = Start.AddDays(1).AddHours(5);
        await service.TickAsync(ct);

        Assert.Single(harness.Poster.Posts);
    }

    /// <summary>A second service instance over the same database does not refire what the first already posted (NF-8).</summary>
    [Fact]
    public async Task Tick_SecondInstanceOverSameDb_DoesNotRefire()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness harness = new();
        harness.WriteProcess(Both);
        await harness.Service().TickAsync(ct);
        harness.Clock.UtcNow = Start.AddDays(1);
        await harness.Service().TickAsync(ct);

        await harness.Service().TickAsync(ct);

        Assert.Single(harness.Poster.Posts);
    }

    /// <summary>A refused post leaves the state alone, so the next tick tries again.</summary>
    [Fact]
    public async Task Tick_PostRefused_RetriesOnNextTick()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness harness = new();
        harness.WriteProcess(Both);
        CadenceService service = harness.Service();
        await service.TickAsync(ct);
        harness.Clock.UtcNow = Start.AddDays(1);
        harness.Poster.Accept = false;
        await service.TickAsync(ct);
        harness.Poster.Accept = true;

        await service.TickAsync(ct);

        Assert.Equal(
            [(Manager, "prompt.digest|Platform|2026-10-08"), (Manager, "prompt.digest|Platform|2026-10-08")],
            harness.Poster.Posts);
    }

    /// <summary>Nine new Done Tasks do not trigger a ten-Task retro.</summary>
    [Fact]
    public async Task Tick_RetroBelowCount_PostsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness harness = new();
        harness.WriteProcess(Both);
        await harness.Service().TickAsync(ct);
        harness.WriteDone(9);

        await harness.Service().TickAsync(ct);

        Assert.Empty(harness.Poster.Posts);
    }

    /// <summary>Ten new Done Tasks fire the retro, and it goes to the hired Retro agent.</summary>
    [Fact]
    public async Task Tick_RetroAtCountWithAgentHired_PostsToRetroAgent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness harness = new();
        harness.WriteProcess(Both);
        harness.HireRetroAgent();
        await harness.Service().TickAsync(ct);
        harness.WriteDone(10);

        await harness.Service().TickAsync(ct);

        Assert.Equal([(RetroAgent, "prompt.retro|Platform|2026-10-08")], harness.Poster.Posts);
    }

    /// <summary>With no Retro agent hired the retro goes to the manager with a note saying so.</summary>
    [Fact]
    public async Task Tick_RetroAtCountWithoutAgent_PostsToManagerWithNote()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness harness = new();
        harness.WriteProcess(Both);
        await harness.Service().TickAsync(ct);
        harness.WriteDone(10);

        await harness.Service().TickAsync(ct);

        Assert.Equal([(Manager, NoRetroNote + "prompt.retro|Platform|2026-10-08")], harness.Poster.Posts);
    }

    /// <summary>A count-based retro does not fire a second time for the same Done Tasks, even from a fresh instance.</summary>
    [Fact]
    public async Task Tick_RetroFired_DoesNotRefireForSameTasks()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness harness = new();
        harness.WriteProcess(Both);
        await harness.Service().TickAsync(ct);
        harness.WriteDone(10);
        await harness.Service().TickAsync(ct);

        await harness.Service().TickAsync(ct);

        Assert.Single(harness.Poster.Posts);
    }

    /// <summary>A day-based retro fires when its days have passed.</summary>
    [Fact]
    public async Task Tick_RetroByDays_FiresAfterDays()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness harness = new();
        harness.WriteProcess("---\ntitle: 'Flow'\nretro: { every: 3, unit: 'days' }\n---\n# Flow\n");
        harness.HireRetroAgent();
        CadenceService service = harness.Service();
        await service.TickAsync(ct);
        harness.Clock.UtcNow = Start.AddDays(2);
        await service.TickAsync(ct);
        Assert.Empty(harness.Poster.Posts);

        harness.Clock.UtcNow = Start.AddDays(3);
        await service.TickAsync(ct);

        Assert.Equal([(RetroAgent, "prompt.retro|Platform|2026-10-08")], harness.Poster.Posts);
    }

    /// <summary>A Team without a Process.md is ignored: no state, no posts.</summary>
    [Fact]
    public async Task Tick_NoProcessFile_IsIgnored()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness harness = new();
        CadenceService service = harness.Service();
        await service.TickAsync(ct);

        harness.Clock.UtcNow = Start.AddDays(30);
        await service.TickAsync(ct);

        Assert.Empty(harness.Poster.Posts);
        Assert.Null(harness.State.Get(Team));
    }

    private static string Both =>
        "---\ntitle: 'Flow'\ndigest: { every: 1, unit: 'days' }\nretro: { every: 10, unit: 'done-tasks' }\n---\n# Flow\n";

    /// <summary>Records each post instead of waking anyone.</summary>
    private sealed class RecordingPoster : IOnboardingPoster
    {
        /// <summary>Every post, in order, as recipient and text.</summary>
        public List<(string Name, string Text)> Posts { get; } = [];

        /// <summary>What <see cref="PostAsync"/> reports; false simulates a refused post.</summary>
        public bool Accept { get; set; } = true;

        /// <inheritdoc />
        public Task<bool> PostAsync(string managerName, string text, CancellationToken ct)
        {
            this.Posts.Add((managerName, text));
            return Task.FromResult(this.Accept);
        }
    }

    /// <summary>Renders a prompt as its key, the Team and the since date, so a test can pin what was asked for.</summary>
    private sealed class KeyedPrompts : IPromptSource
    {
        /// <inheritdoc />
        public string Render(string key, IReadOnlyDictionary<string, string> values) =>
            string.Concat(key, "|", values["{{team}}"], "|", values["{{since}}"]);

        /// <inheritdoc />
        public string Raw(string key) => key;
    }

    /// <summary>One Team over a temporary data folder, with a manual clock and a recording poster.</summary>
    private sealed class Harness : IDisposable
    {
        private readonly TempDataDir dir = new();
        private readonly PersonaStore personas;
        private readonly FakeTeamFolders folders;
        private readonly FakeTeamCatalog catalog;
        private readonly TeamTypeCatalog types;
        private readonly List<IDisposable> owned = [];

        public Harness()
        {
            this.personas = TestTaskStore.CreatePersonaStore(this.dir);
            this.State = new CadenceStateStore(this.dir.Options());
            VersionedRef type = new("web", "1.2.0");
            LibraryRoot root = new("teams", "Teams", this.dir.Path, LibraryRootKind.Teams);
            this.folders = new FakeTeamFolders
            {
                TeamResult = LibraryResult<LibraryPath>.Ok(new LibraryPath(root, Team, this.dir.Path, LibraryNodeRole.TeamFolder)),
            };
            this.catalog = new FakeTeamCatalog
            {
                Teams = [new TeamSummary(Team, [], [Manager, RetroAgent], true) { TeamType = type, Manager = Manager }],
            };
            Dictionary<string, string> builtins = new(StringComparer.Ordinal)
            {
                ["web/team-type.json"] = Manifest,
                ["web/candidates/lead.md"] = "---\ntitle: Lead\nconsult_when: planning\n---\nYou lead.",
                ["web/candidates/retro.md"] = "---\ntitle: Retro\nconsult_when: retros\n---\nYou review.",
                ["web/processes/flow.md"] = "# Flow",
            };
            this.types = new TeamTypeCatalog(this.dir.Options(), NullLogger<TeamTypeCatalog>.Instance, builtins);
        }

        /// <summary>The clock every service built here reads.</summary>
        public ManualTimeProvider Clock { get; } = new(Start);

        /// <summary>The poster every service built here posts through.</summary>
        public RecordingPoster Poster { get; } = new();

        /// <summary>The state store shared by every service built here, like one database across restarts.</summary>
        public CadenceStateStore State { get; }

        /// <summary>Builds a fresh service, with a fresh Task store, over the shared state.</summary>
        public CadenceService Service()
        {
            TaskStore tasks = TestTaskStore.CreateTaskStore(this.dir, this.personas);
            this.owned.Add(tasks);
            return new CadenceService(this.catalog, this.folders, this.types, this.personas, tasks, this.State, new KeyedPrompts(), this.Poster, NullLogger<CadenceService>.Instance, this.Clock);
        }

        /// <summary>Writes the Team's Process.md.</summary>
        /// <param name="text">The file's text.</param>
        public void WriteProcess(string text) =>
            File.WriteAllText(Path.Combine(this.dir.Path, TeamPaths.ProcessFileName), text);

        /// <summary>Hires the Teammate holding the type's retro role.</summary>
        public void HireRetroAgent() =>
            _ = this.personas.Add(
                new PersonaIdentity(RetroAgent, "Retro", "rae", [Team], Role: new RoleRef("web", "retro", "1.2.0")),
                "You run retros.");

        /// <summary>Writes Done Tasks PLAT-0001 to <paramref name="count"/> on disk.</summary>
        /// <param name="count">How many Done Tasks the Team has afterwards.</param>
        public void WriteDone(int count)
        {
            string root = TestTaskStore.Root(this.dir);
            for (int number = 1; number <= count; number++)
            {
                string id = $"PLAT-{number:0000}";
                _ = TestTaskStore.WriteTask(
                    root,
                    Path.Combine(Team, "_tasks", $"{id}.md"),
                    TestTasks.Make(id: id, status: TaskState.Done, location: new(Team, null, false)));
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            foreach (IDisposable item in this.owned)
            {
                item.Dispose();
            }

            this.personas.Dispose();
            this.dir.Dispose();
        }
    }
}
