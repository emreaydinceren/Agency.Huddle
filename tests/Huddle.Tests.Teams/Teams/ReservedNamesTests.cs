using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Pins SB-R02: memory, raw and outputs are reserved Team and Project names, ignoring case.</summary>
public sealed class ReservedNamesTests
{
    /// <summary>ValidateTeamName refuses memory, raw and outputs in any letter case, quoting the name as typed.</summary>
    [Theory]
    [InlineData("memory", "\"memory\" is reserved for a Team's own folders.")]
    [InlineData("Raw", "\"Raw\" is reserved for a Team's own folders.")]
    [InlineData("OUTPUTS", "\"OUTPUTS\" is reserved for a Team's own folders.")]
    public void ValidateTeamName_ReservedFolderName_ReturnsReservedMessage(string name, string expected)
    {
        string? result = TeamNames.ValidateTeamName(name, []);

        Assert.Equal(expected, result);
    }

    /// <summary>ValidateTeamName accepts an ordinary Team name that is not reserved.</summary>
    [Fact]
    public void ValidateTeamName_OrdinaryName_ReturnsNull()
    {
        string? result = TeamNames.ValidateTeamName("Marketing", []);

        Assert.Null(result);
    }

    /// <summary>ValidateProjectName refuses raw with the message for files brought into the Team.</summary>
    [Fact]
    public void ValidateProjectName_Raw_ReturnsRawReservedMessage()
    {
        TeamSummary team = Team("Business", "Marketing Project");

        string? result = TeamNames.ValidateProjectName("raw", team);

        Assert.Equal("\"raw\" is reserved for files brought into the Team.", result);
    }

    /// <summary>ValidateProjectName refuses Outputs in any letter case with the message for work the Team produces.</summary>
    [Fact]
    public void ValidateProjectName_OutputsMixedCase_ReturnsOutputsReservedMessage()
    {
        TeamSummary team = Team("Business", "Marketing Project");

        string? result = TeamNames.ValidateProjectName("Outputs", team);

        Assert.Equal("\"outputs\" is reserved for work the Team produces.", result);
    }

    /// <summary>ValidateProjectName refuses memory with the existing memory-reserved message.</summary>
    [Fact]
    public void ValidateProjectName_Memory_ReturnsMemoryReservedProblem()
    {
        TeamSummary team = Team("Business", "Marketing Project");

        string? result = TeamNames.ValidateProjectName("memory", team);

        Assert.Equal(TeamNames.MemoryReservedProblem, result);
    }

    /// <summary>Uploads is no longer reserved for Project names, so ValidateProjectName accepts it.</summary>
    [Fact]
    public void ValidateProjectName_Uploads_ReturnsNull()
    {
        TeamSummary team = Team("Business", "Marketing Project");

        string? result = TeamNames.ValidateProjectName("Uploads", team);

        Assert.Null(result);
    }

    /// <summary>IsReservedTeamName is true for raw in upper case.</summary>
    [Fact]
    public void IsReservedTeamName_RawUpperCase_ReturnsTrue()
    {
        bool result = TeamNames.IsReservedTeamName("RAW");

        Assert.True(result);
    }

    /// <summary>IsReservedTeamName is false for an ordinary name such as Home.</summary>
    [Fact]
    public void IsReservedTeamName_Home_ReturnsFalse()
    {
        bool result = TeamNames.IsReservedTeamName("Home");

        Assert.False(result);
    }

    /// <summary>The raw and outputs folder constants hold the lower-case folder names.</summary>
    [Fact]
    public void FolderConstants_RawAndOutputs_AreLowerCaseFolderNames()
    {
        Assert.Equal("raw", TeamNames.RawFolder);
        Assert.Equal("outputs", TeamNames.OutputsFolder);
    }

    /// <summary>Builds a Team summary with the given Projects for the Project-name rules.</summary>
    /// <param name="name">The Team name.</param>
    /// <param name="projects">The Project names already in the Team.</param>
    /// <returns>A Team summary holding those Projects.</returns>
    private static TeamSummary Team(string name, params string[] projects)
    {
        return new TeamSummary(name, projects.ToList(), [], HasFolder: true);
    }
}
