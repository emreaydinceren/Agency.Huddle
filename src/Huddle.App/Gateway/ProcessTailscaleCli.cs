using System.Diagnostics;
using System.Threading.Channels;

namespace Agency.Huddle.App.Gateway;

/// <summary>
/// The real <see cref="ITailscaleCli"/> (D-11): no shell, no window, redirected streams, tree kill, and on Windows and Linux
/// <see cref="ProcessStartInfo.KillOnParentExit"/>, so a Huddle crash does not orphan a <c>tailscale serve</c> child.
/// </summary>
/// <param name="configuredCommand"><c>Team:Gateway:Tailscale:Command</c>, tried after <c>PATH</c> and the platform default.</param>
internal sealed class ProcessTailscaleCli(string? configuredCommand = null) : ITailscaleCli
{
    /// <inheritdoc />
    public async Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(IReadOnlyList<string> args, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using Process process = this.Launch(args);
        using CancellationTokenSource limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(timeout);
        try
        {
            // Reads both streams together, so a full stderr pipe cannot stall a child that is still writing stdout.
            (string stdout, string stderr) = await process.ReadAllTextAsync(limit.Token).ConfigureAwait(false);
            await process.WaitForExitAsync(limit.Token).ConfigureAwait(false);
            return (process.ExitCode, stdout, stderr);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"tailscale did not finish within {timeout}.", ex);
        }
        finally
        {
            KillIfRunning(process);
        }
    }

    /// <inheritdoc />
    public Task<ITailscaleChild> StartAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        Process process = this.Launch(args);
        Channel<string> lines = Channel.CreateUnbounded<string>();
        process.OutputDataReceived += (_, e) => Forward(lines, e.Data);
        process.ErrorDataReceived += (_, e) => Forward(lines, e.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return Task.FromResult<ITailscaleChild>(new Child(process, lines));
    }

    /// <summary>The start settings for one <c>tailscale</c> call.</summary>
    /// <param name="executable">The resolved CLI path.</param>
    /// <param name="args">The arguments, without the executable.</param>
    /// <returns>Settings with no shell, no window, all three streams redirected and, where supported, the child tied to Huddle's lifetime.</returns>
    internal static ProcessStartInfo BuildStartInfo(string executable, IReadOnlyList<string> args)
    {
        ProcessStartInfo info = new(executable, args)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        // macOS has no equivalent; there a crashed Huddle can still orphan a foreground serve child (known-limits.md).
        if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
        {
            info.KillOnParentExit = true;
        }

        return info;
    }

    /// <summary>Where the Tailscale installer puts the CLI on each platform.</summary>
    /// <param name="windows">Whether the host is Windows.</param>
    /// <param name="macOs">Whether the host is macOS.</param>
    /// <param name="programFiles">The Program Files folder, used on Windows.</param>
    /// <returns>The default path of the Tailscale CLI.</returns>
    internal static string DefaultPath(bool windows, bool macOs, string programFiles)
    {
        if (windows)
        {
            return Path.Combine(programFiles, "Tailscale", "tailscale.exe");
        }

        return macOs ? "/Applications/Tailscale.app/Contents/MacOS/Tailscale" : "/usr/bin/tailscale";
    }

    private static void KillIfRunning(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // Exited or disposed between the check and the kill: nothing left to release.
        }
    }

    private static void Forward(Channel<string> lines, string? data)
    {
        if (data is not null)
        {
            lines.Writer.TryWrite(data);
        }
    }

    private static string DefaultPath()
        => DefaultPath(OperatingSystem.IsWindows(), OperatingSystem.IsMacOS(), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));

    private static string? FromPath()
    {
        string file = OperatingSystem.IsWindows() ? "tailscale.exe" : "tailscale";
        foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(directory.Trim(), file);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private Process Launch(IReadOnlyList<string> args)
    {
        string executable = FromPath() ?? (File.Exists(DefaultPath()) ? DefaultPath() : configuredCommand) ?? "tailscale";
        return Process.Start(BuildStartInfo(executable, args)) ?? throw new InvalidOperationException("tailscale did not start.");
    }

    private sealed class Child : ITailscaleChild
    {
        private readonly Process process;
        private readonly Channel<string> lines;

        public Child(Process process, Channel<string> lines)
        {
            this.process = process;
            this.lines = lines;
            this.Id = process.Id;
            this.Exited = Task.Run(async () =>
            {
                await process.WaitForExitAsync().ConfigureAwait(false);
                lines.Writer.TryComplete();
                int exitCode = process.ExitCode;
                process.Dispose();
                return exitCode;
            });
        }

        public int Id { get; }

        public IAsyncEnumerable<string> Lines => this.lines.Reader.ReadAllAsync();

        public Task<int> Exited { get; }

        public ValueTask KillAsync()
        {
            try
            {
                this.process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Already exited: nothing left to release.
            }

            return ValueTask.CompletedTask;
        }
    }
}
