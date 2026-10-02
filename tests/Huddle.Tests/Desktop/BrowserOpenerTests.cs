namespace Agency.Huddle.Tests.Desktop;

using Agency.Huddle.App.Desktop;

/// <summary>Pins browser opener commands per OS and URL validation (Spec §8 IBrowserOpener).</summary>
public sealed class BrowserOpenerTests
{
    /// <summary>Windows: BuildStartInfo returns FileName as the URL and UseShellExecute=true.</summary>
    [Fact]
    public void BuildStartInfo_Windows_ReturnsUrlWithUseShellExecute()
    {
        Uri uri = new("http://127.0.0.1:5123/");

        System.Diagnostics.ProcessStartInfo startInfo = SystemBrowserOpener.BuildStartInfo(uri, DesktopPlatform.Windows);

        Assert.Equal("http://127.0.0.1:5123/", startInfo.FileName);
        Assert.True(startInfo.UseShellExecute);
    }

    /// <summary>macOS: BuildStartInfo returns "open" with the URL in ArgumentList.</summary>
    [Fact]
    public void BuildStartInfo_MacOS_ReturnsOpenCommandWithUrl()
    {
        Uri uri = new("http://127.0.0.1:5123/");

        System.Diagnostics.ProcessStartInfo startInfo = SystemBrowserOpener.BuildStartInfo(uri, DesktopPlatform.MacOS);

        Assert.Equal("open", startInfo.FileName);
        Assert.Single(startInfo.ArgumentList, "http://127.0.0.1:5123/");
    }

    /// <summary>Linux: BuildStartInfo returns "xdg-open" with the URL in ArgumentList.</summary>
    [Fact]
    public void BuildStartInfo_Linux_ReturnsXdgOpenWithUrl()
    {
        Uri uri = new("http://127.0.0.1:5123/");

        System.Diagnostics.ProcessStartInfo startInfo = SystemBrowserOpener.BuildStartInfo(uri, DesktopPlatform.Linux);

        Assert.Equal("xdg-open", startInfo.FileName);
        Assert.Single(startInfo.ArgumentList, "http://127.0.0.1:5123/");
    }

    /// <summary>HTTPS to localhost is rejected: only HTTP is allowed.</summary>
    [Fact]
    public void BuildStartInfo_HttpsUrl_Throws()
    {
        Uri uri = new("https://127.0.0.1/");

        Assert.Throws<ArgumentException>(() => SystemBrowserOpener.BuildStartInfo(uri, DesktopPlatform.Windows));
    }

    /// <summary>Arbitrary hostname is rejected.</summary>
    [Fact]
    public void BuildStartInfo_ArbitraryHost_Throws()
    {
        Uri uri = new("http://evil.example/");

        Assert.Throws<ArgumentException>(() => SystemBrowserOpener.BuildStartInfo(uri, DesktopPlatform.Windows));
    }

    /// <summary>Hostname with localhost suffix is rejected (not exact match).</summary>
    [Fact]
    public void BuildStartInfo_LocalhostWithSuffix_Throws()
    {
        Uri uri = new("http://localhost.evil.example/");

        Assert.Throws<ArgumentException>(() => SystemBrowserOpener.BuildStartInfo(uri, DesktopPlatform.Windows));
    }

    /// <summary>IP with localhost suffix is rejected.</summary>
    [Fact]
    public void BuildStartInfo_IpWithSuffix_Throws()
    {
        Uri uri = new("http://127.0.0.1.evil.example/");

        Assert.Throws<ArgumentException>(() => SystemBrowserOpener.BuildStartInfo(uri, DesktopPlatform.Windows));
    }

    /// <summary>File scheme is rejected.</summary>
    [Fact]
    public void BuildStartInfo_FileScheme_Throws()
    {
        Uri uri = new("file:///C:/Windows/notepad.exe");

        Assert.Throws<ArgumentException>(() => SystemBrowserOpener.BuildStartInfo(uri, DesktopPlatform.Windows));
    }

    /// <summary>Localhost with custom port is accepted.</summary>
    [Fact]
    public void BuildStartInfo_LocalhostWithPort_Accepted()
    {
        Uri uri = new("http://localhost:8080/");

        System.Diagnostics.ProcessStartInfo startInfo = SystemBrowserOpener.BuildStartInfo(uri, DesktopPlatform.Windows);

        Assert.Equal("http://localhost:8080/", startInfo.FileName);
    }

    /// <summary>NoopBrowserOpener.Open does not throw.</summary>
    [Fact]
    public void NoopBrowserOpener_Open_DoesNotThrow()
    {
        NoopBrowserOpener opener = new();

        opener.Open("http://127.0.0.1:5000/");

        Assert.True(true); // Verify the call completed without throwing.
    }
}
