using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Agents;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// Tests for <see cref="HireService"/> beyond the draft path: onboarding the manager through <see cref="HireService.OnboardManagerAsync"/>,
/// <see cref="HireService.ProvisionProcess"/>, and the single <see cref="HireService.Hire"/> (role limit under concurrency, a failed write).
/// </summary>
public sealed class HireServiceOnboardingTests
{
    private const string TeamName = "Web Team";

    private static readonly string Manifest = Factories.TeamTypeJson.Manifest("web", "Web", order: 1, version: "1.2.0", rolesJson: """
        [
            { "id": "lead", "title": "Lead", "candidate": "candidates/lead.md", "manager": true, "required": true },
            { "id": "dev", "title": "Developer", "candidate": "candidates/dev.md", "maxPerTeam": 3 }
        ]
        """);

    private static HireService Create(TempDataDir dir, out PersonaStore personas, out FakeSkillCopier installer, out FakeStartGate gate, Func<TeamTypeCatalog, ProcessProvisioner>? makeProvisioner = null, Func<TeamTypeCatalog, TeamOnboarding>? makeOnboarding = null)
    {
        personas = new(new AgentPaths(dir.Options()), new PersonaModelStore(dir.Options()), new PersonaEffortStore(dir.Options()), new PersonaWorkModeStore(dir.Options()), NullLogger<PersonaStore>.Instance);
        Dictionary<string, string> builtins = new(StringComparer.Ordinal)
        {
            ["web/team-type.json"] = Manifest,
            ["web/candidates/lead.md"] = "---\ntitle: Lead\nconsult_when: planning\n---\nYou lead.",
            ["web/candidates/dev.md"] = "---\ntitle: Developer\nconsult_when: code\n---\nYou build.",
            ["web/processes/flow.md"] = "# Flow",
        };
        TeamTypeCatalog types = new(dir.Options(), NullLogger<TeamTypeCatalog>.Instance, builtins);
        FakeSkillAvailability packs = new();
        HireLimitHarness harness = new(Microsoft.Extensions.Options.Options.Create(new TeamOptions()));
        AvatarStore avatars = new(dir.Options(), NullLogger<AvatarStore>.Instance);
        installer = new FakeSkillCopier();
        gate = new FakeStartGate();
        return new HireService(personas, types, packs, avatars, harness.Guard, installer, gate, NullLogger<HireService>.Instance, makeProvisioner?.Invoke(types), makeOnboarding?.Invoke(types));
    }

    private static HireRequest Request(string name, string roleId, CardReadiness readiness = CardReadiness.Ready)
    {
        CardState card = new(roleId, name, new ModelPick("claude-agent-acp", "sonnet"),
            [new SkillRef("tdd", "builtin", null), new SkillRef(roleId == "lead" ? "planning" : "coding", "builtin", null)],
            [], readiness, [], [], []);
        return new HireRequest(TeamName, "web", roleId, card);
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

    /// <summary>A file system failure while writing the Agent is a refusal, not an exception.</summary>
    [Fact]
    public void Hire_AgentCannotBeWritten_RefusesInsteadOfThrowing()
    {
        using TempDataDir dir = new();
        HireService service = Create(dir, out PersonaStore personas, out _, out _);
        AgentPaths paths = new(dir.Options());
        Directory.CreateDirectory(paths.DefinitionsRoot);
        File.WriteAllText(paths.AgentFolder("Dan"), "in the way");

        HireResult result = service.Hire(Request("Dan", "dev"));

        Assert.False(result.Ok);
        Assert.Equal("Dan could not be hired: the Agent file could not be written.", result.Error);
        Assert.Null(result.PersonaName);
        Assert.Empty(personas.ListNames());
    }

    private const string ProcessWithOnboarding = "# Process\n\n## Onboarding\n\n- Read the workspace.\n- Run the tests.\n";

    /// <summary>The manager's first Turn is the onboarding prompt with the process's steps, posted once the manager runs.</summary>
    [Fact]
    public async Task OnboardManagerAsync_ProcessWithOnboarding_PostsStepsToManagerOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using TempDataDir teamFolder = new();
        File.WriteAllText(Path.Combine(teamFolder.Path, TeamPaths.AgentsMdFileName), ProcessWithOnboarding);
        DiskTeamFolders folders = new(teamFolder.Path);
        FakeOnboardingPoster poster = new();
        HireService service = Create(dir, out _, out _, out _, makeOnboarding: _ => new TeamOnboarding(folders, new FakePromptSource(), poster));

        bool posted = await service.OnboardManagerAsync(TeamName, "Lee", ct);

        Assert.True(posted);
        (string team, string manager, string text) = Assert.Single(poster.OneToOnePosts);
        Assert.Equal(TeamName, team);
        Assert.Empty(poster.Posts);
        Assert.Equal("Lee", manager);
        Assert.Equal("Your Team has just started. Work through these onboarding steps, then tell the Human what you found:\n\n- Read the workspace.\n- Run the tests.\n\nEnd by suggesting one starter Task in your reply, and ask the Human whether to start it. Do not create a Task or assign anyone until the Human says yes.", text);
        Assert.True(folders.File?.Onboarded);
    }

    /// <summary>T20: onboarding posts through the Team one-to-one call, never the root <c>PostAsync</c> or the Team Room call, so it lands where the Team page's Message button opens.</summary>
    [Fact]
    public async Task OnboardManagerAsync_Onboarding_UsesTeamOneToOneNotRootOrTeamRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using TempDataDir teamFolder = new();
        File.WriteAllText(Path.Combine(teamFolder.Path, TeamPaths.AgentsMdFileName), ProcessWithOnboarding);
        DiskTeamFolders folders = new(teamFolder.Path);
        FakeOnboardingPoster poster = new();
        HireService service = Create(dir, out _, out _, out _, makeOnboarding: _ => new TeamOnboarding(folders, new FakePromptSource(), poster));

        bool posted = await service.OnboardManagerAsync(TeamName, "Lee", ct);

        Assert.True(posted);
        Assert.Single(poster.OneToOnePosts);
        Assert.Empty(poster.Posts);
        Assert.Empty(poster.TeamPosts);
    }

    /// <summary>A Team whose file already carries the marker is not onboarded again after a restart.</summary>
    [Fact]
    public async Task OnboardManagerAsync_MarkerPresent_PostsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using TempDataDir teamFolder = new();
        File.WriteAllText(Path.Combine(teamFolder.Path, TeamPaths.AgentsMdFileName), ProcessWithOnboarding);
        DiskTeamFolders folders = new(teamFolder.Path);
        FakeOnboardingPoster poster = new();
        HireService first = Create(dir, out _, out _, out _, makeOnboarding: _ => new TeamOnboarding(folders, new FakePromptSource(), poster));
        bool firstPosted = await first.OnboardManagerAsync(TeamName, "Lee", ct);
        using TempDataDir secondDir = new();
        HireService second = Create(secondDir, out _, out _, out _, makeOnboarding: _ => new TeamOnboarding(folders, new FakePromptSource(), poster));

        bool secondPosted = await second.OnboardManagerAsync(TeamName, "Lee", ct);

        Assert.True(firstPosted);
        Assert.False(secondPosted);
        Assert.Single(poster.OneToOnePosts);
        Assert.Empty(poster.Posts);
    }

    /// <summary>A process with no onboarding steps posts nothing and leaves the marker unset.</summary>
    [Fact]
    public async Task OnboardManagerAsync_ProcessWithoutOnboarding_PostsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using TempDataDir teamFolder = new();
        File.WriteAllText(Path.Combine(teamFolder.Path, TeamPaths.AgentsMdFileName), "# Process\n");
        DiskTeamFolders folders = new(teamFolder.Path);
        FakeOnboardingPoster poster = new();
        HireService service = Create(dir, out _, out _, out _, makeOnboarding: _ => new TeamOnboarding(folders, new FakePromptSource(), poster));

        bool posted = await service.OnboardManagerAsync(TeamName, "Lee", ct);

        Assert.False(posted);
        Assert.Empty(poster.Posts);
        Assert.Empty(poster.OneToOnePosts);
        Assert.False(folders.File?.Onboarded);
    }

    /// <summary>A poster that cannot reach the manager leaves the marker unset, so the next start tries again.</summary>
    [Fact]
    public async Task OnboardManagerAsync_PostFails_MarkerStaysUnset()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using TempDataDir teamFolder = new();
        File.WriteAllText(Path.Combine(teamFolder.Path, TeamPaths.AgentsMdFileName), ProcessWithOnboarding);
        DiskTeamFolders folders = new(teamFolder.Path);
        FakeOnboardingPoster poster = new() { Delivers = false };
        HireService service = Create(dir, out _, out _, out _, makeOnboarding: _ => new TeamOnboarding(folders, new FakePromptSource(), poster));

        bool posted = await service.OnboardManagerAsync(TeamName, "Lee", ct);

        Assert.False(posted);
        Assert.False(folders.File?.Onboarded);
    }

    /// <summary>Build a team's onboarding waits for the manager, posts the steps once, and a second call finds the marker and posts nothing.</summary>
    [Fact]
    public async Task OnboardManagerAsync_ManagerRuns_PostsOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using TempDataDir teamFolder = new();
        File.WriteAllText(Path.Combine(teamFolder.Path, TeamPaths.AgentsMdFileName), ProcessWithOnboarding);
        DiskTeamFolders folders = new(teamFolder.Path);
        FakeOnboardingPoster poster = new();
        HireService service = Create(dir, out _, out _, out FakeStartGate gate, makeOnboarding: _ => new TeamOnboarding(folders, new FakePromptSource(), poster));

        bool first = await service.OnboardManagerAsync(TeamName, "Lee", ct);
        bool second = await service.OnboardManagerAsync(TeamName, "Lee", ct);

        Assert.True(first);
        Assert.False(second);
        Assert.Equal("Lee", Assert.Single(poster.OneToOnePosts).Manager);
        Assert.Empty(poster.Posts);
        Assert.Equal(["Lee", "Lee"], gate.Waited);
    }

    /// <summary>A manager that does not start posts nothing and leaves the Team not onboarded; a wait that throws does the same, because nothing awaits this call.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OnboardManagerAsync_ManagerDoesNotStart_PostsNothing(bool waitThrows)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using TempDataDir teamFolder = new();
        File.WriteAllText(Path.Combine(teamFolder.Path, TeamPaths.AgentsMdFileName), ProcessWithOnboarding);
        DiskTeamFolders folders = new(teamFolder.Path);
        FakeOnboardingPoster poster = new();
        HireService service = Create(dir, out _, out _, out FakeStartGate gate, makeOnboarding: _ => new TeamOnboarding(folders, new FakePromptSource(), poster));
        gate.Running = false;
        gate.Cancel = waitThrows;

        bool posted = await service.OnboardManagerAsync(TeamName, "Lee", ct);

        Assert.False(posted);
        Assert.Empty(poster.Posts);
        Assert.Empty(poster.OneToOnePosts);
        Assert.False(folders.File?.Onboarded);
    }

    /// <summary>A chosen process is the one written; with no choice the type's default is.</summary>
    [Theory]
    [InlineData("flow", true)]
    [InlineData(null, true)]
    [InlineData("nope", false)]
    public void ProvisionProcess_WritesTheChosenOrDefaultProcess(string? processId, bool written)
    {
        using TempDataDir dir = new();
        using TempDataDir teamFolder = new();
        DiskTeamFolders folders = new(teamFolder.Path);
        HireService service = Create(dir, out _, out _, out _, makeProvisioner: types => new ProcessProvisioner(types, folders));

        ProvisionResult result = service.ProvisionProcess(TeamName, processId);

        Assert.Equal(written ? ProvisionResult.Created : ProvisionResult.UnknownProcess, result);
        Assert.Equal(written, File.Exists(Path.Combine(teamFolder.Path, TeamPaths.AgentsMdFileName)));
    }
}

/// <summary>An onboarding poster that records what it was asked to post.</summary>
internal sealed class FakeOnboardingPoster : IOnboardingPoster
{
    /// <summary>Gets the posts, each the manager's name and the text.</summary>
    public List<(string Manager, string Text)> Posts { get; } = [];

    /// <summary>Gets the Team-aware posts, each the Team, the recipient's name and the text.</summary>
    public List<(string Team, string Recipient, string Text)> TeamPosts { get; } = [];

    /// <summary>Gets the posts into a manager's Team-scoped one-to-one, each the Team, the manager's name and the text.</summary>
    public List<(string Team, string Manager, string Text)> OneToOnePosts { get; } = [];

    /// <summary>Gets or sets whether the post reaches the manager.</summary>
    public bool Delivers { get; set; } = true;

    /// <inheritdoc />
    public Task<bool> PostToTeamOneToOneAsync(string team, string managerName, string text, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        this.OneToOnePosts.Add((team, managerName, text));
        return Task.FromResult(this.Delivers);
    }

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
