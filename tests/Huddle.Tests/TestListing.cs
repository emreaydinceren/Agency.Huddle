using System.Diagnostics;

namespace Agency.Huddle.Tests;

/// <summary>Makes a folder unlistable for the current user, and back, so a test can prove how a service
/// behaves when it cannot enumerate a folder. Shared by every test class that needs an unreadable folder.</summary>
internal static class TestListing
{
    /// <summary>Denies the current user the right to list <paramref name="path"/>'s contents: <c>icacls</c>
    /// on Windows (no ACL package is referenced by this solution), or <see cref="UnixFileMode.None"/> via
    /// <see cref="File.SetUnixFileMode(string, UnixFileMode)"/> on Linux/macOS. Running as root - the CI
    /// container's user - ignores the Unix mode entirely, so probe with <see cref="ListingIsDenied"/> and
    /// skip when the denial is not enforced.</summary>
    /// <param name="path">The folder to lock.</param>
    public static void DenyListing(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            RunIcacls(path, "/inheritance:r", "/deny", $"{Environment.UserName}:(RD)");
        }
        else
        {
            File.SetUnixFileMode(path, UnixFileMode.None);
        }
    }

    /// <summary>Reverses <see cref="DenyListing"/> so <see cref="TempDataDir.Dispose"/> can clean up.</summary>
    /// <param name="path">The folder to unlock.</param>
    public static void GrantListing(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            RunIcacls(path, "/reset");
        }
        else
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    /// <summary>True when listing <paramref name="path"/> now fails, i.e. the denial was enforced (it is not for root on Linux).</summary>
    /// <param name="path">The folder that was locked.</param>
    public static bool ListingIsDenied(string path)
    {
        try
        {
            _ = Directory.EnumerateFiles(path).Any();
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The listing failed, which is the denial being enforced.
            return true;
        }
    }

    /// <summary>Runs <c>icacls</c> with the given arguments and waits for it to exit.</summary>
    /// <param name="arguments">The command-line arguments, passed unquoted via <see cref="ProcessStartInfo.ArgumentList"/>.</param>
    private static void RunIcacls(params string[] arguments)
    {
        ProcessStartInfo info = new("icacls") { UseShellExecute = false, CreateNoWindow = true };
        foreach (string argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(info) ?? throw new InvalidOperationException("icacls failed to start.");
        process.WaitForExit();
    }
}
