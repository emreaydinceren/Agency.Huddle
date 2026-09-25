using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Acp;

/// <summary>TeammatePaths constructs and caches paths for teammate definitions and work directories.</summary>
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

    /// <summary>DefinitionFile returns Teammates directory joined with name and .md extension.</summary>
    [Fact]
    public void DefinitionFile_Name_IsTeammatesDirNameDotMd()
    {
        using TempDataDir tempDir = new();
        IOptions<TeamOptions> options = tempDir.Options();
        TeammatePaths paths = new(options);

        string result = paths.DefinitionFile("Nova");

        string expected = Path.Combine(tempDir.Path, "Teammates", "Nova.md");
        Assert.Equal(expected, result);
    }

    /// <summary>WorkDir returns work root directory joined with name.</summary>
    [Fact]
    public void WorkDir_Name_IsWorkRootName()
    {
        using TempDataDir tempDir = new();
        IOptions<TeamOptions> options = tempDir.Options();
        TeammatePaths paths = new(options);

        string result = paths.WorkDir("Nova");

        string expected = Path.Combine(tempDir.Path, "work", "Nova");
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

    /// <summary>WorkDirRoot returns the work directory.</summary>
    [Fact]
    public void WorkDirRoot_IsWorkDir()
    {
        using TempDataDir tempDir = new();
        IOptions<TeamOptions> options = tempDir.Options();
        TeammatePaths paths = new(options);

        string result = paths.WorkDirRoot;

        string expected = Path.Combine(tempDir.Path, "work");
        Assert.Equal(expected, result);
    }

    /// <summary>Paths honour configured TeammatesDir and WorkDir options.</summary>
    [Fact]
    public void Paths_HonourConfiguredDirs()
    {
        using TempDataDir tempDir = new();
        IOptions<TeamOptions> options = tempDir.Options();
        options.Value.Acp.TeammatesDir = "P";
        options.Value.Acp.WorkDir = "W";

        TeammatePaths paths = new(options);

        string expectedDefinitionsRoot = Path.Combine(tempDir.Path, "P");
        string expectedWorkDirRoot = Path.Combine(tempDir.Path, "W");
        string expectedDefinitionFile = Path.Combine(tempDir.Path, "P", "Nova.md");
        string expectedWorkDir = Path.Combine(tempDir.Path, "W", "Nova");

        Assert.Equal(expectedDefinitionsRoot, paths.DefinitionsRoot);
        Assert.Equal(expectedWorkDirRoot, paths.WorkDirRoot);
        Assert.Equal(expectedDefinitionFile, paths.DefinitionFile("Nova"));
        Assert.Equal(expectedWorkDir, paths.WorkDir("Nova"));
    }
}
