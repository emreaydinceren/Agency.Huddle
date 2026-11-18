using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using static Agency.Huddle.Tests.Tasks.TaskGateText;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// <c>failing-test-before-fix</c> applies to a bug's move to Needs You or Done (#669, D-14): the two exits from In Progress. Its refusal
/// says "move on".
/// </summary>
public sealed class FailingTestGateMoveOnTests
{
    private const string FailFirst = "Gate failing-test-before-fix: an Evidence line showing a failing test before the fix is needed before this bug can move on.";

    private static readonly IReadOnlyDictionary<string, string> NoCovers = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>A bug moving from In Progress to Needs You or Done with no failing run on record is refused with the "move on" text.</summary>
    /// <param name="to">The proposed status.</param>
    [Theory]
    [InlineData("Needs You")]
    [InlineData("Done")]
    public void CheckTaskChange_BugLeavesInProgressWithoutFailingRun_IsRefused(string to)
    {
        using TempDataDir dir = new();
        ProcessGateService service = BuildGates(dir, Process("'failing-test-before-fix'"));

        GateRefusal? refusal = service.CheckTaskChange(Context(to, isBug: true));

        Assert.Equal(GateId.FailingTestBeforeFix, refusal?.Gate);
        Assert.Equal(FailFirst, refusal?.Message);
    }

    /// <summary>A bug moving to a status that is not an exit from In Progress, and a Task that is not a bug, are not refused.</summary>
    /// <param name="to">The proposed status.</param>
    /// <param name="isBug">Whether the Task is a bug.</param>
    [Theory]
    [InlineData("To Do", true)]
    [InlineData("Needs You", false)]
    public void CheckTaskChange_NotAnExitOrNotABug_IsAllowed(string to, bool isBug)
    {
        using TempDataDir dir = new();
        ProcessGateService service = BuildGates(dir, Process("'failing-test-before-fix'"));

        Assert.Null(service.CheckTaskChange(Context(to, isBug)));
    }

    /// <summary>A change out of In Progress to <paramref name="to"/>.</summary>
    /// <param name="to">The proposed status.</param>
    /// <param name="isBug">Whether the Task is a bug.</param>
    private static GateContext Context(string to, bool isBug) =>
        new(Team, new TaskId("PLAT", 1), "In Progress", to, isBug, true, [], NoCovers, "Jane");
}
