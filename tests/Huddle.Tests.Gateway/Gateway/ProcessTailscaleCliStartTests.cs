using System.Diagnostics;
using Agency.Huddle.App.Gateway;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>How <see cref="ProcessTailscaleCli"/> starts the CLI and reads what it prints (D-11, Spec §6.3).</summary>
public sealed class ProcessTailscaleCliStartTests
{
    /// <summary>The child gets no shell and no window, all three streams are redirected, and the arguments pass through unquoted.</summary>
    [Fact]
    public void BuildStartInfo_AnyPlatform_RedirectsWithoutShellOrWindow()
    {
        ProcessStartInfo info = ProcessTailscaleCli.BuildStartInfo("tailscale", ["serve", "--bg=false", "a b"]);

        Assert.False(info.UseShellExecute);
        Assert.True(info.CreateNoWindow);
        Assert.True(info.RedirectStandardInput);
        Assert.True(info.RedirectStandardOutput);
        Assert.True(info.RedirectStandardError);
        Assert.Equal(["serve", "--bg=false", "a b"], info.ArgumentList);
    }

    /// <summary>On Windows and Linux the child is tied to Huddle's lifetime, so a crash does not orphan a <c>tailscale serve</c> route.</summary>
    [Fact]
    public void BuildStartInfo_WindowsOrLinux_KillsTheChildOnParentExit()
    {
        ProcessStartInfo info = ProcessTailscaleCli.BuildStartInfo("tailscale", ["status"]);

        if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
        {
            Assert.True(info.KillOnParentExit);
        }
        else
        {
            Assert.Skip("KillOnParentExit is supported on Windows and Linux only.");
        }
    }

    /// <summary>A finished call returns the exit code and the captured stdout and stderr of the child.</summary>
    [Fact]
    public async Task RunAsync_ChildExits_ReturnsExitCodeAndOutput()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        ProcessTailscaleCli cli = new("dotnet");

        (int exitCode, string stdout, string stderr) = await cli.RunAsync(["--version"], TimeSpan.FromSeconds(60), ct);

        Assert.Equal(0, exitCode);
        Assert.Matches(@"^\d+\.\d+\.\d+", stdout.Trim());
        Assert.Empty(stderr);
    }
}
