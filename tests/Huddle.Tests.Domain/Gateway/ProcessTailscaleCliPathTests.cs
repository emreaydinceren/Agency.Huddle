using Agency.Huddle.App.Gateway;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>The platform default locations of the Tailscale CLI (Spec §6.3).</summary>
public sealed class ProcessTailscaleCliPathTests
{
    /// <summary>On Windows the CLI is under Program Files\Tailscale.</summary>
    [Fact]
    public void DefaultPath_Windows_IsUnderProgramFiles()
    {
        string path = ProcessTailscaleCli.DefaultPath(windows: true, macOs: false, programFiles: @"C:\Program Files");

        Assert.Equal(Path.Combine(@"C:\Program Files", "Tailscale", "tailscale.exe"), path);
    }

    /// <summary>On macOS the CLI is inside the Tailscale app bundle.</summary>
    [Fact]
    public void DefaultPath_MacOs_IsInsideTheAppBundle()
    {
        string path = ProcessTailscaleCli.DefaultPath(windows: false, macOs: true, programFiles: string.Empty);

        Assert.Equal("/Applications/Tailscale.app/Contents/MacOS/Tailscale", path);
    }

    /// <summary>On Linux the CLI is in /usr/bin.</summary>
    [Fact]
    public void DefaultPath_Linux_IsInUsrBin()
    {
        string path = ProcessTailscaleCli.DefaultPath(windows: false, macOs: false, programFiles: string.Empty);

        Assert.Equal("/usr/bin/tailscale", path);
    }
}
