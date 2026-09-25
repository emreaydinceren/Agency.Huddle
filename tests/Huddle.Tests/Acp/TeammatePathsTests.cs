using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Acp;

/// <summary>TeammatePaths constructs and caches paths for teammate definitions and work directories
/// under the ADR-0031 layout, where each Teammate has its own folder holding its definition file and
/// its Work Dir as a sub-folder.</summary>
public sealed class TeammatePathsTests
{
    /// <summary>DefinitionFile with null throws ArgumentNullException.</summary>
    [Fact]
    public void DefinitionFile_Null_ThrowsArgumentNull()
    {
        using TempDataDir tempDir = new();
        IOptions<TeamOptions> options = tempDir.Options();
        TeammatePaths paths = new(options);

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
        TeammatePaths paths = new(options);

        _ = Assert.Throws<ArgumentException>(() => { paths.DefinitionFile(name); });
    }

    /// <summary>WorkDir with null throws ArgumentNullException.</summary>
    [Fact]
    public void WorkDir_Null_ThrowsArgumentNull()
    {
        using TempDataDir tempDir = new();
        IOptions<TeamOptions> options = tempDir.Options();
        TeammatePaths paths = new(options);

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
        TeammatePaths paths = new(options);

        _ = Assert.Throws<ArgumentException>(() => { paths.WorkDir(name); });
    }

    /// <summary>TeammateFolder returns the Teammates directory joined with the teammate's name.</summary>
    [Fact]
    public void TeammateFolder_Name_IsTeammatesName()
    {
        using TempDataDir tempDir = new();
        IOptions<TeamOptions> options = tempDir.Options();
        TeammatePaths paths = new(options);

        string result = paths.TeammateFolder("Nova");

        string expected = Path.Combine(tempDir.Path, "Teammates", "Nova");
        Assert.Equal(expected, result);
    }

    /// <summary>DefinitionFile returns the definition file inside the teammate's own folder.</summary>
    [Fact]
    public void DefinitionFile_Name_IsInsideTeammateFolder()
    {
        using TempDataDir tempDir = new();
        IOptions<TeamOptions> options = tempDir.Options();
        TeammatePaths paths = new(options);

        string result = paths.DefinitionFile("Nova");

        string expected = Path.Combine(tempDir.Path, "Teammates", "Nova", "Nova.md");
        Assert.Equal(expected, result);
    }

    /// <summary>WorkDir returns the configured Work Dir folder name as a sub-folder of the teammate's own folder.</summary>
    [Fact]
    public void WorkDir_Name_IsWorkInsideTeammateFolder()
    {
        using TempDataDir tempDir = new();
        IOptions<TeamOptions> options = tempDir.Options();
        TeammatePaths paths = new(options);

        string result = paths.WorkDir("Nova");

        string expected = Path.Combine(tempDir.Path, "Teammates", "Nova", "work");
        Assert.Equal(expected, result);
    }

    /// <summary>DefinitionsRoot returns the Teammates directory.</summary>
    [Fact]
    public void DefinitionsRoot_IsTeammatesDir()
    {
        using TempDataDir tempDir = new();
        IOptions<TeamOptions> options = tempDir.Options();
        TeammatePaths paths = new(options);

        string result = paths.DefinitionsRoot;

        string expected = Path.Combine(tempDir.Path, "Teammates");
        Assert.Equal(expected, result);
    }

    /// <summary>Paths honour a configured TeammatesDir and WorkDir folder name.</summary>
    [Fact]
    public void Paths_HonourConfiguredDirs()
    {
        using TempDataDir tempDir = new();
        IOptions<TeamOptions> options = tempDir.Options();
        options.Value.Acp.TeammatesDir = "M";
        options.Value.Acp.WorkDir = "w";

        TeammatePaths paths = new(options);

        string expectedDefinitionsRoot = Path.Combine(tempDir.Path, "M");
        string expectedTeammateFolder = Path.Combine(tempDir.Path, "M", "Nova");
        string expectedDefinitionFile = Path.Combine(tempDir.Path, "M", "Nova", "Nova.md");
        string expectedWorkDir = Path.Combine(tempDir.Path, "M", "Nova", "w");

        Assert.Equal(expectedDefinitionsRoot, paths.DefinitionsRoot);
        Assert.Equal(expectedTeammateFolder, paths.TeammateFolder("Nova"));
        Assert.Equal(expectedDefinitionFile, paths.DefinitionFile("Nova"));
        Assert.Equal(expectedWorkDir, paths.WorkDir("Nova"));
    }
}
