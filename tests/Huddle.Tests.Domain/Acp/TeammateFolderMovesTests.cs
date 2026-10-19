using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// Exercises <see cref="TeammateFolderMoves"/> directly: the gate <see cref="PersonaRenameCascade"/>
/// signals around a Teammate folder move (corrections-B2 item 20), so a caller such as
/// <c>DotAcpAgentHostFactory</c> can await it before reading the folder it is about to create.
/// </summary>
public sealed class TeammateFolderMovesTests
{
    /// <summary>When no folder move is pending for a Name, <see cref="TeammateFolderMoves.WhenSettledAsync"/> completes synchronously rather than handing back a Task that only resolves later.</summary>
    [Fact]
    public void WhenSettled_NothingPending_CompletesSynchronously()
    {
        var ct = TestContext.Current.CancellationToken;
        var moves = new TeammateFolderMoves();

        var task = moves.WhenSettledAsync("Anyone", ct);

        Assert.True(task.IsCompletedSuccessfully);
    }
}
