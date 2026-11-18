using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions.TurnHooks;
using Agency.Huddle.App.AgentsMdChanges;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.Tests.Acp;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Teams;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.FileChanges;

/// <summary>Pins that <see cref="FileChangesTurnHook"/> hides a Playbook file only for an Agent in that Team (Team Playbooks spec §6.4).</summary>
public sealed class FileChangesPlaybookNoTeamTests
{
    /// <summary>An Agent in no Team has no Team folder, so a <c>_member-playbook.md</c> change still reaches it as a File Change.</summary>
    [Fact]
    public async Task PlaybookFileEdit_AgentInNoTeam_StaysInTheReport()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        IOptions<TeamOptions> options = dataDir.Options();
        AgentPaths agentPaths = new(options);
        TestPersonaFiles.Write(agentPaths, "loner", "---\nName: Loner\nTitle: Loner\nAlias: Loner\n---\nYou are Loner.");
        SqliteTeamDirectory directory = new(options);
        await directory.InitializeAsync("You", ct);
        using PersonaStore personas = new(
            agentPaths,
            new PersonaModelStore(options),
            new PersonaEffortStore(options),
            new PersonaWorkModeStore(options),
            NullLogger<PersonaStore>.Instance);
        FileChangeTracker tracker = new(new FileStateStore(options, NullLogger<FileStateStore>.Instance), personas, directory, new WatchedFolderResolver(options, agentPaths), options, agentPaths, NullLogger<FileChangeTracker>.Instance);
        string teamsRoot = Path.GetFullPath(Path.Combine(options.Value.DataDir, options.Value.Teams.Dir));
        Directory.CreateDirectory(teamsRoot);
        FileChangesTurnHook hook = new(tracker, new FakePromptSource(), NullLogger<FileChangesTurnHook>.Instance, new AgentsMdScopes(personas, new FakeTeamCatalog(), options));
        string roomId = (await directory.CreateRoomAsync("RoomA", [KnownIds.Human], ct)).Id;
        TurnHookScope scope = new("Loner", true, true, [teamsRoot]);
        TurnAddition? first = await hook.BeforeTurnAsync(new TurnStarting(scope, roomId, null), ct);
        await ((ITurnHook)hook).AfterTurnAsync(new TurnEnded(scope, roomId, null, []), first, ct);
        string playbook = Path.Combine(teamsRoot, "Home", "_member-playbook.md");
        Directory.CreateDirectory(Path.GetDirectoryName(playbook) ?? teamsRoot);
        File.WriteAllText(playbook, "Own way of working");

        TurnAddition? addition = await hook.BeforeTurnAsync(new TurnStarting(scope, roomId, null), ct);

        Assert.NotNull(addition);
        Assert.Contains(playbook, addition.Text, StringComparison.Ordinal); // contains-ok: prompt text, not markup.
    }
}
