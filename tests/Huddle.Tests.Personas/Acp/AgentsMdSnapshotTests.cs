using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// Pins the shape of the AGENTS.md snapshot types (Spec §6.2): the empty snapshot, one section's values, and
/// the three levels a section can come from.
/// </summary>
public sealed class AgentsMdSnapshotTests
{
    /// <summary>The empty snapshot has no sections and nothing omitted.</summary>
    [Fact]
    public void Empty_HasNoSectionsAndNothingOmitted()
    {
        AgentsMdSnapshot snapshot = AgentsMdSnapshot.Empty;

        Assert.Empty(snapshot.Sections);
        Assert.Empty(snapshot.Omitted);
    }

    /// <summary>A section keeps the level, team, project, path, text and frontmatter flag it was built with.</summary>
    [Fact]
    public void Section_ExposesEveryValueItWasBuiltWith()
    {
        AgentsMdSection section = new(AgentsMdLevel.Team, "Home", null, "Teams/Home/AGENTS.md", "text", HadFrontmatter: false);

        Assert.Equal(AgentsMdLevel.Team, section.Level);
        Assert.Equal("Home", section.Team);
        Assert.Null(section.Project);
        Assert.Equal("Teams/Home/AGENTS.md", section.Path);
        Assert.Equal("text", section.Text);
        Assert.False(section.HadFrontmatter);
    }

    /// <summary>The level enum has exactly the three levels, in general-to-specific order.</summary>
    [Fact]
    public void Level_HasExactlyHuddleWideTeamAndProject()
    {
        string[] expected = ["HuddleWide", "Team", "Project"];

        Assert.Equal(expected, Enum.GetNames<AgentsMdLevel>());
    }
}
