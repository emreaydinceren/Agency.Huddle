using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Agents;
using Agency.Huddle.Tests.Ui.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// The <c>one-in-progress-per-assignee</c> gate (ADR-0102): a change that would leave an assignee with a second Task
/// In Progress is refused, and the refusal names the Task already In Progress.
/// </summary>
public sealed class ProcessGateServiceOneInProgressTests
{
    private const string Team = "Apollo";
    private const string OneGate = "'one-in-progress-per-assignee'";

    private static readonly IReadOnlyDictionary<string, string> NoCovers = new Dictionary<string, string>(StringComparer.Ordinal);
    private static readonly OtherInProgress Busy = new("Nova", new TaskId("DEVT", 1), "Build the API");

    /// <summary>A move into In Progress for an assignee who already has a Task there is refused with the exact words, naming the first Task.</summary>
    [Fact]
    public void CheckTaskChange_SecondMoveIntoInProgress_IsRefusedNamingTheFirst()
    {
        using TempDataDir dir = new();
        ProcessGateService service = Build(dir, Process(OneGate));

        GateRefusal? refusal = service.CheckTaskChange(Context("To Do", "In Progress", Busy));

        Assert.Equal(GateId.OneInProgressPerAssignee, refusal?.Gate);
        Assert.Equal(
            "Gate one-in-progress-per-assignee: Nova already has DEVT-0001 \"Build the API\" In Progress. Finish it or move it out of In Progress before starting another.",
            refusal?.Message);
    }

    /// <summary>Giving an In Progress Task to someone busy (status unchanged) is refused by this gate alone, even when another gate would refuse a move into In Progress.</summary>
    [Fact]
    public void CheckTaskChange_ReassignInProgress_IsRefusedByThisGateOnly()
    {
        using TempDataDir dir = new();
        ProcessGateService service = Build(dir, Process(OneGate + ", 'blockers-done-before-start'"));

        GateRefusal? refusal = service.CheckTaskChange(Context("In Progress", "In Progress", Busy));

        Assert.Equal(GateId.OneInProgressPerAssignee, refusal?.Gate);
    }

    /// <summary>A reassignment of an In Progress Task to someone free passes, even with an open blocker, because the status does not change.</summary>
    [Fact]
    public void CheckTaskChange_ReassignInProgressToFreeAssignee_IsAllowed()
    {
        using TempDataDir dir = new();
        ProcessGateService service = Build(dir, Process(OneGate + ", 'blockers-done-before-start'"));
        GateContext context = Context("In Progress", "In Progress", null) with { OpenBlockers = [new OpenBlocker(new TaskId("DEVT", 4), TaskState.Backlog)] };

        Assert.Null(service.CheckTaskChange(context));
    }

    /// <summary>With no other In Progress Task the move passes.</summary>
    [Fact]
    public void CheckTaskChange_NoOtherInProgress_IsAllowed()
    {
        using TempDataDir dir = new();
        ProcessGateService service = Build(dir, Process(OneGate));

        Assert.Null(service.CheckTaskChange(Context("To Do", "In Progress", null)));
    }

    /// <summary>A move out of In Progress is never refused, whoever holds another Task.</summary>
    /// <param name="to">The proposed status.</param>
    [Theory]
    [InlineData("To Do")]
    [InlineData("Backlog")]
    [InlineData("Needs You")]
    [InlineData("Done")]
    public void CheckTaskChange_MoveOutOfInProgress_IsAllowed(string to)
    {
        using TempDataDir dir = new();
        ProcessGateService service = Build(dir, Process(OneGate));

        Assert.Null(service.CheckTaskChange(Context("In Progress", to, Busy)));
    }

    /// <summary>A Team whose <c>_team.md</c> does not list the gate lets a second Task start.</summary>
    [Fact]
    public void CheckTaskChange_GateOff_IsAllowed()
    {
        using TempDataDir dir = new();
        ProcessGateService service = Build(dir, Process("'qa-evidence-before-done'"));

        Assert.Null(service.CheckTaskChange(Context("To Do", "In Progress", Busy)));
    }

    /// <summary>The gate id parses from <c>_team.md</c> and round-trips through its kebab-case id, name and risk.</summary>
    [Fact]
    public void TryParseGate_OneInProgressGateId_IsKnown()
    {
        Assert.True(ProcessDocument.TryParseGate("one-in-progress-per-assignee", out GateId gate));
        Assert.Equal(GateId.OneInProgressPerAssignee, gate);
        Assert.Equal("one-in-progress-per-assignee", ProcessDocument.ToId(gate));
        Assert.Equal("One In Progress per assignee", ProcessDocument.DisplayName(gate));
        Assert.Equal("A person can then have several Tasks In Progress at once.", ProcessGateService.GateRisk(gate));
    }

    private static string Process(string gates) =>
        $"---\nteamType: 'web@1.2.0'\ngates: [{gates}]\n---\n";

    private static GateContext Context(string from, string to, OtherInProgress? other) =>
        new(Team, new TaskId("DEVT", 2), from, to, false, true, [], NoCovers, "Nova") { OtherInProgress = other };

    private static ProcessGateService Build(TempDataDir dir, string teamFileText)
    {
        VersionedRef type = new("web", "1.2.0");
        LibraryRoot root = new("teams", "Teams", dir.Path, LibraryRootKind.Teams);
        TeamFile teamFile = TeamFile.TryParse(teamFileText, out TeamFile? parsed) ? parsed : new TeamFile(type, null, null, null, string.Empty);
        FakeTeamFolders folders = new()
        {
            TeamResult = LibraryResult<LibraryPath>.Ok(new LibraryPath(root, Team, dir.Path, LibraryNodeRole.TeamFolder)),
            ReadResult = teamFile,
        };
        FakeTeamCatalog catalog = new() { Teams = [new TeamSummary(Team, [], [], true) { TeamType = type }] };
        TeamTypeCatalog types = RoleFixtures.Types(dir);
        return new ProcessGateService(catalog, folders, types);
    }
}
