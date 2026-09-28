using System.Diagnostics;

namespace Agency.Huddle.Tests.Library;

/// <summary>
/// Creates and removes a directory link for tests that need one on disk: a junction on Windows
/// (the only unelevated way, per Task 0.2), a symbolic link elsewhere. Shared by
/// <see cref="WindowsRecycleBinTests"/> and <see cref="TeamFolderCatalogTests"/>.
/// </summary>
internal static class TestLinks
{
    /// <summary>Creates a directory link at <paramref name="link"/> pointing to <paramref name="target"/>.
    /// Returns <see langword="false"/> when the link could not be created (e.g. no privilege).</summary>
    /// <param name="link">The link's path, not yet existing.</param>
    /// <param name="target">The link's target directory, already existing.</param>
    public static bool TryCreateLink(string link, string target)
    {
        ArgumentNullException.ThrowIfNull(link);
        ArgumentNullException.ThrowIfNull(target);

        try
        {
            if (OperatingSystem.IsWindows())
            {
                ProcessStartInfo startInfo = new("cmd")
                {
                    ArgumentList = { "/c", "mklink", "/J", link, target },
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("mklink did not start.");
                process.WaitForExit();
                return process.ExitCode == 0;
            }

            Directory.CreateSymbolicLink(link, target);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Removes a link created by <see cref="TryCreateLink"/>, before the owning
    /// <see cref="TempDataDir"/> disposes (its cleanup cannot recurse through a reparse point).</summary>
    /// <param name="link">The link's path.</param>
    public static void RemoveLink(string link)
    {
        ArgumentNullException.ThrowIfNull(link);

        if (Directory.Exists(link))
        {
            Directory.Delete(link, recursive: false);
        }
    }
}
