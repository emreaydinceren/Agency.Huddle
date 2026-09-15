namespace Agency.Huddle.Tests.Ui;

using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components.Pages;

/// <summary>
/// Exercises <see cref="TeammateGrouping.Group"/> directly against hand-built
/// <see cref="PersonaEntry"/> values - no <see cref="Agency.Huddle.App.Acp.PersonaStore"/>, no
/// rendering. The team filter's narrowing behaviour is proven here rather than through a rendered
/// page: a plain unit test over the pure function is simpler and faster than driving a real
/// <c>MudSelect</c> through bUnit for every narrowing case this covers, so this stays the primary
/// coverage for "the filter narrows the list" even though <c>TeammatesPageTests</c> can now also
/// dispatch a real click on the rendered select.
/// </summary>
public sealed class TeammateGroupingTests
{
    /// <summary>The defining behaviour of this phase: a group comes from the Teams field, never from a Path a caller happens to pass - proven here by giving an entry a nested Path that names a DIFFERENT team than its Teams field does.</summary>
    [Fact]
    public void Group_UsesTheTeamsFieldRatherThanTheEntrysPath()
    {
        var jarvis = Entry("Jarvis", teams: ["Business"], path: "Teams/Household/jarvis.md");

        var groups = TeammateGrouping.Group([jarvis], ["Business"], selectedTeam: null);

        var group = Assert.Single(groups);
        Assert.Equal("Business", group.Heading);
        Assert.Contains(jarvis, group.Members);
    }

    [Fact]
    public void Group_AnEntryInTwoTeams_AppearsUnderBothHeadings()
    {
        var amy = Entry("Amy", teams: ["Business", "Household"]);

        var groups = TeammateGrouping.Group([amy], ["Business", "Household"], selectedTeam: null);

        Assert.Equal(2, groups.Count);
        Assert.All(groups, group => Assert.Contains(amy, group.Members));
    }

    [Fact]
    public void Group_AnEntryWithNoTeams_FallsUnderTheNoTeamHeading()
    {
        var echo = Entry("echo", teams: []);

        var groups = TeammateGrouping.Group([echo], [], selectedTeam: null);

        var group = Assert.Single(groups);
        Assert.Equal(TeammateGrouping.NoTeamHeading, group.Heading);
        Assert.Contains(echo, group.Members);
    }

    [Fact]
    public void Group_HeadingsFollowTheGivenTeamOrder_WithNoTeamAlwaysLast()
    {
        var amy = Entry("Amy", teams: ["Household"]);
        var bob = Entry("Bob", teams: ["Business"]);
        var echo = Entry("echo", teams: []);

        var groups = TeammateGrouping.Group([amy, bob, echo], ["Business", "Household"], selectedTeam: null);

        Assert.Equal(["Business", "Household", TeammateGrouping.NoTeamHeading], groups.Select(g => g.Heading));
    }

    [Fact]
    public void Group_OmitsATeamHeading_WhenNoEntryBelongsToItAnyMore()
    {
        var amy = Entry("Amy", teams: ["Household"]);

        // "Business" is passed as a known Team name but nobody currently belongs to it.
        var groups = TeammateGrouping.Group([amy], ["Business", "Household"], selectedTeam: null);

        Assert.DoesNotContain(groups, g => g.Heading == "Business");
    }

    /// <summary>Selecting a Team is the behaviour <c>HtmlRenderer</c> cannot simulate through a rendered page, so it is proven here instead: only that Team's entries come back, under a single heading.</summary>
    [Fact]
    public void Group_WithATeamSelected_NarrowsToOnlyThatTeam()
    {
        var amy = Entry("Amy", teams: ["Business"]);
        var bob = Entry("Bob", teams: ["Household"]);
        var carla = Entry("Carla", teams: ["Business", "Household"]);

        var groups = TeammateGrouping.Group([amy, bob, carla], ["Business", "Household"], selectedTeam: "Business");

        var group = Assert.Single(groups);
        Assert.Equal("Business", group.Heading);
        Assert.Equal([amy, carla], group.Members);
    }

    [Fact]
    public void Group_TeamMatchingIsCaseInsensitive()
    {
        var amy = Entry("Amy", teams: ["business"]);

        var groups = TeammateGrouping.Group([amy], ["Business"], selectedTeam: "BUSINESS");

        var group = Assert.Single(groups);
        Assert.Contains(amy, group.Members);
    }

    [Fact]
    public void Group_WithATeamSelectedThatCurrentlyHasNoMembers_ReturnsNoGroupsAtAll()
    {
        var amy = Entry("Amy", teams: ["Household"]);

        var groups = TeammateGrouping.Group([amy], ["Business", "Household"], selectedTeam: "Business");

        Assert.Empty(groups);
    }

    [Fact]
    public void Group_BlankSelectedTeam_IsTreatedAsAllTeams()
    {
        var amy = Entry("Amy", teams: ["Business"]);
        var echo = Entry("echo", teams: []);

        var groups = TeammateGrouping.Group([amy, echo], ["Business"], selectedTeam: "   ");

        Assert.Equal(2, groups.Count);
    }

    /// <summary>A valid <see cref="PersonaEntry"/> for a unit test, with Title, Alias and Path defaulted to values that play no part in grouping.</summary>
    private static PersonaEntry Entry(string name, IReadOnlyList<string> teams, string? path = null) =>
        new(name, name, name, teams, path ?? $"{name}.md", "body");
}
