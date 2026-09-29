using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Tests for TeamLabels list operations.</summary>
public sealed class TeamLabelsTests
{
    /// <summary>Add to an empty list adds the team.</summary>
    [Fact]
    public void Add_EmptyList_AddsTeam()
    {
        IReadOnlyList<string> result = TeamLabels.Add([], "Business");
        Assert.Equal(["Business"], result);
    }

    /// <summary>Add to a list with one team appends the new team.</summary>
    [Fact]
    public void Add_OneTeamExists_AppendsTeam()
    {
        IReadOnlyList<string> result = TeamLabels.Add(["Research"], "Business");
        Assert.Equal(["Research", "Business"], result);
    }

    /// <summary>Add of same team in different case keeps the original spelling.</summary>
    [Fact]
    public void Add_SameCaseInsensitive_KeepsOriginalSpelling()
    {
        IReadOnlyList<string> result = TeamLabels.Add(["Business"], "business");
        Assert.Equal(["Business"], result);
    }

    /// <summary>Add to a list with multiple teams appends to the end.</summary>
    [Fact]
    public void Add_MultiplTeamsExist_AppendsTeam()
    {
        IReadOnlyList<string> result = TeamLabels.Add(["A", "B"], "Ops");
        Assert.Equal(["A", "B", "Ops"], result);
    }

    /// <summary>Remove drops all case variants and keeps other teams in order.</summary>
    [Fact]
    public void Remove_MultipleVariants_DropsAllCaseVariantsKeepsOthers()
    {
        IReadOnlyList<string> result = TeamLabels.Remove(["Business", "x", "BUSINESS"], "business");
        Assert.Equal(["x"], result);
    }

    /// <summary>Remove of absent team returns list unchanged.</summary>
    [Fact]
    public void Remove_TeamNotPresent_ReturnsUnchanged()
    {
        IReadOnlyList<string> result = TeamLabels.Remove(["A", "B"], "C");
        Assert.Equal(["A", "B"], result);
    }

    /// <summary>Remove of only team returns empty list.</summary>
    [Fact]
    public void Remove_OnlyTeam_ReturnsEmpty()
    {
        IReadOnlyList<string> result = TeamLabels.Remove(["Business"], "Business");
        Assert.Equal([], result);
    }

    /// <summary>Contains ignores case.</summary>
    [Fact]
    public void Contains_CaseInsensitiveMatch_ReturnsTrue()
    {
        IReadOnlyList<string> teams = ["Business"];
        bool result = TeamLabels.Contains(teams, "BUSINESS");
        Assert.True(result);
    }

    /// <summary>Contains returns false for absent team.</summary>
    [Fact]
    public void Contains_TeamNotPresent_ReturnsFalse()
    {
        IReadOnlyList<string> teams = [];
        bool result = TeamLabels.Contains(teams, "x");
        Assert.False(result);
    }

    /// <summary>Add does not mutate the input list.</summary>
    [Fact]
    public void Add_Input_NotMutated()
    {
        List<string> original = ["Research"];
        List<string> input = original;
        _ = TeamLabels.Add(input, "Business");
        Assert.Equal(["Research"], input);
    }
}
