using System.Runtime.CompilerServices;

namespace Agency.Huddle.Tests;

/// <summary>
/// Lets a developer machine put the test run's temp folder somewhere other than the system drive,
/// for this test assembly only. Every test builds its data directory from <see cref="Path.GetTempPath"/>
/// (<see cref="TempDataDir"/> and about forty other files), and the SQLite-backed tests flush to disk
/// thousands of times a run, so on a Windows box whose temp folder is on the system drive a native run
/// is both slow and hard on that drive. Pointing the temp folder at a RAM disk fixes both.
/// </summary>
/// <remarks>
/// <para>
/// If the <c>HUDDLE_TEST_TEMP</c> environment variable names a usable folder, the test process uses it
/// as its temp folder; otherwise it keeps the system temp folder. Nothing else is configurable and
/// nothing is global: the redirect sets <c>TEMP</c>, <c>TMP</c> and <c>TMPDIR</c> on this test process
/// only (child processes it starts inherit them), and never writes to the user or machine environment.
/// With the variable unset, which is how CI runs, this type does nothing.
/// </para>
/// <para>
/// A folder that cannot be created (an unmounted RAM disk, a read-only path) or a relative path is
/// ignored and the system temp folder is used, so a missing drive never fails the whole run.
/// </para>
/// </remarks>
internal static class TestTempRedirect
{
    /// <summary>The environment variable that names the temp folder for the test run.</summary>
    internal const string VariableName = "HUDDLE_TEST_TEMP";

    /// <summary>
    /// The environment variable that keeps the system temp folder as it was before <see cref="Apply"/> redirected
    /// <c>TEMP</c>. It is set only when a redirect happens, and <see cref="TestPaths.GetTempPath"/> reads it.
    /// </summary>
    internal const string SystemTempVariableName = "HUDDLE_TEST_SYSTEM_TEMP";

    /// <summary>Runs once when the test assembly loads, before the first test touches the temp folder.</summary>
    [ModuleInitializer]
    internal static void Apply()
    {

        string? folder = Resolve(Environment.GetEnvironmentVariable(VariableName));
        if (folder is null)
        {
            return;
        }

        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(SystemTempVariableName)))
        {
            Environment.SetEnvironmentVariable(SystemTempVariableName, Path.GetTempPath());
        }

        Environment.SetEnvironmentVariable("TEMP", folder);
        Environment.SetEnvironmentVariable("TMP", folder);
        Environment.SetEnvironmentVariable("TMPDIR", folder);
    }

    /// <summary>
    /// Turns a requested folder into one that exists, or <see langword="null"/> when it is blank, relative
    /// (a relative path would depend on the working directory of whoever started the run) or cannot be created.
    /// </summary>
    /// <param name="requested">The folder as written in the environment variable.</param>
    /// <returns>The full path of the existing folder, or <see langword="null"/> to keep the system temp.</returns>
    internal static string? Resolve(string? requested)
    {
        if (string.IsNullOrWhiteSpace(requested))
        {
            return null;
        }

        string trimmed = requested.Trim();
        if (!Path.IsPathRooted(trimmed))
        {
            return null;
        }

        try
        {
            string full = Path.GetFullPath(trimmed);
            Directory.CreateDirectory(full);
            return full;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // An unmounted drive or an unwritable path falls back to the system temp rather than failing every test.
            return null;
        }
    }
}
