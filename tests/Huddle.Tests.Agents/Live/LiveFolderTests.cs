using Agency.Huddle.Tests.Live.DocHistory;

namespace Agency.Huddle.Tests.Live;

/// <summary>
/// Folders the real Adapter works in stay off a redirected temp folder. Claude Code resolves its working folder's final
/// path before every tool call and refuses the call when Windows cannot. A RAM disk that is not registered with the
/// mount manager cannot (<c>Incorrect function</c>), so with <c>HUDDLE_TEST_TEMP=R:\t</c> every paid probe failed every
/// tool call. These tests run free and are skipped where no redirect applies, which is how CI runs.
/// </summary>
public sealed class LiveFolderTests
{
    /// <summary>With the temp folder redirected, the folder for the real Adapter is the system temp folder the redirect saved.</summary>
    [Fact]
    public void GetAdapterTempPath_TempRedirected_IsTheSystemTemp()
    {
        string systemTemp = SystemTempOrSkip();

        Assert.Equal(Path.TrimEndingDirectorySeparator(systemTemp), Path.TrimEndingDirectorySeparator(TestPaths.GetAdapterTempPath()), ignoreCase: true);
    }

    /// <summary>With the temp folder redirected, a DocHistory lane's working folder is made directly under the system temp folder.</summary>
    [Fact]
    public void NewFolder_TempRedirected_IsUnderTheSystemTemp()
    {
        string systemTemp = SystemTempOrSkip();
        string folder = LiveAgentLane.NewFolder("placement");
        try
        {
            Assert.Equal(Path.TrimEndingDirectorySeparator(systemTemp), Path.GetDirectoryName(folder), ignoreCase: true);
        }
        finally
        {
            Directory.Delete(folder);
        }
    }

    /// <summary>The system temp folder the redirect saved, or a skip when this process was not redirected.</summary>
    /// <returns>The saved system temp folder.</returns>
    private static string SystemTempOrSkip()
    {
        string? systemTemp = Environment.GetEnvironmentVariable(TestTempRedirect.SystemTempVariableName);
        if (string.IsNullOrEmpty(systemTemp))
        {
            Assert.Skip("HUDDLE_TEST_TEMP does not redirect this test process.");
        }

        return systemTemp;
    }
}
