using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Tests for the Team records and enums.</summary>
public sealed class TeamRecordsTests
{
    /// <summary>HasMembers is true only when Members list is not empty.</summary>
    [Fact]
    public void TeamSummary_HasMembers_IsTrueOnlyWhenMembersExist()
    {
        TeamSummary summaryEmpty = new("Team1", [], [], false);
        TeamSummary summaryWithMembers = new("Team2", [], ["Nova"], false);

        Assert.False(summaryEmpty.HasMembers);
        Assert.True(summaryWithMembers.HasMembers);
    }

    /// <summary>TeamSummary with expression changes the Name property.</summary>
    [Fact]
    public void TeamSummary_With_ChangesName()
    {
        TeamSummary original = new("OriginalName", [], ["Nova"], false);
        TeamSummary modified = original with { Name = "NewName" };

        Assert.Equal("OriginalName", original.Name);
        Assert.Equal("NewName", modified.Name);
        Assert.Equal(original.Members, modified.Members);
    }

    /// <summary>MembershipOutcome enum members match the type map exactly.</summary>
    [Fact]
    public void MembershipOutcome_Members_MatchTypeMap()
    {
        string[] expected = ["Added", "Removed", "AlreadyMember", "NotMember", "NotFound", "Rejected"];
        string[] actual = Enum.GetNames<MembershipOutcome>();

        Assert.Equal(expected, actual);
    }

    /// <summary>MembershipResult Problem parameter defaults to null.</summary>
    [Fact]
    public void MembershipResult_Problem_DefaultsToNull()
    {
        MembershipResult result = new(MembershipOutcome.Added);

        Assert.Null(result.Problem);
    }

    /// <summary>TeamPageTab enum members are Members, Files, and Tasks.</summary>
    [Fact]
    public void TeamPageTab_Members_AreMembersFilesTasks()
    {
        string[] expected = ["Members", "Files", "Tasks"];
        string[] actual = Enum.GetNames<TeamPageTab>();

        Assert.Equal(expected, actual);
    }

    /// <summary>TeamPageFeatures equality is by value, not by reference.</summary>
    [Fact]
    public void TeamPageFeatures_Equality_IsByValue()
    {
        TeamPageFeatures features1 = new(Library: true, Tasks: false);
        TeamPageFeatures features2 = new(Library: true, Tasks: false);
        TeamPageFeatures features3 = new(Library: false, Tasks: true);

        Assert.Equal(features1, features2);
        Assert.NotEqual(features1, features3);
    }
}
