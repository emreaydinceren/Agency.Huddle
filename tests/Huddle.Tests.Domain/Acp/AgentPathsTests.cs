using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Acp;

/// <summary>AgentPaths constructs and caches paths for agent definitions and work directories
/// under the ADR-0031 layout, where each Agent has its own folder holding its definition file and
/// its Work Dir as a sub-folder.</summary>
public sealed class AgentPathsTests
{
    /// <summary>DefinitionFile with null throws ArgumentNullException.</summary>
    [Fact]
    public void DefinitionFile_Null_ThrowsArgumentNull()
    {
        using TempDataDir tempDir = new();
        IOptions<TeamOptions> options = tempDir.Options();
        AgentPaths paths = new(options);

        _ = Assert.Throws<ArgumentNullException>(() => { paths.DefinitionFile(null!); });
    }

    /// <summary>DefinitionFile with empty string throws ArgumentException.</summary>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void DefinitionFile_NullOrWhitespace_Throws(string name)
    {
        using TempDataDir tempDir = new();
        IOptions<TeamOptions> options = tempDir.Options();
        AgentPaths paths = new(options);

        _ = Assert.Throws<ArgumentException>(() => { paths.DefinitionFile(name); });
    }

    /// <summary>WorkDir with null throws ArgumentNullException.</summary>
    [Fact]
    public void WorkDir_Null_ThrowsArgumentNull()
    {
        using TempDataDir tempDir = new();
        IOptions<TeamOptions> options = tempDir.Options();
        AgentPaths paths = new(options);

        _ = Assert.Throws<ArgumentNullException>(() => { paths.WorkDir(null!); });
    }

    /// <summary>WorkDir with empty string throws ArgumentException.</summary>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void WorkDir_NullOrWhitespace_Throws(string name)
    {
        using TempDataDir tempDir = new();
        IOptions<TeamOptions> options = tempDir.Options();
        AgentPaths paths = new(options);

        _ = Assert.Throws<ArgumentException>(() => { paths.WorkDir(name); });
    }

    /// <summary>AgentFolder returns the Agents directory joined with the agent's name.</summary>
    [Fact]
    public void AgentFolder_Name_IsAgentsName()
    {
        using TempDataDir tempDir = new();
        IOptions<TeamOptions> options = tempDir.Options();
        AgentPaths paths = new(options);

        string result = paths.AgentFolder("Nova");

        string expected = Path.Combine(tempDir.Path, "Agents", "Nova");
        Assert.Equal(expected, result);
    }

    /// <summary>DefinitionFile returns the definition file inside the agent's own folder.</summary>
    [Fact]
    public void DefinitionFile_Name_IsInsideAgentFolder()
    {
        using TempDataDir tempDir = new();
        IOptions<TeamOptions> options = tempDir.Options();
        AgentPaths paths = new(options);

        string result = paths.DefinitionFile("Nova");

        string expected = Path.Combine(tempDir.Path, "Agents", "Nova", "Nova.md");
        Assert.Equal(expected, result);
    }

    /// <summary>WorkDir returns the configured Work Dir folder name as a sub-folder of the agent's own folder.</summary>
    [Fact]
    public void WorkDir_Name_IsWorkInsideAgentFolder()
    {
        using TempDataDir tempDir = new();
        IOptions<TeamOptions> options = tempDir.Options();
        AgentPaths paths = new(options);

        string result = paths.WorkDir("Nova");

        string expected = Path.Combine(tempDir.Path, "Agents", "Nova", "work");
        Assert.Equal(expected, result);
    }

    /// <summary>DefinitionsRoot returns the Agents directory.</summary>
    [Fact]
    public void DefinitionsRoot_IsAgentsDir()
    {
        using TempDataDir tempDir = new();
        IOptions<TeamOptions> options = tempDir.Options();
        AgentPaths paths = new(options);

        string result = paths.DefinitionsRoot;

        string expected = Path.Combine(tempDir.Path, "Agents");
        Assert.Equal(expected, result);
    }

    /// <summary>Paths honour a configured AgentsDir and WorkDir folder name.</summary>
    [Fact]
    public void Paths_HonourConfiguredDirs()
    {
        using TempDataDir tempDir = new();
        IOptions<TeamOptions> options = tempDir.Options();
        options.Value.Acp.AgentsDir = "M";
        options.Value.Acp.WorkDir = "w";

        AgentPaths paths = new(options);

        string expectedDefinitionsRoot = Path.Combine(tempDir.Path, "M");
        string expectedAgentFolder = Path.Combine(tempDir.Path, "M", "Nova");
        string expectedDefinitionFile = Path.Combine(tempDir.Path, "M", "Nova", "Nova.md");
        string expectedWorkDir = Path.Combine(tempDir.Path, "M", "Nova", "w");

        Assert.Equal(expectedDefinitionsRoot, paths.DefinitionsRoot);
        Assert.Equal(expectedAgentFolder, paths.AgentFolder("Nova"));
        Assert.Equal(expectedDefinitionFile, paths.DefinitionFile("Nova"));
        Assert.Equal(expectedWorkDir, paths.WorkDir("Nova"));
    }
}
