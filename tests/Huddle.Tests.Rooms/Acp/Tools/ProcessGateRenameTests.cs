using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Tasks;

namespace Agency.Huddle.Tests.Acp.Tools;

/// <summary>Pins the old-name resolution of <see cref="ProcessGateService"/> (Team Rename R9): a label still saying the old name reads the renamed Team's gates.</summary>
public sealed class ProcessGateRenameTests
{
    /// <summary>A Task whose label still says Marketing reads Platform's gates, so the gate is never failed open.</summary>
    [Fact]
    public void EnabledGates_OldTeamName_ReadsTheRenamedTeamsGates()
    {
        using TempDataDir dir = new();
        TeamRenameStore renames = new(dir.Options());
        renames.Complete(renames.Begin(TeamRenameKind.Team, "Marketing", "Marketing", TaskGateText.Team));
        ProcessGateService gates = TaskGateText.BuildGates(dir, TaskGateText.Process(TaskGateText.QaGate), renames);

        Assert.Equal([GateId.QaEvidenceBeforeDone], gates.EnabledGates("Marketing"));
        Assert.Equal([GateId.QaEvidenceBeforeDone], gates.EnabledGates(TaskGateText.Team));
    }

    /// <summary>With no journalled rename, an unknown Team name has no gates, as today.</summary>
    [Fact]
    public void EnabledGates_NeverJournalledName_HasNoGates()
    {
        using TempDataDir dir = new();
        TeamRenameStore renames = new(dir.Options());
        ProcessGateService gates = TaskGateText.BuildGates(dir, TaskGateText.Process(TaskGateText.QaGate), renames);

        Assert.Empty(gates.EnabledGates("Marketing"));
    }
}
