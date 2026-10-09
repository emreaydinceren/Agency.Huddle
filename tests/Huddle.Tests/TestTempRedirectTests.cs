namespace Agency.Huddle.Tests;

/// <summary>
/// Pins <see cref="TestTempRedirect"/>: what counts as a usable folder, and that a configured redirect
/// really moves <see cref="Path.GetTempPath"/> for this run.
/// </summary>
public sealed class TestTempRedirectTests
{
    /// <summary>A blank request means "not configured", so the system temp folder is kept.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_Blank_ReturnsNull(string? requested)
    {
        Assert.Null(TestTempRedirect.Resolve(requested));
    }

    /// <summary>A relative path would mean a different folder depending on who started the run, so it is refused.</summary>
    [Fact]
    public void Resolve_RelativePath_ReturnsNull()
    {
        Assert.Null(TestTempRedirect.Resolve(Path.Combine("some", "relative", "folder")));
    }

    /// <summary>A rooted folder that does not exist yet is created, and its full path comes back.</summary>
    [Fact]
    public void Resolve_RootedMissingFolder_CreatesItAndReturnsTheFullPath()
    {
        using TempDataDir dir = new();
        string target = Path.Combine(dir.Path, "redirect", "nested");

        string? resolved = TestTempRedirect.Resolve(target);

        Assert.Equal(target, resolved);
        Assert.True(Directory.Exists(target));
    }

    /// <summary>Whitespace around the configured value, as a hand-typed variable often has, is ignored.</summary>
    [Fact]
    public void Resolve_PaddedPath_IsTrimmed()
    {
        using TempDataDir dir = new();
        string target = Path.Combine(dir.Path, "padded");

        string? resolved = TestTempRedirect.Resolve("  " + target + "  ");

        Assert.Equal(target, resolved);
    }

    /// <summary>A path that cannot be created, here one beneath a plain file, falls back instead of throwing, as an unmounted drive would.</summary>
    [Fact]
    public void Resolve_PathBeneathAFile_ReturnsNull()
    {
        using TempDataDir dir = new();
        string file = Path.Combine(dir.Path, "plain-file");
        File.WriteAllText(file, "not a folder");

        Assert.Null(TestTempRedirect.Resolve(Path.Combine(file, "child")));
    }

    /// <summary>
    /// When this machine sets <c>HUDDLE_TEST_TEMP</c> to a usable folder, the module initializer has already
    /// made that folder the temp folder. With it unset (CI, a fresh machine) the test is skipped, not passed.
    /// </summary>
    [Fact]
    public void Apply_WhenConfigured_MakesTheConfiguredFolderTheTempPath()
    {
        string? configured = TestTempRedirect.Resolve(Environment.GetEnvironmentVariable(TestTempRedirect.VariableName));
        if (configured is null)
        {
            Assert.Skip("HUDDLE_TEST_TEMP does not name a usable folder on this machine.");
        }

        Assert.Equal(
            Path.TrimEndingDirectorySeparator(configured),
            Path.TrimEndingDirectorySeparator(Path.GetTempPath()),
            ignoreCase: true);
    }
}
