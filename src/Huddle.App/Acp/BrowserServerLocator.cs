namespace Agency.Huddle.App.Acp;

/// <summary>
/// Finds the Playwright MCP entry point and an installed browser, and reads the display. The core takes
/// its probes as arguments so tests start no process and touch no real file system. It never downloads a browser.
/// </summary>
internal static class BrowserServerLocator
{
    private const string RelativeCliPath = "tools/acp/node_modules/@playwright/mcp/cli.js";

    private const string MacChrome = "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome";

    private const string MacEdge = "/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge";

    private static readonly string[] LinuxChrome =
        ["/opt/google/chrome/chrome", "/usr/bin/google-chrome", "/usr/bin/google-chrome-stable"];

    private static readonly string[] LinuxEdge =
        ["/opt/microsoft/msedge/msedge", "/usr/bin/microsoft-edge", "/usr/bin/microsoft-edge-stable"];

    private static readonly string[] WindowsProgramFilesVariables = ["ProgramFiles(x86)", "ProgramFiles"];

    /// <summary>Gathers the facts for the running computer.</summary>
    /// <param name="options">The <c>Team:Browser</c> section.</param>
    /// <param name="startDirectory">The directory to walk up from to find <c>tools/acp</c>.</param>
    /// <param name="adapterPath">The Adapter profile's path, or <see langword="null"/>.</param>
    /// <returns>What was found.</returns>
    internal static BrowserLaunchFacts Locate(BrowserOptions options, string startDirectory, string? adapterPath)
    {
        return Locate(
            options,
            startDirectory,
            adapterPath,
            File.Exists,
            static name => Environment.GetEnvironmentVariable(name),
            CurrentPlatform());
    }

    /// <summary>Gathers the facts from the given probes.</summary>
    /// <param name="options">The <c>Team:Browser</c> section.</param>
    /// <param name="startDirectory">The directory to walk up from to find <c>tools/acp</c>.</param>
    /// <param name="adapterPath">The Adapter profile's path, or <see langword="null"/>.</param>
    /// <param name="fileExists">Whether a file exists.</param>
    /// <param name="readEnvironment">Reads an environment variable, or returns <see langword="null"/>.</param>
    /// <param name="platform">The operating system.</param>
    /// <returns>What was found.</returns>
    internal static BrowserLaunchFacts Locate(
        BrowserOptions options,
        string startDirectory,
        string? adapterPath,
        Func<string, bool> fileExists,
        Func<string, string?> readEnvironment,
        BrowserPlatform platform)
    {
        string? cli = LocateCli(startDirectory, adapterPath, fileExists);
        string? channel = string.IsNullOrWhiteSpace(options.Channel)
            ? LocateChannel(fileExists, readEnvironment, platform)
            : options.Channel.Trim();
        bool noDisplay = platform == BrowserPlatform.Linux
            && string.IsNullOrEmpty(readEnvironment("DISPLAY"))
            && string.IsNullOrEmpty(readEnvironment("WAYLAND_DISPLAY"));
        return new BrowserLaunchFacts(
            options.Enabled,
            cli,
            channel,
            options.Headless || noDisplay,
            noDisplay,
            options.IdleTimeoutMinutes);
    }

    private static BrowserPlatform CurrentPlatform()
    {
        if (OperatingSystem.IsWindows())
        {
            return BrowserPlatform.Windows;
        }

        return OperatingSystem.IsMacOS() ? BrowserPlatform.MacOS : BrowserPlatform.Linux;
    }

    private static string? LocateCli(string startDirectory, string? adapterPath, Func<string, bool> fileExists)
    {
        string? beside = BesideAdapter(adapterPath, fileExists);
        if (beside is not null)
        {
            return beside;
        }

        string relative = RelativeCliPath.Replace('/', Path.DirectorySeparatorChar);
        string? directory = startDirectory;
        while (!string.IsNullOrEmpty(directory))
        {
            string candidate = Path.Combine(directory, relative);
            if (fileExists(candidate))
            {
                return candidate;
            }

            directory = Path.GetDirectoryName(directory);
        }

        return null;
    }

    private static string? BesideAdapter(string? adapterPath, Func<string, bool> fileExists)
    {
        string? directory = string.IsNullOrEmpty(adapterPath) ? null : Path.GetDirectoryName(adapterPath);
        while (directory is not null)
        {
            if (string.Equals(Path.GetFileName(directory), "node_modules", StringComparison.Ordinal))
            {
                string candidate = Path.Combine(directory, "@playwright", "mcp", "cli.js");
                return fileExists(candidate) ? candidate : null;
            }

            directory = Path.GetDirectoryName(directory);
        }

        return null;
    }

    private static string? LocateChannel(Func<string, bool> fileExists, Func<string, string?> readEnvironment, BrowserPlatform platform)
    {
        switch (platform)
        {
            case BrowserPlatform.Windows:
                foreach (string variable in WindowsProgramFilesVariables)
                {
                    string? root = readEnvironment(variable);
                    if (!string.IsNullOrEmpty(root)
                        && fileExists(Path.Combine(root, "Microsoft", "Edge", "Application", "msedge.exe")))
                    {
                        return "msedge";
                    }
                }

                return null;
            case BrowserPlatform.MacOS:
                return FirstInstalled(fileExists, [MacChrome], [MacEdge]);
            default:
                return FirstInstalled(fileExists, LinuxChrome, LinuxEdge);
        }
    }

    private static string? FirstInstalled(Func<string, bool> fileExists, string[] chrome, string[] edge)
    {
        if (chrome.Any(fileExists))
        {
            return "chrome";
        }

        return edge.Any(fileExists) ? "msedge" : null;
    }
}
