using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Agents;
using Agency.Huddle.Tests.Ui.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>The four process gates decide whether a Task change or a merge is refused (PR-7, Spec §Gates).</summary>
public sealed class ProcessGateServiceTests
{
    private const string Team = "Apollo";
    private const string Check = "dotnet test";
    private const string QaDone = "Gate qa-evidence-before-done: an Evidence line from a member who verifies (for example QA Engineer) is needed before this Task can be Done.";
    private const string SpecFirst = "Gate spec-approved-before-build: the parent spec Task must be approved before this Task can move to In Progress.";
    private const string FailFirst = "Gate failing-test-before-fix: an Evidence line showing a failing test before the fix is needed before this bug can move to Review.";
    private const string ChecksFirst = "Gate checks-before-merge: an Evidence line with exit code 0 for 'dotnet test' is needed before merging or pushing.";

    private static readonly DateTimeOffset Noon = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly IReadOnlyDictionary<string, string> Covers = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Quinn"] = "verify, review",
        ["Dev"] = "build",
    };

    /// <summary>A Team with no Process.md has no enabled gates.</summary>
    [Fact]
    public void EnabledGates_NoProcessFile_IsEmpty()
    {
        using TempDataDir dir = new();
        ProcessGateService service = Build(dir, null);

        Assert.Empty(service.EnabledGates(Team));
    }

    /// <summary>The enabled gates are the ones the Process.md frontmatter declares, in file order.</summary>
    [Fact]
    public void EnabledGates_Declared_ReturnsFileOrder()
    {
        using TempDataDir dir = new();
        ProcessGateService service = Build(dir, Process("'failing-test-before-fix', 'qa-evidence-before-done'"));

        Assert.Equal([GateId.FailingTestBeforeFix, GateId.QaEvidenceBeforeDone], service.EnabledGates(Team));
    }

    /// <summary>A Team whose type the catalog does not know has no gates, even with a Process.md.</summary>
    [Fact]
    public void EnabledGates_UnknownTeamType_IsEmpty()
    {
        using TempDataDir dir = new();
        ProcessGateService service = Build(dir, Process("'qa-evidence-before-done'"), "ghost");

        Assert.Empty(service.EnabledGates(Team));
    }

    /// <summary>Moving to Done without any Evidence is refused with the gate message.</summary>
    [Fact]
    public void CheckTaskChange_DoneWithoutEvidence_IsRefused()
    {
        using TempDataDir dir = new();
        ProcessGateService service = Build(dir, Process("'qa-evidence-before-done'"));

        GateRefusal? refusal = service.CheckTaskChange(Context("In Progress", "Done"));

        Assert.Equal(GateId.QaEvidenceBeforeDone, refusal?.Gate);
        Assert.Equal(QaDone, refusal?.Message);
    }

    /// <summary>An Evidence line authored by a member whose role covers verify lets the Task reach Done.</summary>
    [Fact]
    public void CheckTaskChange_DoneWithVerifierEvidence_IsAllowed()
    {
        using TempDataDir dir = new();
        ProcessGateService service = Build(dir, Process("'qa-evidence-before-done'"));

        GateRefusal? refusal = service.CheckTaskChange(Context("Review", "Done", evidence: [Line("Quinn", Check, 0)]));

        Assert.Null(refusal);
    }

    /// <summary>Evidence from a member whose role does not cover verify does not satisfy the QA gate.</summary>
    [Fact]
    public void CheckTaskChange_DoneWithNonVerifierEvidence_IsRefused()
    {
        using TempDataDir dir = new();
        ProcessGateService service = Build(dir, Process("'qa-evidence-before-done'"));

        GateRefusal? refusal = service.CheckTaskChange(Context("Review", "Done", evidence: [Line("Dev", Check, 0), Line("Stranger", Check, 0)]));

        Assert.Equal(QaDone, refusal?.Message);
    }

    /// <summary>The Human acting does not bypass the service; the override lives in TaskService (Task 52).</summary>
    [Fact]
    public void CheckTaskChange_HumanActor_IsStillRefused()
    {
        using TempDataDir dir = new();
        ProcessGateService service = Build(dir, Process("'qa-evidence-before-done'"));

        GateRefusal? refusal = service.CheckTaskChange(Context("Review", "Done", actor: "Human"));

        Assert.Equal(GateId.QaEvidenceBeforeDone, refusal?.Gate);
    }

    /// <summary>A gate the Process.md does not enable never refuses.</summary>
    [Fact]
    public void CheckTaskChange_GateNotEnabled_IsAllowed()
    {
        using TempDataDir dir = new();
        ProcessGateService service = Build(dir, Process("'checks-before-merge'"));

        Assert.Null(service.CheckTaskChange(Context("Review", "Done")));
        Assert.Null(service.CheckTaskChange(Context("To Do", "In Progress", parentSpecApproved: false)));
        Assert.Null(service.CheckTaskChange(Context("In Progress", "Review", isBug: true)));
    }

    /// <summary>A Team with no Process.md has no gates, so nothing is refused.</summary>
    [Fact]
    public void CheckTaskChange_NoProcessFile_IsAllowed()
    {
        using TempDataDir dir = new();
        ProcessGateService service = Build(dir, null);

        Assert.Null(service.CheckTaskChange(Context("Review", "Done")));
    }

    /// <summary>A status other than Done is not gated by the QA gate.</summary>
    [Fact]
    public void CheckTaskChange_QaGateOtherStatus_IsAllowed()
    {
        using TempDataDir dir = new();
        ProcessGateService service = Build(dir, Process("'qa-evidence-before-done'"));

        Assert.Null(service.CheckTaskChange(Context("In Progress", "Review")));
    }

    /// <summary>Moving to In Progress under an unapproved parent spec is refused.</summary>
    [Fact]
    public void CheckTaskChange_InProgressSpecNotApproved_IsRefused()
    {
        using TempDataDir dir = new();
        ProcessGateService service = Build(dir, Process("'spec-approved-before-build'"));

        GateRefusal? refusal = service.CheckTaskChange(Context("To Do", "In Progress", parentSpecApproved: false));

        Assert.Equal(GateId.SpecApprovedBeforeBuild, refusal?.Gate);
        Assert.Equal(SpecFirst, refusal?.Message);
    }

    /// <summary>An approved parent spec (or none) lets the Task move to In Progress; other targets are not gated.</summary>
    [Fact]
    public void CheckTaskChange_SpecApprovedOrOtherTarget_IsAllowed()
    {
        using TempDataDir dir = new();
        ProcessGateService service = Build(dir, Process("'spec-approved-before-build'"));

        Assert.Null(service.CheckTaskChange(Context("To Do", "In Progress", parentSpecApproved: true)));
        Assert.Null(service.CheckTaskChange(Context("To Do", "Review", parentSpecApproved: false)));
    }

    /// <summary>A bug moving to Review with no Evidence is refused.</summary>
    [Fact]
    public void CheckTaskChange_BugToReviewWithoutEvidence_IsRefused()
    {
        using TempDataDir dir = new();
        ProcessGateService service = Build(dir, Process("'failing-test-before-fix'"));

        GateRefusal? refusal = service.CheckTaskChange(Context("In Progress", "Review", isBug: true));

        Assert.Equal(GateId.FailingTestBeforeFix, refusal?.Gate);
        Assert.Equal(FailFirst, refusal?.Message);
    }

    /// <summary>A failing run followed by a passing run satisfies the failing-test gate.</summary>
    [Fact]
    public void CheckTaskChange_BugFailThenPass_IsAllowed()
    {
        using TempDataDir dir = new();
        ProcessGateService service = Build(dir, Process("'failing-test-before-fix'"));
        EvidenceLine[] evidence = [Line("Dev", Check, 1, 0), Line("Dev", Check, 0, 10)];

        Assert.Null(service.CheckTaskChange(Context("In Progress", "Review", isBug: true, evidence: evidence)));
    }

    /// <summary>A pass that is only followed by a failure, or a failure with no later pass, does not satisfy the gate.</summary>
    [Fact]
    public void CheckTaskChange_BugPassThenFailOrFailOnly_IsRefused()
    {
        using TempDataDir dir = new();
        ProcessGateService service = Build(dir, Process("'failing-test-before-fix'"));
        EvidenceLine[] passThenFail = [Line("Dev", Check, 0, 0), Line("Dev", Check, 1, 10)];
        EvidenceLine[] failOnly = [Line("Dev", Check, 1, 0)];

        Assert.Equal(FailFirst, service.CheckTaskChange(Context("In Progress", "Review", isBug: true, evidence: passThenFail))?.Message);
        Assert.Equal(FailFirst, service.CheckTaskChange(Context("In Progress", "Review", isBug: true, evidence: failOnly))?.Message);
    }

    /// <summary>The failing-test gate only applies to bugs.</summary>
    [Fact]
    public void CheckTaskChange_NonBugToReview_IsAllowed()
    {
        using TempDataDir dir = new();
        ProcessGateService service = Build(dir, Process("'failing-test-before-fix'"));

        Assert.Null(service.CheckTaskChange(Context("In Progress", "Review", isBug: false)));
    }

    /// <summary>A passing check command with nothing later lets a merge through.</summary>
    [Fact]
    public void CheckMerge_PassingCheck_IsAllowed()
    {
        using TempDataDir dir = new();
        ProcessGateService service = Build(dir, Process("'checks-before-merge'"));

        Assert.Null(service.CheckMerge(Team, [Line("Dev", " dotnet test ", 0)]));
    }

    /// <summary>A merge with no Evidence is refused with the gate message.</summary>
    [Fact]
    public void CheckMerge_NoEvidence_IsRefused()
    {
        using TempDataDir dir = new();
        ProcessGateService service = Build(dir, Process("'checks-before-merge'"));

        GateRefusal? refusal = service.CheckMerge(Team, []);

        Assert.Equal(GateId.ChecksBeforeMerge, refusal?.Gate);
        Assert.Equal(ChecksFirst, refusal?.Message);
    }

    /// <summary>A non-zero run after the passing one, or a different command, does not satisfy the gate.</summary>
    [Fact]
    public void CheckMerge_LaterFailureOrOtherCommand_IsRefused()
    {
        using TempDataDir dir = new();
        ProcessGateService service = Build(dir, Process("'checks-before-merge'"));
        EvidenceLine[] laterFailure = [Line("Dev", Check, 0, 0), Line("Dev", Check, 1, 10)];
        EvidenceLine[] otherCommand = [Line("Dev", "dotnet build", 0)];

        Assert.Equal(ChecksFirst, service.CheckMerge(Team, laterFailure)?.Message);
        Assert.Equal(ChecksFirst, service.CheckMerge(Team, otherCommand)?.Message);
    }

    /// <summary>A failure followed by a later pass lets the merge through.</summary>
    [Fact]
    public void CheckMerge_FailureThenPass_IsAllowed()
    {
        using TempDataDir dir = new();
        ProcessGateService service = Build(dir, Process("'checks-before-merge'"));

        Assert.Null(service.CheckMerge(Team, [Line("Dev", Check, 1, 0), Line("Dev", Check, 0, 10)]));
    }

    /// <summary>A merge is allowed when the gate is not enabled or the Team has no Process.md.</summary>
    [Fact]
    public void CheckMerge_GateNotEnabledOrNoProcess_IsAllowed()
    {
        using TempDataDir dir = new();
        using TempDataDir none = new();
        ProcessGateService other = Build(dir, Process("'qa-evidence-before-done'"));
        ProcessGateService missing = Build(none, null);

        Assert.Null(other.CheckMerge(Team, []));
        Assert.Null(missing.CheckMerge(Team, []));
    }

    private static string Process(string gates) =>
        $"---\ntitle: 'Flow'\ngates: [{gates}]\ncheckCommand: '{Check}'\n---\n# Flow\n";

    private static EvidenceLine Line(string author, string command, int exitCode, int minutes = 0) =>
        new(Noon.AddMinutes(minutes), author, command, exitCode, null, null, $"{author} | {command}");

    private static GateContext Context(
        string from,
        string to,
        bool isBug = false,
        bool parentSpecApproved = true,
        IReadOnlyList<EvidenceLine>? evidence = null,
        string? actor = null) =>
        new(Team, new TaskId("TASK", 1), from, to, isBug, parentSpecApproved, evidence ?? [], Covers, actor);

    private static ProcessGateService Build(TempDataDir dir, string? processText, string typeId = "web")
    {
        if (processText is not null)
        {
            File.WriteAllText(Path.Combine(dir.Path, TeamPaths.ProcessFileName), processText);
        }

        VersionedRef type = new(typeId, "1.2.0");
        LibraryRoot root = new("teams", "Teams", dir.Path, LibraryRootKind.Teams);
        FakeTeamFolders folders = new()
        {
            TeamResult = LibraryResult<LibraryPath>.Ok(new LibraryPath(root, Team, dir.Path, LibraryNodeRole.TeamFolder)),
            ReadResult = new TeamFile(type, null, null, null, TeamPaths.ProcessFileName, string.Empty),
        };
        FakeTeamCatalog catalog = new() { Teams = [new TeamSummary(Team, [], [], true) { TeamType = type }] };
        TeamTypeCatalog types = RoleFixtures.Types(dir);
        return new ProcessGateService(catalog, folders, types);
    }
}
