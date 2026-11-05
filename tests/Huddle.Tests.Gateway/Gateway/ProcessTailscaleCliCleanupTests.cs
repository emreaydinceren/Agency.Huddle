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

    /// <summary>
    /// Polls for the pid the child writes. It reads with full sharing because the child may still hold the file open for writing, which a plain
    /// read refuses with a sharing violation on Windows; a file with nothing in it yet fails the parse and is polled again.
    /// </summary>
    private static async Task<int> WaitForPidAsync(string file, CancellationToken ct)
    {
        for (int i = 0; i < 100; i++)
        {
            if (File.Exists(file))
            {
                await using FileStream stream = new(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using StreamReader reader = new(stream);
                if (int.TryParse(await reader.ReadToEndAsync(ct), out int pid))
                {
                    return pid;
                }
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
