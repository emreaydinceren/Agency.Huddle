using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Agents;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.Tasks;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// SB-R07 (Spec 6.2): gates and cadence read the Team's <c>_team.md</c>; onboarding and the Done checklist read the Team's
/// <c>AGENTS.md</c>; a Team with no <c>AGENTS.md</c> has neither, and a leftover <c>Process.md</c> is not a fallback.
/// </summary>
public sealed class ProcessSourcesTests
{
    private const string Team = "Platform";
    private const string Manager = "Mia";
    private const string Check = "dotnet test";
    private const string Agents = "# Home\n\n## Onboarding\n\n- Read the workspace.\n- Run the tests.\n\n## Definition of Done\n\n- [ ] Tests pass\n";
    private const string LegacyProcess = "---\ntitle: 'Flow'\ngates: ['qa-evidence-before-done']\nretro: { every: 3, unit: 'days' }\n---\n# Flow\n\n## Onboarding\n\n- Legacy step.\n\n## Definition of Done\n\n- [ ] Legacy item\n";
    private const string QaDone = "Gate qa-evidence-before-done: an Evidence line from a member who verifies (for example QA Engineer) is needed before this Task can be Done.";

    private static readonly DateTimeOffset Start = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly IReadOnlyDictionary<string, string> Covers = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Quinn"] = "verify, review",
    };

    private static readonly string Manifest = Factories.TeamTypeJson.Manifest("web", "Web", order: 1, version: "1.2.0", extraJson: "\"retroRole\": \"retro\"", rolesJson: """
        [
            { "id": "lead", "title": "Lead", "candidate": "candidates/lead.md", "manager": true, "required": true },
            { "id": "retro", "title": "Retro", "candidate": "candidates/retro.md", "maxPerTeam": 1 }
        ]
        """);

    /// <summary>
    /// A Team whose <c>_team.md</c> lists <c>qa-evidence-before-done</c> refuses a Done move with no verifier Evidence, and
    /// allows it once a member whose role covers verify has logged a run. No <c>Process.md</c> exists, so the gate cannot come from there.
    /// </summary>
    [Fact]
    public void CheckTaskChange_GateInTeamFileOnly_RefusesDoneUntilProofComplete()
    {
        using TempDataDir dir = new();
        ProcessGateService service = Gates(dir, [GateId.QaEvidenceBeforeDone]);
        EvidenceLine verified = new(Start, "Quinn", Check, 0, null, null, "Quinn | dotnet test");

        GateRefusal? refused = service.CheckTaskChange(Context("Review", "Done", []));
        GateRefusal? allowed = service.CheckTaskChange(Context("Review", "Done", [verified]));

        // Refused: gates [qa-evidence-before-done] (from _team.md) and no Evidence line. Allowed: Quinn covers "verify" and ran one.
        Assert.Equal(GateId.QaEvidenceBeforeDone, refused?.Gate);
        Assert.Equal(QaDone, refused?.Message);
        Assert.Null(allowed);
        Assert.Equal([GateId.QaEvidenceBeforeDone], service.EnabledGates(Team));
    }

    /// <summary>The Done checklist is the <c>AGENTS.md</c> list under the Definition of Done heading.</summary>
    [Fact]
    public void ProcessOf_AgentsMdWithDoneItems_ReadsTheChecklist()
    {
        using TempDataDir dir = new();
        File.WriteAllText(Path.Combine(dir.Path, TeamPaths.AgentsMdFileName), Agents);
        ProcessGateService service = Gates(dir, [GateId.QaEvidenceBeforeDone]);

        ProcessDocument? process = service.ProcessOf(Team);

        // One "- [ ] Tests pass" line under "## Definition of Done" gives the one label "Tests pass".
        Assert.NotNull(process);
        Assert.Equal(["Tests pass"], process.DefinitionOfDone.Select(static item => item.Label).ToArray());
    }

    /// <summary>With no <c>AGENTS.md</c> the Team has no Done items, even if a legacy <c>Process.md</c> lists some.</summary>
    [Fact]
    public void ProcessOf_NoAgentsMdButLegacyProcessMd_HasNoDoneItems()
    {
        using TempDataDir dir = new();
        File.WriteAllText(Path.Combine(dir.Path, "Process.md"), LegacyProcess);
        ProcessGateService service = Gates(dir, []);

        IReadOnlyList<DoneItem> items = service.ProcessOf(Team)?.DefinitionOfDone ?? [];

        // The only file is Process.md, which no caller reads any more: nothing to list.
        Assert.Empty(items);
    }

    /// <summary><c>retro:</c> in <c>_team.md</c> schedules the retro; no <c>Process.md</c> or <c>AGENTS.md</c> is needed.</summary>
    [Fact]
    public async Task Tick_RetroInTeamFile_FiresAfterItsDays()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CadenceArrange arrange = new(new ProcessSettings([], null, "{ every: 3, unit: 'days' }", null, null, null));
        CadenceService service = arrange.Service();
        await service.TickAsync(ct);
        arrange.Clock.UtcNow = Start.AddDays(2);
        await service.TickAsync(ct);
        Assert.Empty(arrange.Poster.Posts);

        arrange.Clock.UtcNow = Start.AddDays(3);
        await service.TickAsync(ct);

        // Baseline 2026-10-08 12:00; every 3 days is due at 2026-10-11 12:00 (not at +2 days). No Retro agent is hired, so the
        // manager Mia gets the note plus the prompt rendered as key|team|since date.
        Assert.Equal([(Manager, "No Retro agent is hired on this Team, so please run this retro yourself.\n\nprompt.retro|Platform|2026-10-08")], arrange.Poster.Posts);
    }

    /// <summary>A legacy <c>Process.md</c> with a retro cadence does not schedule anything when <c>_team.md</c> has none.</summary>
    [Fact]
    public async Task Tick_RetroOnlyInLegacyProcessMd_PostsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CadenceArrange arrange = new(ProcessSettings.Empty);
        File.WriteAllText(Path.Combine(arrange.Folder, "Process.md"), LegacyProcess);
        CadenceService service = arrange.Service();
        await service.TickAsync(ct);

        arrange.Clock.UtcNow = Start.AddDays(3);
        await service.TickAsync(ct);

        // _team.md carries no retro, and Process.md is no longer a source.
        Assert.Empty(arrange.Poster.Posts);
    }

    /// <summary>The onboarding steps are the <c>AGENTS.md</c> Onboarding section.</summary>
    [Fact]
    public async Task PostIfDueAsync_AgentsMdWithOnboarding_PostsItsSteps()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        File.WriteAllText(Path.Combine(dir.Path, TeamPaths.AgentsMdFileName), Agents);
        DiskTeamFolders folders = new(dir.Path);
        FakeOnboardingPoster poster = new();

        bool posted = await new TeamOnboarding(folders, new FakePromptSource(), poster).PostIfDueAsync(Team, Manager, ct);

        // The two bullets under "## Onboarding", up to the next heading, joined by a newline (the same shape TeamOnboardingTests pins).
        Assert.True(posted);
        (string manager, string text) = Assert.Single(poster.Posts);
        Assert.Equal(Manager, manager);
        Assert.Equal("Your Team has just started. Work through these onboarding steps, then tell the Human what you found and propose a starter Task:\n\n- Read the workspace.\n- Run the tests.", text);
        Assert.True(folders.File?.Onboarded);
    }

    /// <summary>A Team with no <c>AGENTS.md</c> has no onboarding steps, even if a legacy <c>Process.md</c> lists some: nothing is posted.</summary>
    [Fact]
    public async Task PostIfDueAsync_NoAgentsMdButLegacyProcessMd_PostsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        File.WriteAllText(Path.Combine(dir.Path, "Process.md"), LegacyProcess);
        DiskTeamFolders folders = new(dir.Path);
        FakeOnboardingPoster poster = new();

        bool posted = await new TeamOnboarding(folders, new FakePromptSource(), poster).PostIfDueAsync(Team, Manager, ct);

        // Only Process.md exists, so there are no steps and the marker is not written.
        Assert.False(posted);
        Assert.Empty(poster.Posts);
        Assert.False(folders.File?.Onboarded);
    }

    private static GateContext Context(string from, string to, IReadOnlyList<EvidenceLine> evidence) =>
        new(Team, new TaskId("TASK", 1), from, to, false, true, evidence, Covers, null);

    private static ProcessGateService Gates(TempDataDir dir, IReadOnlyList<GateId> gates)
    {
        VersionedRef type = new("web", "1.2.0");
        DiskTeamFolders folders = new(dir.Path)
        {
            File = new TeamFile(type, null, null, null, string.Empty) { Process = ProcessSettings.Empty with { Gates = gates, CheckCommand = Check } },
        };
        FakeTeamCatalog catalog = new() { Teams = [new TeamSummary(Team, [], [], true) { TeamType = type }] };
        return new ProcessGateService(catalog, folders, RoleFixtures.Types(dir));
    }

    /// <summary>Resolves every prompt to its key, the Team and the since date, so a test can pin what was asked for.</summary>
    private sealed class KeyedPrompts : IPromptSource
    {
        /// <inheritdoc />
        public string Render(string key, IReadOnlyDictionary<string, string> values) =>
            string.Concat(key, "|", values["{{team}}"], "|", values["{{since}}"]);

        /// <inheritdoc />
        public string Raw(string key) => key;
    }

    /// <summary>One Team over a temporary folder whose <c>_team.md</c> holds the given settings, with a manual clock.</summary>
    private sealed class CadenceArrange : IDisposable
    {
        private readonly TempDataDir dir = new();
        private readonly PersonaStore personas;
        private readonly DiskTeamFolders folders;
        private readonly FakeTeamCatalog catalog;
        private readonly TeamTypeCatalog types;
        private readonly CadenceStateStore state;
        private readonly List<IDisposable> owned = [];

        public CadenceArrange(ProcessSettings settings)
        {
            VersionedRef type = new("web", "1.2.0");
            this.personas = this.dir.CreatePersonaStore();
            this.state = new CadenceStateStore(this.dir.Options());
            this.folders = new DiskTeamFolders(this.dir.Path)
            {
                File = new TeamFile(type, null, null, null, string.Empty) { Process = settings },
            };
            this.catalog = new FakeTeamCatalog
            {
                Teams = [new TeamSummary(Team, [], [Manager], true) { TeamType = type, Manager = Manager }],
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

        /// <summary>The clock the service reads.</summary>
        public ManualTimeProvider Clock { get; } = new(Start);

        /// <summary>The poster the service posts through.</summary>
        public CadenceServiceTests.RecordingPoster Poster { get; } = new();

        /// <summary>The Team folder.</summary>
        public string Folder => this.dir.Path;

        /// <summary>Builds the service over the shared state.</summary>
        public CadenceService Service()
        {
            TaskStore tasks = TestTaskStore.CreateTaskStore(this.dir, this.personas);
            this.owned.Add(tasks);
            return new CadenceService(this.catalog, this.folders, this.types, this.personas, tasks, this.state, new KeyedPrompts(), this.Poster, NullLogger<CadenceService>.Instance, this.Clock);
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
