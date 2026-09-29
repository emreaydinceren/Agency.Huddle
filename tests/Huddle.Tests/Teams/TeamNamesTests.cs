using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Tests for TeamNames validation methods and constants.</summary>
public sealed class TeamNamesTests
{
    /// <summary>IsReservedProjectName returns true for reserved folder names: memory (case-insensitive), _tasks, _x, .git, .x.</summary>
    [Theory]
    [InlineData("memory")]
    [InlineData("Memory")]
    [InlineData("MEMORY")]
    [InlineData("_tasks")]
    [InlineData("_x")]
    [InlineData(".git")]
    [InlineData(".x")]
    public void IsReservedProjectName_ReservedNames_ReturnsTrue(string name)
    {
        bool result = TeamNames.IsReservedProjectName(name);

        Assert.True(result);
    }

    /// <summary>IsReservedProjectName returns false for unreserved names.</summary>
    [Theory]
    [InlineData("memory-notes")]
    [InlineData("memories")]
    [InlineData("mem")]
    [InlineData("Launch Q4")]
    [InlineData("")]
    public void IsReservedProjectName_UnreservedNames_ReturnsFalse(string name)
    {
        bool result = TeamNames.IsReservedProjectName(name);

        Assert.False(result);
    }

    /// <summary>ValidateTeamName allows a new name that does not conflict with existing Teams.</summary>
    [Fact]
    public void ValidateTeamName_NewTeam_ReturnsNull()
    {
        List<TeamSummary> existing = [Team("Business")];

        string? result = TeamNames.ValidateTeamName("Research", existing);

        Assert.Null(result);
    }

    /// <summary>ValidateTeamName allows "memory" at Team level (it is only reserved for Projects).</summary>
    [Fact]
    public void ValidateTeamName_MemoryAtTeamLevel_ReturnsNull()
    {
        List<TeamSummary> existing = [Team("Business")];

        string? result = TeamNames.ValidateTeamName("memory", existing);

        Assert.Null(result);
    }

    /// <summary>ValidateTeamName rejects a Team name that already exists (case-insensitive) using the typed spelling in the message.</summary>
    [Fact]
    public void ValidateTeamName_ExistingTeamSameCasing_ReturnsExactMessage()
    {
        List<TeamSummary> existing = [Team("Business")];

        string? result = TeamNames.ValidateTeamName("Business", existing);

        Assert.Equal("A Team named \"Business\" already exists.", result);
    }

    /// <summary>ValidateTeamName rejects a Team name that already exists (case-insensitive) using the typed spelling in the message.</summary>
    [Fact]
    public void ValidateTeamName_ExistingTeamDifferentCasing_ReturnsMessageWithTypedSpelling()
    {
        List<TeamSummary> existing = [Team("Business")];

        string? result = TeamNames.ValidateTeamName("business", existing);

        Assert.Equal("A Team named \"business\" already exists.", result);
    }

    /// <summary>ValidateTeamName rejects names starting with underscore.</summary>
    [Fact]
    public void ValidateTeamName_StartsWithUnderscore_ReturnsReservedPrefixMessage()
    {
        List<TeamSummary> existing = [Team("Business")];

        string? result = TeamNames.ValidateTeamName("_x", existing);

        Assert.Equal("Names starting with \"_\" or \".\" are reserved.", result);
    }

    /// <summary>ValidateTeamName rejects names starting with dot.</summary>
    [Fact]
    public void ValidateTeamName_StartsWithDot_ReturnsReservedPrefixMessage()
    {
        List<TeamSummary> existing = [Team("Business")];

        string? result = TeamNames.ValidateTeamName(".x", existing);

        Assert.Equal("Names starting with \"_\" or \".\" are reserved.", result);
    }

    /// <summary>ValidateTeamName rejects names containing commas.</summary>
    [Fact]
    public void ValidateTeamName_ContainsComma_ReturnsInvalidCharactersMessage()
    {
        List<TeamSummary> existing = [Team("Business")];

        string? result = TeamNames.ValidateTeamName("Sales, EMEA", existing);

        Assert.Equal("A Team name can't contain commas, semicolons or square brackets.", result);
    }

    /// <summary>ValidateTeamName rejects names containing semicolons.</summary>
    [Fact]
    public void ValidateTeamName_ContainsSemicolon_ReturnsInvalidCharactersMessage()
    {
        List<TeamSummary> existing = [Team("Business")];

        string? result = TeamNames.ValidateTeamName("A;B", existing);

        Assert.Equal("A Team name can't contain commas, semicolons or square brackets.", result);
    }

    /// <summary>ValidateTeamName rejects names containing square brackets.</summary>
    [Fact]
    public void ValidateTeamName_ContainsSquareBracket_ReturnsInvalidCharactersMessage()
    {
        List<TeamSummary> existing = [Team("Business")];

        string? result = TeamNames.ValidateTeamName("[A]", existing);

        Assert.Equal("A Team name can't contain commas, semicolons or square brackets.", result);
    }

    /// <summary>ValidateTeamName rejects names with colon (control character check).</summary>
    [Fact]
    public void ValidateTeamName_ContainsColon_ReturnsNonNull()
    {
        List<TeamSummary> existing = [Team("Business")];

        string? result = TeamNames.ValidateTeamName("a:b", existing);

        Assert.NotNull(result);
    }

    /// <summary>ValidateTeamName rejects empty names.</summary>
    [Fact]
    public void ValidateTeamName_EmptyName_ReturnsNonNull()
    {
        List<TeamSummary> existing = [Team("Business")];

        string? result = TeamNames.ValidateTeamName("", existing);

        Assert.NotNull(result);
    }

    /// <summary>ValidateTeamName rejects names with newline control character (S14).</summary>
    [Fact]
    public void ValidateTeamName_ContainsNewline_ReturnsNonNull()
    {
        List<TeamSummary> existing = [Team("Business")];

        string? result = TeamNames.ValidateTeamName("A\nB", existing);

        Assert.NotNull(result);
    }

    /// <summary>ValidateProjectName allows a new Project name that does not conflict with existing ones.</summary>
    [Fact]
    public void ValidateProjectName_NewProject_ReturnsNull()
    {
        TeamSummary team = Team("Business", "Marketing Project");

        string? result = TeamNames.ValidateProjectName("Q4 Launch", team);

        Assert.Null(result);
    }

    /// <summary>ValidateProjectName allows "memory-notes" even though "memory" is reserved.</summary>
    [Fact]
    public void ValidateProjectName_MemoryDash_ReturnsNull()
    {
        TeamSummary team = Team("Business", "Marketing Project");

        string? result = TeamNames.ValidateProjectName("memory-notes", team);

        Assert.Null(result);
    }

    /// <summary>ValidateProjectName rejects "memory" (case-insensitive) in Project names.</summary>
    [Fact]
    public void ValidateProjectName_MemoryExactMatch_ReturnsMemoryReservedMessage()
    {
        TeamSummary team = Team("Business", "Marketing Project");

        string? result = TeamNames.ValidateProjectName("memory", team);

        Assert.Equal(TeamNames.MemoryReservedProblem, result);
    }

    /// <summary>ValidateProjectName rejects "Memory" (case-insensitive) in Project names.</summary>
    [Fact]
    public void ValidateProjectName_MemoryDifferentCase_ReturnsMemoryReservedMessage()
    {
        TeamSummary team = Team("Business", "Marketing Project");

        string? result = TeamNames.ValidateProjectName("Memory", team);

        Assert.Equal(TeamNames.MemoryReservedProblem, result);
    }

    /// <summary>ValidateProjectName rejects names starting with underscore.</summary>
    [Fact]
    public void ValidateProjectName_StartsWithUnderscore_ReturnsReservedPrefixMessage()
    {
        TeamSummary team = Team("Business", "Marketing Project");

        string? result = TeamNames.ValidateProjectName("_x", team);

        Assert.Equal("Names starting with \"_\" or \".\" are reserved.", result);
    }

    /// <summary>ValidateProjectName rejects names that already exist in the Team (case-insensitive) using the typed spelling.</summary>
    [Fact]
    public void ValidateProjectName_ExistingProject_ReturnsDuplicateMessage()
    {
        TeamSummary team = Team("Business", "Marketing Project");

        string? result = TeamNames.ValidateProjectName("marketing project", team);

        Assert.Equal("A Project named \"marketing project\" already exists in Business.", result);
    }

    /// <summary>ValidateProjectName allows commas in Project names (they are only restricted for Teams).</summary>
    [Fact]
    public void ValidateProjectName_ContainsComma_ReturnsNull()
    {
        TeamSummary team = Team("Business", "Marketing Project");

        string? result = TeamNames.ValidateProjectName("Sales, EMEA", team);

        Assert.Null(result);
    }

    /// <summary>ValidateProjectName rejects names with colon.</summary>
    [Fact]
    public void ValidateProjectName_ContainsColon_ReturnsNonNull()
    {
        TeamSummary team = Team("Business", "Marketing Project");

        string? result = TeamNames.ValidateProjectName("a:b", team);

        Assert.NotNull(result);
    }

    /// <summary>MemoryFolder constant equals "memory".</summary>
    [Fact]
    public void MemoryFolder_Value_EqualsMemoryString()
    {
        Assert.Equal("memory", TeamNames.MemoryFolder);
    }

    /// <summary>MemoryReservedProblem constant equals the settled text.</summary>
    [Fact]
    public void MemoryReservedProblem_Value_EqualsSettledText()
    {
        Assert.Equal("\"memory\" is reserved for the Team's shared Memory.", TeamNames.MemoryReservedProblem);
    }

    /// <summary>Helper factory for a TeamSummary with no members and HasFolder true.</summary>
    private static TeamSummary Team(string name, params string[] projects)
    {
        return new TeamSummary(name, projects.ToList(), [], HasFolder: true);
    }
}
