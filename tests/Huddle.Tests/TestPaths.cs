namespace Agency.Huddle.Tests;

/// <summary>Where a test puts its scratch folders, given what the test needs from the disk.</summary>
internal static class TestPaths
{
    /// <summary>
    /// The temp folder for a test. By default it is <see cref="Path.GetTempPath"/>, which follows the
    /// <c>HUDDLE_TEST_TEMP</c> redirect (see <see cref="TestTempRedirect"/>). A test that creates a directory
    /// link and reads it back passes <see langword="true"/> to get the system temp folder instead (saved in <c>HUDDLE_TEST_SYSTEM_TEMP</c> when the redirect applies), because a
    /// redirected volume such as a RAM disk may create links but fail to resolve them (<c>Incorrect function</c>).
    /// </summary>
    /// <param name="supportsReparsePoints"><see langword="true"/> when the test needs a volume that resolves junctions and symbolic links.</param>
    /// <returns>The folder to build the test's scratch folders under.</returns>
    public static string GetTempPath(bool supportsReparsePoints = false)
    {
        if (supportsReparsePoints && Environment.GetEnvironmentVariable(TestTempRedirect.SystemTempVariableName) is { Length: > 0 } systemTemp)
        {
            return systemTemp;
        }

        return Path.GetTempPath();
    }
}