using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Teammates;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Teams;


/// <summary>A start gate that records the waits and answers with a configured outcome.</summary>
internal sealed class FakeStartGate : IPersonaStartGate
{
    /// <summary>Gets the names waited for, in order.</summary>
    public List<string> Waited { get; } = [];

    /// <summary>Gets or sets whether the Teammate reports running before the timeout.</summary>
    public bool Running { get; set; } = true;

    /// <summary>Gets or sets whether the wait throws <see cref="OperationCanceledException"/>.</summary>
    public bool Cancel { get; set; }

    /// <summary>Gets or sets a callback run at each wait with the waited name.</summary>
    public Action<string>? OnWait { get; set; }

    /// <inheritdoc />
    public Task<bool> WaitUntilRunningAsync(string name, TimeSpan timeout, CancellationToken ct)
    {
        this.Waited.Add(name);
        this.OnWait?.Invoke(name);
        if (this.Cancel)
        {
            throw new OperationCanceledException();
        }

        return Task.FromResult(this.Running);
    }
}

/// <summary>Tests for <see cref="HireService.StartTeamAsync"/>: members start manager first, after skills, with nothing written on a refusal (HR-9).</summary>
public sealed class HireServiceStartTests
{
    private const string TeamName = "Web Team";

    private const string Manifest = """
        { "schemaVersion": 1, "id": "web", "version": "1.2.0", "displayName": "Web", "order": 1,
          "workspace": { "kind": "none", "label": "None", "required": false },
          "processes": [{ "id": "flow", "file": "processes/flow.md", "default": true }],
          "roles": [
            { "id": "lead", "title": "Lead", "candidate": "candidates/lead.md", "manager": true, "required": true },
            { "id": "dev", "title": "Developer", "candidate": "candidates/dev.md", "maxPerTeam": 3 }
          ] }
        """;

    private const string FlatManifest = """
        { "schemaVersion": 1, "id": "flat", "version": "1.0.0", "displayName": "Flat", "order": 2,
          "workspace": { "kind": "none", "label": "None", "required": false },
          "processes": [{ "id": "flow", "file": "processes/flow.md", "default": true }],
          "roles": [
            { "id": "lead", "title": "Lead", "candidate": "candidates/lead.md", "manager": true },
            { "id": "dev", "title": "Developer", "candidate": "candidates/dev.md", "required": true }
          ] }
        """;

    private static HireService Create(TempDataDir dir, out PersonaStore personas, out FakeSkillCopier installer, out FakeStartGate gate, TeamOptions? options = null, Func<TeamTypeCatalog, ProcessProvisioner>? makeProvisioner = null, Func<TeamTypeCatalog, TeamOnboarding>? makeOnboarding = null)
    {
        personas = new(new TeammatePaths(dir.Options()), new PersonaModelStore(dir.Options()), new PersonaEffortStore(dir.Options()), new PersonaWorkModeStore(dir.Options()), NullLogger<PersonaStore>.Instance);
        Dictionary<string, string> builtins = new(StringComparer.Ordinal)
        {
            ["web/team-type.json"] = Manifest,
            ["web/candidates/lead.md"] = "---\ntitle: Lead\nconsult_when: planning\n---\nYou lead.",
            ["web/candidates/dev.md"] = "---\ntitle: Developer\nconsult_when: code\n---\nYou build.",
            ["web/processes/flow.md"] = "# Flow",
            ["flat/team-type.json"] = FlatManifest,
            ["flat/candidates/lead.md"] = "---\ntitle: Lead\nconsult_when: planning\n---\nYou lead.",
            ["flat/candidates/dev.md"] = "---\ntitle: Developer\nconsult_when: code\n---\nYou build.",
            ["flat/processes/flow.md"] = "# Flow",
        };
        TeamTypeCatalog types = new(dir.Options(), NullLogger<TeamTypeCatalog>.Instance, builtins);
        SkillPackStore packs = new(dir.Options(), NullLogger<SkillPackStore>.Instance, new Dictionary<string, string>(StringComparer.Ordinal));
        HireLimitHarness harness = new(Microsoft.Extensions.Options.Options.Create(options ?? new TeamOptions()));
        AvatarStore avatars = new(dir.Options(), NullLogger<AvatarStore>.Instance);
        installer = new FakeSkillCopier();
        gate = new FakeStartGate();
        return new HireService(personas, types, FakeSkillAvailability.From(packs), avatars, harness.Guard, installer, gate, NullLogger<HireService>.Instance, makeProvisioner?.Invoke(types), makeOnboarding?.Invoke(types));
    }

    private static HireRequest Request(string name, string roleId, CardReadiness readiness = CardReadiness.Ready)
    {
        CardState card = new(roleId, name, new ModelPick("claude-agent-acp", "sonnet"),
            [new SkillRef("tdd", "builtin", null), new SkillRef(roleId == "lead" ? "planning" : "coding", "builtin", null)],
            [], readiness, [], []);
        return new HireRequest(TeamName, "web", roleId, card);
    }

    /// <summary>The manager is hired and confirmed running before any other member exists, whatever the request order.</summary>
    [Fact]
    public async Task StartTeamAsync_ManagerListedLast_ManagerStartsFirst()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        HireService service = Create(dir, out PersonaStore personas, out _, out FakeStartGate gate);
        IReadOnlyList<string> seenAtWait = [];
        gate.OnWait = _ => seenAtWait = [.. personas.ListNames()];

        StartTeamResult result = await service.StartTeamAsync(TeamName, [Request("Dan", "dev"), Request("Dee", "dev"), Request("Lee", "lead")], ct);

        Assert.True(result.Ok);
        Assert.Null(result.Error);
        Assert.Equal(["Lee", "Dan", "Dee"], result.Started);
        Assert.Equal(["Lee"], gate.Waited);
        Assert.Equal(["Lee"], seenAtWait);
    }

    /// <summary>A started Team that has no <c>Process.md</c> receives the type's default process; one that has it keeps its file.</summary>
    [Fact]
    public async Task StartTeamAsync_TeamWithoutProcess_ProvisionsDefaultProcessOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using TempDataDir teamFolder = new();
        DiskTeamFolders folders = new(teamFolder.Path) { File = new TeamFile(new VersionedRef("web", "1.2.0"), null, null, null, TeamPaths.ProcessFileName, string.Empty) };
        HireService service = Create(dir, out _, out _, out _, makeProvisioner: types => new ProcessProvisioner(types, folders));
        string path = Path.Combine(teamFolder.Path, TeamPaths.ProcessFileName);

        StartTeamResult result = await service.StartTeamAsync(TeamName, [Request("Lee", "lead")], ct);
        string first = File.ReadAllText(path);
        File.WriteAllText(path, "# Edited\n");
        HireService again = Create(dir, out _, out _, out _, makeProvisioner: types => new ProcessProvisioner(types, folders));
        _ = await again.StartTeamAsync(TeamName, [Request("Lee", "lead")], ct);

        Assert.True(result.Ok);
        Assert.Equal("# Flow", first);
        Assert.Equal("# Edited\n", File.ReadAllText(path));
    }

    /// <summary>The copier is asked once for the distinct Skills of every card, before anything is hired.</summary>
    [Fact]
    public async Task StartTeamAsync_AllCards_EnsuresDistinctSkillsOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        HireService service = Create(dir, out _, out FakeSkillCopier installer, out _);

        _ = await service.StartTeamAsync(TeamName, [Request("Lee", "lead"), Request("Dan", "dev"), Request("Dee", "dev")], ct);

        IReadOnlyList<string> asked = Assert.Single(installer.Calls);
        Assert.Equal(["tdd", "planning", "coding"], asked);
    }

    /// <summary>A copyable Skill is copied into Skills/ before the manager is hired: no Teammate exists while the copier runs
    /// (Skill Packs spec §6.10.3).</summary>
    [Fact]
    public async Task StartTeamAsync_CopiesSkillsBeforeTheManagerIsHired()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        HireService service = Create(dir, out PersonaStore personas, out FakeSkillCopier copier, out _);
        IReadOnlyList<string>? namesWhileCopying = null;
        copier.OnEnsure = _ => namesWhileCopying = [.. personas.ListNames()];

        StartTeamResult result = await service.StartTeamAsync(TeamName, [Request("Lee", "lead"), Request("Dan", "dev")], ct);

        Assert.True(result.Ok);
        Assert.NotNull(namesWhileCopying);
        Assert.Empty(namesWhileCopying);
        Assert.Equal(["Lee", "Dan"], result.Started);
    }

    /// <summary>A Needs-setup card blocks the whole start and nothing is written or installed.</summary>
    [Fact]
    public async Task StartTeamAsync_NeedsSetupCard_BlocksAndWritesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        HireService service = Create(dir, out PersonaStore personas, out FakeSkillCopier installer, out FakeStartGate gate);

        StartTeamResult result = await service.StartTeamAsync(TeamName, [Request("Lee", "lead"), Request("Dan", "dev", CardReadiness.NeedsSetup)], ct);

        Assert.False(result.Ok);
        Assert.Equal("Dan still needs setup.", result.Error);
        Assert.Empty(result.Started);
        Assert.Empty(personas.ListNames());
        Assert.Empty(installer.Calls);
        Assert.Empty(gate.Waited);
    }

    /// <summary>A start without a required role is refused and nothing is written.</summary>
    [Fact]
    public async Task StartTeamAsync_RequiredRoleMissing_BlocksAndWritesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        HireService service = Create(dir, out PersonaStore personas, out _, out _);

        StartTeamResult result = await service.StartTeamAsync(TeamName, [Request("Dan", "dev")], ct);

        Assert.False(result.Ok);
        Assert.Equal("The team needs a Lead.", result.Error);
        Assert.Empty(result.Started);
        Assert.Empty(personas.ListNames());
    }

    /// <summary>A team type whose manager is not hired is refused and nothing is written.</summary>
    [Fact]
    public async Task StartTeamAsync_NoManagerHired_BlocksAndWritesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        HireService service = Create(dir, out PersonaStore personas, out _, out _);
        HireRequest dev = Request("Dan", "dev") with { TypeId = "flat" };

        StartTeamResult result = await service.StartTeamAsync(TeamName, [dev], ct);

        Assert.False(result.Ok);
        Assert.Equal("The team needs a manager.", result.Error);
        Assert.Empty(personas.ListNames());
    }

    /// <summary>An empty request list is refused.</summary>
    [Fact]
    public async Task StartTeamAsync_NoHires_Refused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        HireService service = Create(dir, out _, out FakeSkillCopier installer, out _);

        StartTeamResult result = await service.StartTeamAsync(TeamName, [], ct);

        Assert.False(result.Ok);
        Assert.Equal("There is nobody to start.", result.Error);
        Assert.Empty(installer.Calls);
    }

    /// <summary>Requests that mix team types are refused before anything is written.</summary>
    [Fact]
    public async Task StartTeamAsync_MixedTeamTypes_BlocksAndWritesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        HireService service = Create(dir, out PersonaStore personas, out _, out _);
        HireRequest other = Request("Dan", "dev") with { TypeId = "flat" };

        StartTeamResult result = await service.StartTeamAsync(TeamName, [Request("Lee", "lead"), other], ct);

        Assert.False(result.Ok);
        Assert.Equal("Every member must belong to the same team and team type.", result.Error);
        Assert.Empty(personas.ListNames());
    }

    /// <summary>A Skill that is not ready blocks the start, is named with its reason, and no Teammate is written.</summary>
    [Fact]
    public async Task StartTeamAsync_SkillMissing_BlocksNamingIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        HireService service = Create(dir, out PersonaStore personas, out FakeSkillCopier installer, out FakeStartGate gate);
        installer.Missing = [FakeSkillCopier.NotDownloaded("coding")];

        StartTeamResult result = await service.StartTeamAsync(TeamName, [Request("Lee", "lead"), Request("Dan", "dev")], ct);

        Assert.False(result.Ok);
        Assert.Equal("Skills not ready: coding. Skill \"coding\" not downloaded.", result.Error);
        Assert.Empty(personas.ListNames());
        Assert.Empty(gate.Waited);
    }

    /// <summary>A manager that does not report running in time stops the start; the other members are never hired.</summary>
    [Fact]
    public async Task StartTeamAsync_ManagerTimesOut_StopsAndReportsIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        HireService service = Create(dir, out PersonaStore personas, out _, out FakeStartGate gate);
        gate.Running = false;

        StartTeamResult result = await service.StartTeamAsync(TeamName, [Request("Lee", "lead"), Request("Dan", "dev")], ct);

        Assert.False(result.Ok);
        Assert.Equal("Lee did not start in time.", result.Error);
        Assert.Equal(["Lee"], result.Started);
        Assert.Equal(["Lee"], personas.ListNames());
    }

    /// <summary>Cancelling while the manager starts stops the start and reports the members already hired.</summary>
    [Fact]
    public async Task StartTeamAsync_CancelledWhileWaiting_ReportsPartialStart()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        HireService service = Create(dir, out PersonaStore personas, out _, out FakeStartGate gate);
        gate.Cancel = true;

        StartTeamResult result = await service.StartTeamAsync(TeamName, [Request("Lee", "lead"), Request("Dan", "dev")], ct);

        Assert.False(result.Ok);
        Assert.Equal("The start was cancelled.", result.Error);
        Assert.Equal(["Lee"], result.Started);
        Assert.Equal(["Lee"], personas.ListNames());
    }

    /// <summary>A name already used by a member of another Team refuses the whole start before anything is written.</summary>
    [Fact]
    public async Task StartTeamAsync_NameUsedElsewhere_RefusedBeforeAnyWrite()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        HireService service = Create(dir, out PersonaStore personas, out _, out FakeStartGate gate);
        Assert.True(service.Hire(Request("Dee", "dev") with { Team = "Other Team" }).Ok);

        StartTeamResult result = await service.StartTeamAsync(TeamName, [Request("Lee", "lead"), Request("Dan", "dev"), Request("Dee", "dev"), Request("Dot", "dev")], ct);

        Assert.False(result.Ok);
        Assert.Equal("The name 'Dee' is already in use.", result.Error);
        Assert.Empty(result.Started);
        Assert.Equal(["Dee"], personas.ListNames());
        Assert.Empty(gate.Waited);
    }

    /// <summary>Two members of one batch that share a name (ignoring case) refuse the whole start before anything is written.</summary>
    [Fact]
    public async Task StartTeamAsync_DuplicateNameInBatch_RefusedBeforeAnyWrite()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        HireService service = Create(dir, out PersonaStore personas, out _, out _);

        StartTeamResult result = await service.StartTeamAsync(TeamName, [Request("Lee", "lead"), Request("Dan", "dev"), Request("dan", "dev")], ct);

        Assert.False(result.Ok);
        Assert.Equal("The name 'dan' is already in use.", result.Error);
        Assert.Empty(result.Started);
        Assert.Empty(personas.ListNames());
    }

    /// <summary>More members of a role than it allows refuse the whole start before anything is written.</summary>
    [Fact]
    public async Task StartTeamAsync_RoleOverfullInBatch_RefusedBeforeAnyWrite()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        HireService service = Create(dir, out PersonaStore personas, out _, out _);

        StartTeamResult result = await service.StartTeamAsync(TeamName,
            [Request("Lee", "lead"), Request("Dan", "dev"), Request("Dee", "dev"), Request("Dot", "dev"), Request("Dib", "dev")], ct);

        Assert.False(result.Ok);
        Assert.Equal("Team 'Web Team' already has the most Developer Teammates it allows (3).", result.Error);
        Assert.Empty(result.Started);
        Assert.Empty(personas.ListNames());
    }

    /// <summary>Existing members of the role in the Team count against the role's limit together with the batch.</summary>
    [Fact]
    public async Task StartTeamAsync_RoleFullWithExistingMembers_RefusedBeforeAnyWrite()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        HireService service = Create(dir, out PersonaStore personas, out _, out _);
        Assert.True(service.Hire(Request("Eve", "dev")).Ok);
        Assert.True(service.Hire(Request("Eli", "dev")).Ok);

        StartTeamResult result = await service.StartTeamAsync(TeamName, [Request("Lee", "lead"), Request("Dan", "dev"), Request("Dee", "dev")], ct);

        Assert.False(result.Ok);
        Assert.Equal("Team 'Web Team' already has the most Developer Teammates it allows (3).", result.Error);
        Assert.Empty(result.Started);
        Assert.Equal(["Eli", "Eve"], personas.ListNames().Order(StringComparer.Ordinal));
    }

    /// <summary>The hire limits are checked for every member at once, so a batch that exceeds the Team limit writes nobody.</summary>
    [Fact]
    public async Task StartTeamAsync_BatchExceedsTeamLimit_RefusedBeforeAnyWrite()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new();
        options.Acp.MaxTeammatesPerTeam = 2;
        HireService service = Create(dir, out PersonaStore personas, out _, out _, options);

        StartTeamResult result = await service.StartTeamAsync(TeamName, [Request("Lee", "lead"), Request("Dan", "dev"), Request("Dee", "dev")], ct);

        Assert.False(result.Ok);
        Assert.Empty(result.Started);
        Assert.Empty(personas.ListNames());
    }

    /// <summary>After the manager timed out, starting again treats the members already hired into their roles as started and hires only the rest.</summary>
    [Fact]
    public async Task StartTeamAsync_RetryAfterManagerTimeout_HiresOnlyTheRest()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        HireService service = Create(dir, out PersonaStore personas, out _, out FakeStartGate gate);
        gate.Running = false;
        List<HireRequest> hires = [Request("Lee", "lead"), Request("Dan", "dev")];
        StartTeamResult first = await service.StartTeamAsync(TeamName, hires, ct);
        Assert.False(first.Ok);
        gate.Running = true;

        StartTeamResult second = await service.StartTeamAsync(TeamName, hires, ct);

        Assert.True(second.Ok);
        Assert.Null(second.Error);
        Assert.Equal(["Lee", "Dan"], second.Started);
        Assert.Equal(["Dan", "Lee"], personas.ListNames().Order(StringComparer.Ordinal));
        Assert.Equal(["Lee", "Lee"], gate.Waited);
    }

    /// <summary>Concurrent hires of one role never exceed the role's limit, because check and write are one step.</summary>
    [Fact]
    public async Task Hire_ConcurrentHiresOfOneRole_NeverExceedTheRoleLimit()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        HireService service = Create(dir, out PersonaStore personas, out _, out _);
        string[] names = ["Ann", "Bob", "Cat", "Dot", "Eve", "Fay", "Gus", "Hal"];

        HireResult[] results = await Task.WhenAll(names.Select(name => Task.Run(() => service.Hire(Request(name, "dev")), ct)));

        Assert.Equal(3, results.Count(static r => r.Ok));
        Assert.Equal(3, personas.ListNames().Count);
    }

    /// <summary>A file system failure while writing the Teammate is a refusal, not an exception.</summary>
    [Fact]
    public void Hire_TeammateCannotBeWritten_RefusesInsteadOfThrowing()
    {
        using TempDataDir dir = new();
        HireService service = Create(dir, out PersonaStore personas, out _, out _);
        TeammatePaths paths = new(dir.Options());
        Directory.CreateDirectory(paths.DefinitionsRoot);
        File.WriteAllText(paths.TeammateFolder("Dan"), "in the way");

        HireResult result = service.Hire(Request("Dan", "dev"));

        Assert.False(result.Ok);
        Assert.Equal("Dan could not be hired: the Teammate file could not be written.", result.Error);
        Assert.Null(result.PersonaName);
        Assert.Empty(personas.ListNames());
    }

    /// <summary>A failure writing a later member stops the start and still reports who started.</summary>
    [Fact]
    public async Task StartTeamAsync_LaterWriteFails_ReportsStartedSoFar()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        HireService service = Create(dir, out _, out _, out FakeStartGate gate);
        TeammatePaths paths = new(dir.Options());
        gate.OnWait = _ => File.WriteAllText(paths.TeammateFolder("Dan"), "in the way");

        StartTeamResult result = await service.StartTeamAsync(TeamName, [Request("Lee", "lead"), Request("Dan", "dev")], ct);

        Assert.False(result.Ok);
        Assert.NotNull(result.Error);
        Assert.Equal(["Lee"], result.Started);
    }

    private const string ProcessWithOnboarding = "# Process\n\n## Onboarding\n\n- Read the workspace.\n- Run the tests.\n";

    /// <summary>The manager's first Turn is the onboarding prompt with the process's steps, posted once the manager runs.</summary>
    [Fact]
    public async Task StartTeamAsync_ProcessWithOnboarding_PostsStepsToManagerOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using TempDataDir teamFolder = new();
        File.WriteAllText(Path.Combine(teamFolder.Path, TeamPaths.ProcessFileName), ProcessWithOnboarding);
        DiskTeamFolders folders = new(teamFolder.Path);
        FakeOnboardingPoster poster = new();
        HireService service = Create(dir, out _, out _, out _, makeOnboarding: _ => new TeamOnboarding(folders, new FakePromptSource(), poster));

        StartTeamResult result = await service.StartTeamAsync(TeamName, [Request("Lee", "lead"), Request("Dan", "dev")], ct);

        Assert.True(result.Ok);
        (string manager, string text) = Assert.Single(poster.Posts);
        Assert.Equal("Lee", manager);
        Assert.Equal("Your Team has just started. Work through these onboarding steps, then tell the Human what you found and propose a starter Task:\n\n- Read the workspace.\n- Run the tests.", text);
        Assert.True(folders.File?.Onboarded);
    }

    /// <summary>T20: onboarding posts through the root <c>PostAsync</c> and never through the Team-aware call, so it stays in the manager's root one-to-one.</summary>
    [Fact]
    public async Task StartTeamAsync_Onboarding_UsesRootPostNotTeamPost()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using TempDataDir teamFolder = new();
        File.WriteAllText(Path.Combine(teamFolder.Path, TeamPaths.ProcessFileName), ProcessWithOnboarding);
        DiskTeamFolders folders = new(teamFolder.Path);
        FakeOnboardingPoster poster = new();
        HireService service = Create(dir, out _, out _, out _, makeOnboarding: _ => new TeamOnboarding(folders, new FakePromptSource(), poster));

        StartTeamResult result = await service.StartTeamAsync(TeamName, [Request("Lee", "lead"), Request("Dan", "dev")], ct);

        Assert.True(result.Ok);
        Assert.Single(poster.Posts);
        Assert.Empty(poster.TeamPosts);
    }

    /// <summary>A Team whose file already carries the marker is not onboarded again after a restart.</summary>
    [Fact]
    public async Task StartTeamAsync_MarkerPresent_PostsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using TempDataDir teamFolder = new();
        File.WriteAllText(Path.Combine(teamFolder.Path, TeamPaths.ProcessFileName), ProcessWithOnboarding);
        DiskTeamFolders folders = new(teamFolder.Path);
        FakeOnboardingPoster poster = new();
        HireService first = Create(dir, out _, out _, out _, makeOnboarding: _ => new TeamOnboarding(folders, new FakePromptSource(), poster));
        _ = await first.StartTeamAsync(TeamName, [Request("Lee", "lead")], ct);
        using TempDataDir secondDir = new();
        HireService second = Create(secondDir, out _, out _, out _, makeOnboarding: _ => new TeamOnboarding(folders, new FakePromptSource(), poster));

        StartTeamResult result = await second.StartTeamAsync(TeamName, [Request("Lee", "lead")], ct);

        Assert.True(result.Ok);
        Assert.Single(poster.Posts);
    }

    /// <summary>A process with no onboarding steps posts nothing and leaves the marker unset.</summary>
    [Fact]
    public async Task StartTeamAsync_ProcessWithoutOnboarding_PostsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using TempDataDir teamFolder = new();
        File.WriteAllText(Path.Combine(teamFolder.Path, TeamPaths.ProcessFileName), "# Process\n");
        DiskTeamFolders folders = new(teamFolder.Path);
        FakeOnboardingPoster poster = new();
        HireService service = Create(dir, out _, out _, out _, makeOnboarding: _ => new TeamOnboarding(folders, new FakePromptSource(), poster));

        _ = await service.StartTeamAsync(TeamName, [Request("Lee", "lead")], ct);

        Assert.Empty(poster.Posts);
        Assert.False(folders.File?.Onboarded);
    }

    /// <summary>A poster that cannot reach the manager leaves the marker unset, so the next start tries again.</summary>
    [Fact]
    public async Task StartTeamAsync_PostFails_MarkerStaysUnset()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using TempDataDir teamFolder = new();
        File.WriteAllText(Path.Combine(teamFolder.Path, TeamPaths.ProcessFileName), ProcessWithOnboarding);
        DiskTeamFolders folders = new(teamFolder.Path);
        FakeOnboardingPoster poster = new() { Delivers = false };
        HireService service = Create(dir, out _, out _, out _, makeOnboarding: _ => new TeamOnboarding(folders, new FakePromptSource(), poster));

        StartTeamResult result = await service.StartTeamAsync(TeamName, [Request("Lee", "lead")], ct);

        Assert.True(result.Ok);
        Assert.False(folders.File?.Onboarded);
    }
}

/// <summary>An onboarding poster that records what it was asked to post.</summary>
internal sealed class FakeOnboardingPoster : IOnboardingPoster
{
    /// <summary>Gets the posts, each the manager's name and the text.</summary>
    public List<(string Manager, string Text)> Posts { get; } = [];

    /// <summary>Gets the Team-aware posts, each the Team, the recipient's name and the text.</summary>
    public List<(string Team, string Recipient, string Text)> TeamPosts { get; } = [];

    /// <summary>Gets or sets whether the post reaches the manager.</summary>
    public bool Delivers { get; set; } = true;

    /// <inheritdoc />
    public Task<bool> PostToTeamAsync(string team, string recipientName, string text, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        this.TeamPosts.Add((team, recipientName, text));
        return Task.FromResult(this.Delivers);
    }

    /// <inheritdoc />
    public Task<bool> PostAsync(string managerName, string text, CancellationToken ct)
    {
        this.Posts.Add((managerName, text));
        return Task.FromResult(this.Delivers);
    }
}
