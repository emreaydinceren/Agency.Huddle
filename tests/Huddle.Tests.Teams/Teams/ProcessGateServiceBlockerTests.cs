using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Agents;
using Agency.Huddle.Tests.Ui.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// The <c>blockers-done-before-start</c> gate (#538): a Task whose stored blockers are not all finished cannot move to
/// In Progress, Review, Needs You or Done, and the refusal names each open blocker.
/// </summary>
public sealed class ProcessGateServiceBlockerTests
{
    private const string Team = "Apollo";
    private const string BlockerGate = "'blockers-done-before-start'";

    private static readonly IReadOnlyDictionary<string, string> NoCovers = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Each refused status is refused while a blocker waits in Needs You, and the refusal names the blocker and its status.</summary>
    /// <param name="to">The proposed status.</param>
    [Theory]
    [InlineData("In Progress")]
    [InlineData("Needs You")]
    [InlineData("Done")]
    public void CheckTaskChange_OpenBlocker_IsRefusedNamingIt(string to)
    {
        using TempDataDir dir = new();
        ProcessGateService service = Build(dir, Process(BlockerGate));

        GateRefusal? refusal = service.CheckTaskChange(Context("To Do", to, [new OpenBlocker(new TaskId("DEVT", 1), TaskState.NeedsYou)]));

        Assert.Equal(GateId.BlockersDoneBeforeStart, refusal?.Gate);
        Assert.Equal(
            "Gate blockers-done-before-start: this Task is blocked by DEVT-0001 (Needs You). A blocked Task waits until every blocker is Done, Cancelled, Duplicate or Rejected, and for the manager's go-ahead.",
            refusal?.Message);
    }

    /// <summary>Two open blockers are both named, in the order given.</summary>
    [Fact]
    public void CheckTaskChange_TwoOpenBlockers_NamesBoth()
    {
        using TempDataDir dir = new();
        ProcessGateService service = Build(dir, Process(BlockerGate));

        GateRefusal? refusal = service.CheckTaskChange(Context(
            "Backlog",
            "In Progress",
            [new OpenBlocker(new TaskId("DEVT", 1), TaskState.InProgress), new OpenBlocker(new TaskId("DEVT", 4), TaskState.Backlog)]));

        Assert.Contains("blocked by DEVT-0001 (In Progress), DEVT-0004 (Backlog).", refusal?.Message, StringComparison.Ordinal); // contains-ok: pins the list order and separator; the full sentence is pinned in the theory above
    }

    /// <summary>Moving to Backlog or To Do is never refused by the gate: a blocked Task may be planned.</summary>
    /// <param name="to">The proposed status.</param>
    [Theory]
    [InlineData("To Do")]
    [InlineData("Backlog")]
    [InlineData("Cancelled")]
    public void CheckTaskChange_OpenBlockerToUngatedStatus_IsAllowed(string to)
    {
        using TempDataDir dir = new();
        ProcessGateService service = Build(dir, Process(BlockerGate));

        Assert.Null(service.CheckTaskChange(Context("Backlog", to, [new OpenBlocker(new TaskId("DEVT", 1), TaskState.InProgress)])));
    }

    /// <summary>A Task with no open blocker passes the gate.</summary>
    [Fact]
    public void CheckTaskChange_NoOpenBlocker_IsAllowed()
    {
        using TempDataDir dir = new();
        ProcessGateService service = Build(dir, Process(BlockerGate));

        Assert.Null(service.CheckTaskChange(Context("To Do", "In Progress", [])));
    }

    /// <summary>A Team without the gate lets a blocked Task start, as before #538.</summary>
    [Fact]
    public void CheckTaskChange_GateOff_IsAllowed()
    {
        using TempDataDir dir = new();
        ProcessGateService service = Build(dir, Process("'qa-evidence-before-done'"));

        Assert.Null(service.CheckTaskChange(Context("To Do", "In Progress", [new OpenBlocker(new TaskId("DEVT", 1), TaskState.InProgress)])));
    }

    /// <summary>The gate id parses from <c>_team.md</c> and round-trips through its kebab-case id.</summary>
    [Fact]
    public void TryParseGate_BlockerGateId_IsKnown()
    {
        Assert.True(ProcessDocument.TryParseGate("blockers-done-before-start", out GateId gate));
        Assert.Equal(GateId.BlockersDoneBeforeStart, gate);
        Assert.Equal("blockers-done-before-start", ProcessDocument.ToId(gate));
        Assert.Equal("Blockers done before start", ProcessDocument.DisplayName(gate));
        Assert.Equal("A Task can then start, or reach Done, while a Task it is blocked by is still open.", ProcessGateService.GateRisk(gate));
    }

    /// <summary>The manager the Team's catalog entry names is returned, and a Team with none returns null.</summary>
    [Fact]
    public void ManagerOf_ReadsTheCatalog()
    {
        using TempDataDir dir = new();
        ProcessGateService service = Build(dir, Process(BlockerGate), manager: "Rhea");

        Assert.Equal("Rhea", service.ManagerOf(Team));
        Assert.Null(service.ManagerOf("Nowhere"));
    }

    private static string Process(string gates) =>
        $"---\nteamType: 'web@1.2.0'\ngates: [{gates}]\n---\n";

    private static GateContext Context(string from, string to, IReadOnlyList<OpenBlocker> open) =>
        new(Team, new TaskId("DEVT", 2), from, to, false, true, [], NoCovers, "Jane") { OpenBlockers = open };

    private static ProcessGateService Build(TempDataDir dir, string teamFileText, string? manager = null)
    {
        VersionedRef type = new("web", "1.2.0");
        LibraryRoot root = new("teams", "Teams", dir.Path, LibraryRootKind.Teams);
        TeamFile teamFile = TeamFile.TryParse(teamFileText, out TeamFile? parsed) ? parsed : new TeamFile(type, null, null, null, string.Empty);
        FakeTeamFolders folders = new()
        {
            TeamResult = LibraryResult<LibraryPath>.Ok(new LibraryPath(root, Team, dir.Path, LibraryNodeRole.TeamFolder)),
            ReadResult = teamFile,
        };
        FakeTeamCatalog catalog = new() { Teams = [new TeamSummary(Team, [], [], true) { TeamType = type, Manager = manager }] };
        TeamTypeCatalog types = RoleFixtures.Types(dir);
        return new ProcessGateService(catalog, folders, types);
    }
}
