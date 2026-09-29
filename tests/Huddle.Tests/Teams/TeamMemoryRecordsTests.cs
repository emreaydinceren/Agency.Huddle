using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Tests for the Team Memory record shapes.</summary>
public sealed class TeamMemoryRecordsTests
{
    /// <summary>TeamMemoryGroup holds Team, TeamMemoryPath, TeamWide snapshot, and Projects list.</summary>
    [Fact]
    public void TeamMemoryGroup_Holds_TeamProjectsAndPaths()
    {
        MemoryEntry entry1 = new("Summary 1", "C:/path/to/entry1.md");
        MemorySnapshot teamWide = new("C:/teams/business/memory", [entry1], 2);

        MemoryEntry entry2 = new("Summary 2", "C:/path/to/entry2.md");
        MemorySnapshot projectMemory = new("C:/teams/business/marketing/memory", [entry2], 1);

        TeamMemoryGroup group = new(
            Team: "Business",
            TeamMemoryPath: "C:/teams/business/memory",
            TeamWide: teamWide,
            Projects:
            [
                ("Marketing", projectMemory)
            ]
        );

        Assert.Equal("Business", group.Team);
        Assert.Equal("C:/teams/business/memory", group.TeamMemoryPath);
        Assert.Equal(teamWide, group.TeamWide);
        Assert.Single(group.Projects);
        Assert.Equal(("Marketing", projectMemory), group.Projects[0]);
    }

    /// <summary>TeamMemorySnapshot holds Groups and NotListed count.</summary>
    [Fact]
    public void TeamMemorySnapshot_Holds_GroupsAndNotListed()
    {
        MemoryEntry entry = new("Summary", "C:/path/to/entry.md");
        MemorySnapshot teamWide = new("C:/teams/business/memory", [entry], 0);

        TeamMemoryGroup group = new(
            Team: "Business",
            TeamMemoryPath: "C:/teams/business/memory",
            TeamWide: teamWide,
            Projects: []
        );

        TeamMemorySnapshot snapshot = new(
            Groups: [group],
            NotListed: 5
        );

        Assert.Single(snapshot.Groups);
        Assert.Equal(group, snapshot.Groups[0]);
        Assert.Equal(5, snapshot.NotListed);
    }

    /// <summary>TeamMemorySnapshot empty has no groups and zero NotListed.</summary>
    [Fact]
    public void TeamMemorySnapshot_Empty_HasNoGroups()
    {
        TeamMemorySnapshot snapshot = new(
            Groups: [],
            NotListed: 0
        );

        Assert.Empty(snapshot.Groups);
        Assert.Equal(0, snapshot.NotListed);
    }
}
