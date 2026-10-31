using HumanGateCopy = Agency.Huddle.App.Components.Tasks.HumanGateCopy;

namespace Agency.Huddle.Tests.Ui.Tasks;

/// <summary>Pins the decision card's two formatted lines of <c>HumanGateCopy</c> (UI copy rows 16 and 25) against full literals for every variant.</summary>
public sealed class HumanGateCopyDecisionTests
{
    /// <summary>The locked notice names the open item with its owner and detail.</summary>
    [Fact]
    public void LockedNotice_OwnerAndDetail_NamesBoth()
    {
        Assert.Equal(
            "Proof 3/4 — citation check still open (Ruth, 19 of 23 in #RSH-209). Accept unlocks when it's in.",
            HumanGateCopy.LockedNotice(3, 4, "citation check", "Ruth", "19 of 23 in #RSH-209"));
    }

    /// <summary>The locked notice with only an owner or only a detail shows that one in the parentheses.</summary>
    [Fact]
    public void LockedNotice_OneOfOwnerAndDetail_ShowsThatOne()
    {
        Assert.Equal("Proof 1/2 — tone still open (Ruth). Accept unlocks when it's in.", HumanGateCopy.LockedNotice(1, 2, "tone", "Ruth", null));
        Assert.Equal("Proof 1/2 — tone still open (due Friday). Accept unlocks when it's in.", HumanGateCopy.LockedNotice(1, 2, "tone", " ", "due Friday"));
    }

    /// <summary>The locked notice with neither owner nor detail drops the parentheses.</summary>
    [Fact]
    public void LockedNotice_NeitherOwnerNorDetail_DropsTheParentheses()
    {
        Assert.Equal("Proof 0/2 — tone still open. Accept unlocks when it's in.", HumanGateCopy.LockedNotice(0, 2, "tone", null, null));
    }

    /// <summary>The trail with no other checkers reads drafted by and tracked as only.</summary>
    [Fact]
    public void Trail_NoCheckers_OmitsTheCheckedByPart()
    {
        Assert.Equal("Drafted by Nova · Tracked as PLAT-0001", HumanGateCopy.Trail("Nova", [], "PLAT-0001"));
    }

    /// <summary>The trail lists every checker, comma-joined in the order given.</summary>
    [Fact]
    public void Trail_WithCheckers_ListsThemInOrder()
    {
        Assert.Equal("Drafted by Nova · Checked by Ruth · Tracked as PLAT-0001", HumanGateCopy.Trail("Nova", ["Ruth"], "PLAT-0001"));
        Assert.Equal("Drafted by Nova · Checked by Ruth, Kai · Tracked as PLAT-0001", HumanGateCopy.Trail("Nova", ["Ruth", "Kai"], "PLAT-0001"));
    }
}
