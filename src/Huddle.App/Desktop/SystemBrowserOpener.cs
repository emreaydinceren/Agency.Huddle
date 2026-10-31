using System.ComponentModel;
using System.Diagnostics;

namespace Agency.Huddle.App.Desktop;

/// <summary>Opens the browser using the platform's native command.</summary>
internal sealed class SystemBrowserOpener : IBrowserOpener
{
    /// <summary>Build a ProcessStartInfo for opening the given URL, throwing if it is unsafe.</summary>
    /// <param name="url">The URL to open. Must be http scheme, host 127.0.0.1 or localhost exactly.</param>
    /// <param name="platform">The platform to select the command for.</param>
    /// <returns>A ProcessStartInfo ready to pass to Process.Start.</returns>
    /// <exception cref="ArgumentException">The URL is not http, or host is not exactly 127.0.0.1 or localhost.</exception>
    public static ProcessStartInfo BuildStartInfo(Uri url, DesktopPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(url);

        if (!url.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only http scheme is allowed.", nameof(url));
        }

        string host = url.Host;
        if (!host.Equals("127.0.0.1", StringComparison.Ordinal) && !host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only 127.0.0.1 and localhost are allowed.", nameof(url));
        }

        return platform switch
        {
            DesktopPlatform.Windows => new ProcessStartInfo
            {
                FileName = url.AbsoluteUri,
                UseShellExecute = true,
            },
            DesktopPlatform.MacOS => new ProcessStartInfo
            {
                FileName = "/usr/bin/open",
                ArgumentList = { url.AbsoluteUri },
            },
            DesktopPlatform.Linux => new ProcessStartInfo
            {
                FileName = "/usr/bin/xdg-open",
                ArgumentList = { url.AbsoluteUri },
            },
            _ => throw new ArgumentException($"Unknown platform: {platform}", nameof(platform)),
        };
    }

    /// <summary>Open the browser to the given loopback URL on the current OS.</summary>
    /// <param name="url">The URL to open, e.g., http://localhost:5000/.</param>
    /// <remarks>
    /// If the process fails to start (e.g., browser already running and URL already shown), the error
    /// is ignored but not thrown, because the instance information is already in instance.json.
    /// </remarks>
    public void Open(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        Uri uri = new(url);

        DesktopPlatform platform = DesktopPlatform.Linux;
        if (OperatingSystem.IsWindows())
        {
            platform = DesktopPlatform.Windows;
        }
        else if (OperatingSystem.IsMacOS())
        {
            platform = DesktopPlatform.MacOS;
        }

        ProcessStartInfo startInfo = BuildStartInfo(uri, platform);

        try
        {
            using Process? process = Process.Start(startInfo);
        }
        catch (Win32Exception)
        {
            // Browser or handler already running with this URL. The instance file has the URL;
            // a second-launch will find it and hand off.
        }
    }
}
