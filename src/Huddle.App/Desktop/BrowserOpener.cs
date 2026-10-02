using System.ComponentModel;
using System.Diagnostics;

namespace Agency.Huddle.App.Desktop;

/// <summary>The platform the application is running on, for browser-opener selection.</summary>
public enum DesktopPlatform
{
    /// <summary>Windows OS.</summary>
    Windows,

    /// <summary>macOS.</summary>
    MacOS,

    /// <summary>Linux.</summary>
    Linux,
}

/// <summary>Opens the browser to a local loopback URL.</summary>
internal interface IBrowserOpener
{
    /// <summary>Open the browser to the given URL.</summary>
    /// <param name="url">The URL to open, validated to be http://127.0.0.1 or http://localhost with any port.</param>
    void Open(string url);
}

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
                FileName = "open",
                ArgumentList = { url.AbsoluteUri },
            },
            DesktopPlatform.Linux => new ProcessStartInfo
            {
                FileName = "xdg-open",
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

        DesktopPlatform platform = OperatingSystem.IsWindows() ? DesktopPlatform.Windows
            : OperatingSystem.IsMacOS() ? DesktopPlatform.MacOS
            : DesktopPlatform.Linux;

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

/// <summary>A no-op browser opener that does nothing.</summary>
internal sealed class NoopBrowserOpener : IBrowserOpener
{
    /// <summary>Do nothing.</summary>
    /// <param name="url">Ignored.</param>
    public void Open(string url)
    {
    }
}
