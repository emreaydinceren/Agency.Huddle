using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Teammates;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Teams;

/// <summary>A skill installer that records what it was asked for and reports a configured set as missing.</summary>
internal sealed class FakeSkillInstaller : ISkillInstaller
{
    /// <summary>Gets the calls, each the skills asked for.</summary>
    public List<IReadOnlyList<SkillRef>> Calls { get; } = [];

    /// <summary>Gets or sets the skills reported as not installed.</summary>
    public IReadOnlyList<SkillRef> Missing { get; set; } = [];

    /// <inheritdoc />
    public Task<IReadOnlyList<SkillRef>> EnsureInstalledAsync(IReadOnlyList<SkillRef> skills, CancellationToken ct)
    {
        this.Calls.Add(skills);
        return Task.FromResult(this.Missing);
    }
}

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

    private static HireService Create(TempDataDir dir, out PersonaStore personas, out FakeSkillInstaller installer, out FakeStartGate gate, TeamOptions? options = null)
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
        installer = new FakeSkillInstaller();
        gate = new FakeStartGate();
        return new HireService(personas, types, packs, avatars, harness.Guard, installer, gate, NullLogger<HireService>.Instance);
    }

    private static HireRequest Request(string name, string roleId, CardReadiness readiness = CardReadiness.Ready)
    {
        CardState card = new(roleId, name, new ModelPick("claude-agent-acp", "sonnet"),
            [new SkillRef("tdd", "builtin", null), new SkillRef(roleId == "lead" ? "planning" : "coding", "builtin", null)],
            [], readiness, []);
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

    /// <summary>The installer is asked once for the distinct skills of every card, before anything is hired.</summary>
    [Fact]
    public async Task StartTeamAsync_AllCards_InstallsDistinctSkillsOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        HireService service = Create(dir, out _, out FakeSkillInstaller installer, out _);

        _ = await service.StartTeamAsync(TeamName, [Request("Lee", "lead"), Request("Dan", "dev"), Request("Dee", "dev")], ct);

        IReadOnlyList<SkillRef> asked = Assert.Single(installer.Calls);
        Assert.Equal(["tdd", "planning", "coding"], asked.Select(static s => s.Name));
    }

    /// <summary>A Needs-setup card blocks the whole start and nothing is written or installed.</summary>
    [Fact]
    public async Task StartTeamAsync_NeedsSetupCard_BlocksAndWritesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        HireService service = Create(dir, out PersonaStore personas, out FakeSkillInstaller installer, out FakeStartGate gate);

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
        HireService service = Create(dir, out _, out FakeSkillInstaller installer, out _);

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

    /// <summary>A skill that cannot be installed blocks the start and is named.</summary>
    [Fact]
    public async Task StartTeamAsync_SkillMissing_BlocksNamingIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        HireService service = Create(dir, out PersonaStore personas, out FakeSkillInstaller installer, out FakeStartGate gate);
        installer.Missing = [new SkillRef("coding", "builtin", null)];

        StartTeamResult result = await service.StartTeamAsync(TeamName, [Request("Lee", "lead"), Request("Dan", "dev")], ct);

        Assert.False(result.Ok);
        Assert.Equal("Skills not installed: coding.", result.Error);
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
}
