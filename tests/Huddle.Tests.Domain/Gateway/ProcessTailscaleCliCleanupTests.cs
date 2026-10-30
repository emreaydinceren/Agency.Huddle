using System.Diagnostics;
using Agency.Huddle.App.Gateway;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>Review fix R-7: a cancelled <see cref="ProcessTailscaleCli.RunAsync"/> does not leave its child running.</summary>
public sealed class ProcessTailscaleCliCleanupTests
{
    /// <summary>Cancelling the caller's token kills the child process tree, not only a timeout.</summary>
    [Fact]
    public async Task RunAsync_CallerCancels_KillsTheChild()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string pidFile = Path.Combine(Path.GetTempPath(), "ts-cli-" + Guid.NewGuid().ToString("N") + ".pid");
        ProcessTailscaleCli cli = new("node");
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        string script = "require('fs').writeFileSync(process.argv[1], String(process.pid)); setTimeout(() => {}, 60000);";

        Task<(int ExitCode, string Stdout, string Stderr)> run = cli.RunAsync(["-e", script, pidFile], TimeSpan.FromSeconds(60), cts.Token);
        int pid = await WaitForPidAsync(pidFile, ct);
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);

        Assert.True(await ExitsAsync(pid, ct), "The child process was still running after cancellation.");
        File.Delete(pidFile);
    }

    private static async Task<int> WaitForPidAsync(string file, CancellationToken ct)
    {
        for (int i = 0; i < 100; i++)
        {
            if (File.Exists(file) && int.TryParse(await File.ReadAllTextAsync(file, ct), out int pid))
            {
                return pid;
            }

            await Task.Delay(100, ct);
        }

        throw new TimeoutException("The child never wrote its pid.");
    }

    private static async Task<bool> ExitsAsync(int pid, CancellationToken ct)
    {
        for (int i = 0; i < 50; i++)
        {
            try
            {
                using Process process = Process.GetProcessById(pid);
                if (process.HasExited)
                {
                    return true;
                }
            }
            catch (ArgumentException)
            {
                return true;
            }

            await Task.Delay(100, ct);
        }

        return false;
    }
}
