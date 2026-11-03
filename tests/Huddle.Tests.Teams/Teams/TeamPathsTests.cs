using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Pins the data-folder names in <see cref="TeamPaths"/> (plan C8).</summary>
public sealed class TeamPathsTests
{
    /// <summary>Every constant holds its documented value.</summary>
    [Fact]
    public void Constants_HoldDocumentedValues()
    {
        Assert.Equal("TeamTypes", TeamPaths.TeamTypesDir);
        Assert.Equal("SkillPacks", TeamPaths.SkillPacksDir);
        Assert.Equal("SkillsCatalog.json", TeamPaths.SkillsCatalogFile);
        Assert.Equal("_team.md", TeamPaths.TeamFileName);
        Assert.Equal("AGENTS.md", TeamPaths.AgentsMdFileName);
        Assert.Equal("Retro register.md", TeamPaths.RetroRegisterFileName);
        Assert.Equal("_retros", TeamPaths.RetrosDir);
        Assert.Equal("logs/tool-calls", TeamPaths.ToolCallLogDir);
    }
}
