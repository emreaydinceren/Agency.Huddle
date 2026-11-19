using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;

namespace Agency.Huddle.Tests.Tasks.Views;

/// <summary>Tests for <see cref="RankPlacement"/>: the rank a card gets when it is dropped at an index among the cards of its zone.</summary>
public sealed class RankPlacementTests
{
    /// <summary>A Task with the given id, rank, priority and Team.</summary>
    private static TaskItem Make(string id, int? rank, TaskPriority priority = TaskPriority.Medium, string team = "Platform") =>
        TestTasks.Make(id: id, priority: priority, location: new TaskLocation(team, null, false)) with { Rank = rank };

    /// <summary>The Task being dragged: unranked, Low priority, id PLAT-0099 unless told otherwise.</summary>
    private static TaskItem Dragged(int? rank = null, TaskPriority priority = TaskPriority.Low) => Make("PLAT-0099", rank, priority);

    /// <summary>Two ranked neighbours with room between them: the card goes to the middle.</summary>
    [Fact]
    public void Place_BetweenRankedWithGap_TakesTheMiddle()
    {
        IReadOnlyList<TaskItem> zone = [Make("PLAT-0001", 10), Make("PLAT-0002", 30)];

        Assert.Equal(new RankPlacement.Change.Set(20), RankPlacement.Place(zone, 1, Dragged()));
    }

    /// <summary>An odd gap rounds down: 10 and 13 give 11.</summary>
    [Fact]
    public void Place_BetweenRankedWithOddGap_RoundsDown()
    {
        IReadOnlyList<TaskItem> zone = [Make("PLAT-0001", 10), Make("PLAT-0002", 13)];

        Assert.Equal(new RankPlacement.Change.Set(11), RankPlacement.Place(zone, 1, Dragged()));
    }

    /// <summary>A gap of exactly two still has a whole number between: 10 and 12 give 11.</summary>
    [Fact]
    public void Place_BetweenRankedWithGapOfTwo_TakesTheOneBetween()
    {
        IReadOnlyList<TaskItem> zone = [Make("PLAT-0001", 10), Make("PLAT-0002", 12)];

        Assert.Equal(new RankPlacement.Change.Set(11), RankPlacement.Place(zone, 1, Dragged()));
    }

    /// <summary>At the top of a zone whose first card is ranked 10, the card takes half of it.</summary>
    [Fact]
    public void Place_TopOfZone_TakesHalfTheFirstRank()
    {
        IReadOnlyList<TaskItem> zone = [Make("PLAT-0001", 10), Make("PLAT-0002", 30)];

        Assert.Equal(new RankPlacement.Change.Set(5), RankPlacement.Place(zone, 0, Dragged()));
    }

    /// <summary>At the top above a card ranked 1 there is no smaller rank: the card takes 1 and the tiebreak decides.</summary>
    [Fact]
    public void Place_TopOfZoneAboveRankOne_TakesOne()
    {
        IReadOnlyList<TaskItem> zone = [Make("PLAT-0001", 1)];

        Assert.Equal(new RankPlacement.Change.Set(1), RankPlacement.Place(zone, 0, Dragged()));
    }

    /// <summary>At the top above a card ranked 2, half of 2 is 1.</summary>
    [Fact]
    public void Place_TopOfZoneAboveRankTwo_TakesOne()
    {
        IReadOnlyList<TaskItem> zone = [Make("PLAT-0001", 2)];

        Assert.Equal(new RankPlacement.Change.Set(1), RankPlacement.Place(zone, 0, Dragged()));
    }

    /// <summary>Directly after the last ranked card, with unranked cards below, the card takes the last rank plus ten.</summary>
    [Fact]
    public void Place_AfterLastRankedBeforeUnranked_TakesLastPlusTen()
    {
        IReadOnlyList<TaskItem> zone = [Make("PLAT-0001", 10), Make("PLAT-0002", null)];

        Assert.Equal(new RankPlacement.Change.Set(20), RankPlacement.Place(zone, 1, Dragged()));
    }

    /// <summary>At the very end of an all-ranked zone, the card takes the last rank plus ten.</summary>
    [Fact]
    public void Place_AtEndAfterRanked_TakesLastPlusTen()
    {
        IReadOnlyList<TaskItem> zone = [Make("PLAT-0001", 10), Make("PLAT-0002", 25)];

        Assert.Equal(new RankPlacement.Change.Set(35), RankPlacement.Place(zone, 2, Dragged()));
    }

    /// <summary>An empty zone: the first rank is 10.</summary>
    [Fact]
    public void Place_EmptyZone_TakesTen()
    {
        Assert.Equal(new RankPlacement.Change.Set(10), RankPlacement.Place([], 0, Dragged()));
    }

    /// <summary>At the top of a zone with no ranked card at all, the card takes 10.</summary>
    [Fact]
    public void Place_TopOfZoneWithNoRankedCards_TakesTen()
    {
        IReadOnlyList<TaskItem> zone = [Make("PLAT-0001", null), Make("PLAT-0002", null)];

        Assert.Equal(new RankPlacement.Change.Set(10), RankPlacement.Place(zone, 0, Dragged()));
    }

    /// <summary>Between two unranked cards, a ranked card loses its rank.</summary>
    [Fact]
    public void Place_BetweenUnrankedWhenRanked_Clears()
    {
        IReadOnlyList<TaskItem> zone = [Make("PLAT-0001", 10), Make("PLAT-0002", null), Make("PLAT-0003", null)];

        Assert.Equal(new RankPlacement.Change.Clear(), RankPlacement.Place(zone, 2, Dragged(rank: 40)));
    }

    /// <summary>Between two unranked cards, an unranked card stays as it is: nothing is written.</summary>
    [Fact]
    public void Place_BetweenUnrankedWhenUnranked_NoChange()
    {
        IReadOnlyList<TaskItem> zone = [Make("PLAT-0001", 10), Make("PLAT-0002", null), Make("PLAT-0003", null)];

        Assert.Equal(new RankPlacement.Change.NoChange(), RankPlacement.Place(zone, 2, Dragged()));
    }

    /// <summary>After the last card when it is unranked counts as after an unranked card: a ranked card is cleared.</summary>
    [Fact]
    public void Place_AfterUnrankedAtEnd_ClearsARankedCard()
    {
        IReadOnlyList<TaskItem> zone = [Make("PLAT-0001", 10), Make("PLAT-0002", null)];

        Assert.Equal(new RankPlacement.Change.Clear(), RankPlacement.Place(zone, 2, Dragged(rank: 5)));
    }

    /// <summary>Dropped where its own rank already puts it, a card writes nothing.</summary>
    [Fact]
    public void Place_ResultEqualsCurrentRank_NoChange()
    {
        IReadOnlyList<TaskItem> zone = [Make("PLAT-0001", 10), Make("PLAT-0002", 30)];

        Assert.Equal(new RankPlacement.Change.NoChange(), RankPlacement.Place(zone, 1, Dragged(rank: 20)));
    }

    /// <summary>With no whole number between 5 and 6, the card tries A's rank and the tiebreak puts a Low card after a Medium one: it keeps 5.</summary>
    [Fact]
    public void Place_NoGap_TakesARankWhenTiebreakPlacesTheCardBetween()
    {
        IReadOnlyList<TaskItem> zone = [Make("PLAT-0001", 5), Make("PLAT-0002", 6)];

        Assert.Equal(new RankPlacement.Change.Set(5), RankPlacement.Place(zone, 1, Dragged(priority: TaskPriority.Low)));
    }

    /// <summary>With no gap and an Urgent card, A's rank would put it above A, so B's rank is tried and fits.</summary>
    [Fact]
    public void Place_NoGap_FallsBackToBRankWhenARankWouldSortAboveA()
    {
        IReadOnlyList<TaskItem> zone = [Make("PLAT-0001", 5), Make("PLAT-0002", 6)];

        Assert.Equal(new RankPlacement.Change.Set(6), RankPlacement.Place(zone, 1, Dragged(priority: TaskPriority.Urgent)));
    }

    /// <summary>When neither rank can place the card between two equal ranks, it takes A's rank.</summary>
    [Fact]
    public void Place_TiedNeighboursNoRankFits_TakesARank()
    {
        IReadOnlyList<TaskItem> zone = [Make("PLAT-0001", 5, TaskPriority.Low), Make("PLAT-0002", 5, TaskPriority.Low)];

        Assert.Equal(new RankPlacement.Change.Set(5), RankPlacement.Place(zone, 1, Dragged(priority: TaskPriority.Urgent)));
    }

    /// <summary>After the last ranked card near int.MaxValue the result is clamped, not wrapped.</summary>
    [Fact]
    public void Place_AfterLastRankedNearIntMax_ClampsToIntMax()
    {
        IReadOnlyList<TaskItem> zone = [Make("PLAT-0001", int.MaxValue - 3)];

        Assert.Equal(new RankPlacement.Change.Set(int.MaxValue), RankPlacement.Place(zone, 1, Dragged()));
    }

    /// <summary>A card already at int.MaxValue dropped after the last ranked card near the limit writes nothing.</summary>
    [Fact]
    public void Place_ClampedResultEqualsCurrentRank_NoChange()
    {
        IReadOnlyList<TaskItem> zone = [Make("PLAT-0001", int.MaxValue)];

        Assert.Equal(new RankPlacement.Change.NoChange(), RankPlacement.Place(zone, 1, Dragged(rank: int.MaxValue)));
    }

    /// <summary>The gap maths runs in long: 1 and int.MaxValue do not overflow.</summary>
    [Fact]
    public void Place_WideGap_DoesNotOverflow()
    {
        IReadOnlyList<TaskItem> zone = [Make("PLAT-0001", 1), Make("PLAT-0002", int.MaxValue)];

        Assert.Equal(new RankPlacement.Change.Set(1073741824), RankPlacement.Place(zone, 1, Dragged()));
    }

    /// <summary>A neighbour of another Team is skipped: the ranked Platform cards on either side of it are the neighbours.</summary>
    [Fact]
    public void Place_OtherTeamNeighbourBetween_IsSkipped()
    {
        IReadOnlyList<TaskItem> zone = [Make("PLAT-0001", 10), Make("OPS-0001", 12, team: "Ops"), Make("PLAT-0002", 40)];

        Assert.Equal(new RankPlacement.Change.Set(25), RankPlacement.Place(zone, 2, Dragged()));
    }

    /// <summary>A card of another Team at the top does not count as the card above: the drop is still the top of the Team's cards.</summary>
    [Fact]
    public void Place_OtherTeamAboveFirstOwnCard_CountsAsTop()
    {
        IReadOnlyList<TaskItem> zone = [Make("OPS-0001", 3, team: "Ops"), Make("PLAT-0001", 10)];

        Assert.Equal(new RankPlacement.Change.Set(5), RankPlacement.Place(zone, 1, Dragged()));
    }

    /// <summary>A zone holding only other Teams' cards is an empty zone for this Team: the card takes 10.</summary>
    [Fact]
    public void Place_OnlyOtherTeamCards_TakesTen()
    {
        IReadOnlyList<TaskItem> zone = [Make("OPS-0001", 3, team: "Ops"), Make("OPS-0002", 8, team: "Ops")];

        Assert.Equal(new RankPlacement.Change.Set(10), RankPlacement.Place(zone, 1, Dragged()));
    }

    /// <summary>A drop index past the end behaves as the end of the zone.</summary>
    [Fact]
    public void Place_IndexPastEnd_IsTheEnd()
    {
        IReadOnlyList<TaskItem> zone = [Make("PLAT-0001", 10)];

        Assert.Equal(new RankPlacement.Change.Set(20), RankPlacement.Place(zone, 99, Dragged()));
    }
}
